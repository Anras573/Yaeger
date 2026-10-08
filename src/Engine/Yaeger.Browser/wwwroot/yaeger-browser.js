/**
 * Yaeger browser interop module — WebGL 2.0 rendering backend.
 *
 * All exports are imported by Yaeger.Browser via [JSImport("functionName", "yaeger-browser")].
 * Load this module once at startup with JSHost.ImportAsync("yaeger-browser", "./yaeger-browser.js").
 */

// ---------------------------------------------------------------------------
// WebGL state
// ---------------------------------------------------------------------------

let canvas;
let gl;
let shaderProgram;
let vao;
let vbo;
let ebo;
let viewProjUniformLocation;
let textureUniformLocation;
let whiteTexture;

const MAX_QUADS = 1000;
const VERTICES_PER_QUAD = 4;
const FLOATS_PER_VERTEX = 9; // pos(3) + uv(2) + color(4)
const INDICES_PER_QUAD = 6;

/** url -> WebGLTexture, or null while loading, or whiteTexture after a 404. */
const textureCache = new Map();

const VERTEX_SHADER_SOURCE = `#version 300 es
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aTexCoord;
layout(location = 2) in vec4 aColor;

uniform mat4 uViewProj;

out vec2 vTexCoord;
out vec4 vColor;

void main() {
    gl_Position = uViewProj * vec4(aPosition, 1.0);
    vTexCoord = aTexCoord;
    vColor = aColor;
}`;

const FRAGMENT_SHADER_SOURCE = `#version 300 es
precision mediump float;

in vec2 vTexCoord;
in vec4 vColor;
out vec4 FragColor;

uniform sampler2D uTexture;

void main() {
    FragColor = texture(uTexture, vTexCoord) * vColor;
}`;

function compileShader(type, source) {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        const log = gl.getShaderInfoLog(shader);
        gl.deleteShader(shader);
        throw new Error(`[Yaeger] Shader compile error: ${log}`);
    }
    return shader;
}

function createShaderProgram(vertSrc, fragSrc) {
    const vert = compileShader(gl.VERTEX_SHADER, vertSrc);
    const frag = compileShader(gl.FRAGMENT_SHADER, fragSrc);
    const prog = gl.createProgram();
    gl.attachShader(prog, vert);
    gl.attachShader(prog, frag);
    gl.linkProgram(prog);
    gl.deleteShader(vert);
    gl.deleteShader(frag);
    if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) {
        throw new Error(`[Yaeger] Shader link error: ${gl.getProgramInfoLog(prog)}`);
    }
    return prog;
}

function generateQuadIndices(maxQuads) {
    const indices = new Uint32Array(maxQuads * INDICES_PER_QUAD);
    for (let i = 0; i < maxQuads; i++) {
        const v = i * VERTICES_PER_QUAD;
        const idx = i * INDICES_PER_QUAD;
        // Winding: TR(0), BR(1), TL(3)  +  BR(1), BL(2), TL(3)
        indices[idx + 0] = v;
        indices[idx + 1] = v + 1;
        indices[idx + 2] = v + 3;
        indices[idx + 3] = v + 1;
        indices[idx + 4] = v + 2;
        indices[idx + 5] = v + 3;
    }
    return indices;
}

function createWhiteTexture() {
    const tex = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.texImage2D(
        gl.TEXTURE_2D, 0, gl.RGBA, 1, 1, 0,
        gl.RGBA, gl.UNSIGNED_BYTE,
        new Uint8Array([255, 255, 255, 255])
    );
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
    return tex;
}

/** Sampling values mirror Yaeger.Platform.TextureFilter / TextureWrap. */
const FILTER_NEAREST = 0;
const FILTER_LINEAR_MIPMAP = 2;
const WRAP_REPEAT = 1;

// Matches TextureSampling.Default (Linear + Clamp) on the native runtime.
let defaultSampling = { filter: 1, wrap: 0 };
const samplingOverrides = new Map();

function getSampling(url) {
    return samplingOverrides.get(url) || defaultSampling;
}

/** Applies filter/wrap to tex (binding it) and generates mipmaps when required. */
function applySampling(tex, sampling) {
    gl.bindTexture(gl.TEXTURE_2D, tex);
    const nearest = sampling.filter === FILTER_NEAREST;
    const mips = sampling.filter === FILTER_LINEAR_MIPMAP;
    if (mips) gl.generateMipmap(gl.TEXTURE_2D);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER,
        nearest ? gl.NEAREST : mips ? gl.LINEAR_MIPMAP_LINEAR : gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, nearest ? gl.NEAREST : gl.LINEAR);
    const wrap = sampling.wrap === WRAP_REPEAT ? gl.REPEAT : gl.CLAMP_TO_EDGE;
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, wrap);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, wrap);
}

/** Sets the sampling used by textures with no per-URL override (applies to textures loaded afterwards). */
export function setDefaultTextureSampling(filter, wrap) {
    defaultSampling = { filter, wrap };
}

/** Overrides sampling for one texture URL; applied immediately if it is already loaded. */
export function setTextureSampling(url, filter, wrap) {
    const sampling = { filter, wrap };
    samplingOverrides.set(url, sampling);
    const tex = textureCache.get(url);
    if (gl && tex && tex !== whiteTexture) applySampling(tex, sampling);
}

/** url -> Promise that settles once the texture is uploaded (or rejects on failure). */
const textureLoads = new Map();
/** url -> [width, height] in pixels, set once the image is uploaded. */
const textureSizes = new Map();
/** url -> error message for loads that failed. */
const textureErrors = new Map();

/** Starts (once) the async load+upload of url; returns the shared promise. */
function startTextureLoad(url) {
    let load = textureLoads.get(url);
    if (load) return load;

    textureCache.set(url, null); // mark as in-flight
    load = new Promise((resolve, reject) => {
        const img = new Image();
        const fail = (message) => {
            console.warn(`[Yaeger] ${message}`);
            textureErrors.set(url, message);
            textureCache.set(url, whiteTexture);
            reject(new Error(message));
        };
        img.onload = async () => {
            try { await img.decode(); } catch { /* onload already fired; upload below decodes if needed */ }
            if (!gl) { reject(new Error('Canvas disposed')); return; }
            const tex = gl.createTexture();
            gl.bindTexture(gl.TEXTURE_2D, tex);
            // Flip Y so row 0 is at the bottom, matching StbImageSharp FlipVerticallyOnLoad
            // used by the desktop Texture loader. Without this, sprites render upside-down.
            gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
            gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, img);
            gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
            applySampling(tex, getSampling(url));
            textureSizes.set(url, [img.naturalWidth, img.naturalHeight]);
            textureCache.set(url, tex);
            resolve();
        };
        img.onerror = () => fail(`Failed to load texture: ${url}`);
        img.src = url;
    });
    textureLoads.set(url, load);
    // Lazy loads (first drawBatch) never await the promise; avoid unhandled-rejection noise.
    load.catch(() => { });
    return load;
}

/**
 * Returns the WebGLTexture for the given URL, starting an async load on first access.
 * Returns whiteTexture (1×1 white) while loading or when url is empty/null.
 */
function getOrLoadTexture(url) {
    if (!url) return whiteTexture;

    if (textureCache.has(url)) {
        return textureCache.get(url) || whiteTexture;
    }

    startTextureLoad(url);
    return whiteTexture;
}

/** Starts loading url (if not already) and resolves once it is decoded and uploaded; rejects on failure. */
export function preloadTexture(url) {
    if (!url) return Promise.resolve();
    return startTextureLoad(url);
}

/** True once url has been loaded and uploaded successfully (empty url is always ready). */
export function isTextureReady(url) {
    if (!url) return true;
    return textureSizes.has(url);
}

/** Returns [width, height] in pixels of a loaded texture, or [0, 0] if it is not ready. */
export function getTextureSize(url) {
    return textureSizes.get(url) || [0, 0];
}

/** Returns the load error message for url, or null if it has not failed. */
export function getTextureError(url) {
    return textureErrors.get(url) ?? null;
}

// ---------------------------------------------------------------------------
// Text: Canvas 2D glyph atlases
// ---------------------------------------------------------------------------

const ATLAS_PAGE_SIZE = 1024;

/** pathPrefix -> { family, px, dpr, pages: [{canvas, ctx, tex, dirty}], pen: {x, y, rowH}, glyphs: Map } */
const glyphAtlases = new Map();

/**
 * Loads a font file (woff2/ttf/...) through the FontFace API and registers it under
 * <paramref name="family"/> so Text entities can use it by that name.
 */
export async function fontLoad(family, url) {
    const face = new FontFace(family, `url(${url})`);
    await face.load();
    document.fonts.add(face);
}

function fontCss(family, px) {
    return `${px}px "${family}"`;
}

/** Returns [lineHeight, ascent] in CSS pixels for the font at sizePx. */
export function fontLineMetrics(family, sizePx, dpr) {
    const ctx = new OffscreenCanvas(1, 1).getContext('2d');
    ctx.font = fontCss(family, sizePx * dpr);
    const m = ctx.measureText('Mg');
    const asc = m.fontBoundingBoxAscent ?? sizePx * dpr * 0.9;
    const desc = m.fontBoundingBoxDescent ?? sizePx * dpr * 0.3;
    return [(asc + desc) / dpr, asc / dpr];
}

function newAtlasPage(atlas, path) {
    const canvas = new OffscreenCanvas(ATLAS_PAGE_SIZE, ATLAS_PAGE_SIZE);
    const ctx = canvas.getContext('2d');
    ctx.font = fontCss(atlas.family, atlas.px * atlas.dpr);
    ctx.textBaseline = 'alphabetic';
    ctx.fillStyle = '#fff';
    const page = { canvas, ctx, path, dirty: false };
    atlas.pages.push(page);
    atlas.pen = { x: 0, y: 0, rowH: 0 };
    return page;
}

function uploadAtlasPage(page) {
    if (!gl || !page.dirty) return;
    let tex = textureCache.get(page.path);
    if (!tex || tex === whiteTexture) {
        tex = gl.createTexture();
        textureCache.set(page.path, tex);
    }
    gl.bindTexture(gl.TEXTURE_2D, tex);
    // Same orientation as image textures: row 0 of the canvas is the top, v = 1.
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, page.canvas);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    page.dirty = false;
}

/**
 * Rasterizes any not-yet-cached codepoints into the atlas for (family, size, dpr) and returns
 * 11 numbers per requested codepoint:
 *   [codepoint, page, advance, offsetX, offsetY, width, height, u0, v0, u1, v1]
 * Lengths are in CSS pixels (raster size / dpr); offsetY is the quad bottom relative to the
 * baseline (Y up); page -1 means no ink. Page p is the texture `${pathPrefix}${p}`.
 */
export function glyphAtlasEnsure(family, sizePx, dpr, pathPrefix, codepoints) {
    let atlas = glyphAtlases.get(pathPrefix);
    if (!atlas) {
        atlas = { family, px: sizePx, dpr, pages: [], pen: null, glyphs: new Map() };
        glyphAtlases.set(pathPrefix, atlas);
    }

    const out = [];
    for (const cp of codepoints) {
        let g = atlas.glyphs.get(cp);
        if (!g) {
            g = rasterizeGlyph(atlas, pathPrefix, cp);
            atlas.glyphs.set(cp, g);
        }
        out.push(cp, ...g);
    }
    for (const page of atlas.pages) uploadAtlasPage(page);
    return out;
}

function rasterizeGlyph(atlas, pathPrefix, cp) {
    const { dpr } = atlas;
    const ch = String.fromCodePoint(cp);
    let page = atlas.pages[atlas.pages.length - 1];
    if (!page) page = newAtlasPage(atlas, pathPrefix + '0');
    const m = page.ctx.measureText(ch);
    const advance = m.width / dpr;

    const ink = m.actualBoundingBoxLeft + m.actualBoundingBoxRight;
    if (!(ink > 0)) return [-1, advance, 0, 0, 0, 0, 0, 0, 0, 0];

    // One pixel of padding on every side keeps linear filtering from bleeding neighbours in.
    const L = Math.ceil(m.actualBoundingBoxLeft) + 1;
    const R = Math.ceil(m.actualBoundingBoxRight) + 1;
    const A = Math.ceil(m.actualBoundingBoxAscent) + 1;
    const D = Math.ceil(m.actualBoundingBoxDescent) + 1;
    const cw = Math.min(L + R, ATLAS_PAGE_SIZE);
    const chh = Math.min(A + D, ATLAS_PAGE_SIZE);

    let pen = atlas.pen;
    if (pen.x + cw > ATLAS_PAGE_SIZE) {
        pen.x = 0;
        pen.y += pen.rowH;
        pen.rowH = 0;
    }
    if (pen.y + chh > ATLAS_PAGE_SIZE) {
        page = newAtlasPage(atlas, pathPrefix + atlas.pages.length);
        pen = atlas.pen;
    }

    const cx = pen.x, cy = pen.y;
    page.ctx.fillText(ch, cx + L, cy + A);
    page.dirty = true;
    pen.x += cw;
    pen.rowH = Math.max(pen.rowH, chh);

    const pageIndex = atlas.pages.length - 1;
    const S = ATLAS_PAGE_SIZE;
    return [
        pageIndex, advance, -L / dpr, -D / dpr, cw / dpr, chh / dpr,
        cx / S, 1 - (cy + chh) / S, (cx + cw) / S, 1 - cy / S,
    ];
}

// ---------------------------------------------------------------------------
// Input state  (unchanged from Canvas 2D version)
// ---------------------------------------------------------------------------

const pressedKeys = new Set();
// DOM key codes whose browser default (scroll, focus change, reload, ...) is suppressed.
let preventDefaultKeys = new Set();
let mouseX = 0;
let mouseY = 0;
// True while a mouse/primary pointer is over the canvas. Starts false: no pointer event yet.
let mouseInside = false;
let scrollDelta = 0;
const mouseButtons = new Set();
// Edges recorded by DOM events since the last takeInputEdges() snapshot. Kept separate from the
// held sets so a down+up that both land between two ticks is still observable as a click.
const keysDownThisFrame = new Set();
const keysUpThisFrame = new Set();
const buttonsDownThisFrame = new Set();
const buttonsUpThisFrame = new Set();
let activePrimaryPointerId;
const PIXELS_PER_LINE = 16;
const WHEEL_EVENT_OPTIONS = { passive: false };
const POINTER_EVENT_OPTIONS = { passive: false };

let resizeCanvasHandler;
let keyDownHandler;
let keyUpHandler;
let pointerDownHandler;
let pointerMoveHandler;
let pointerEnterHandler;
let pointerLeaveHandler;
let pointerUpHandler;
let pointerCancelHandler;
let wheelHandler;
let blurHandler;
let contextMenuHandler;

function pressKey(code) {
    if (pressedKeys.has(code)) return;
    pressedKeys.add(code);
    keysDownThisFrame.add(code);
}

function releaseKey(code) {
    if (!pressedKeys.delete(code)) return;
    keysUpThisFrame.add(code);
}

function pressButton(button) {
    if (mouseButtons.has(button)) return;
    mouseButtons.add(button);
    buttonsDownThisFrame.add(button);
}

function releaseButton(button) {
    if (!mouseButtons.delete(button)) return;
    buttonsUpThisFrame.add(button);
}

function clearInputState() {
    pressedKeys.clear();
    mouseButtons.clear();
    keysDownThisFrame.clear();
    keysUpThisFrame.clear();
    buttonsDownThisFrame.clear();
    buttonsUpThisFrame.clear();
    scrollDelta = 0;
    activePrimaryPointerId = undefined;
}

function normalizeWheelDeltaToPixels(e) {
    if (e.deltaMode === WheelEvent.DOM_DELTA_LINE) {
        return e.deltaY * PIXELS_PER_LINE;
    }
    if (e.deltaMode === WheelEvent.DOM_DELTA_PAGE) {
        const pageHeight = canvas?.clientHeight || window.innerHeight || 0;
        return e.deltaY * pageHeight;
    }
    return e.deltaY;
}

function setupInputListeners() {
    resizeCanvasHandler = () => {
        if (!canvas) return;
        const dpr = window.devicePixelRatio || 1;
        canvas.width = Math.round(canvas.clientWidth * dpr);
        canvas.height = Math.round(canvas.clientHeight * dpr);
        if (gl) gl.viewport(0, 0, canvas.width, canvas.height);
    };

    keyDownHandler = (e) => {
        if (!e.code) return;
        pressKey(e.code);
        if (preventDefaultKeys.has(e.code)) e.preventDefault();
    };
    keyUpHandler = (e) => { if (e.code) releaseKey(e.code); };

    pointerMoveHandler = (e) => {
        if (!canvas) return;
        if (e.pointerType !== 'mouse' && e.pointerId !== activePrimaryPointerId) return;
        const rect = canvas.getBoundingClientRect();
        mouseX = e.clientX - rect.left;
        mouseY = e.clientY - rect.top;
        mouseInside = true;
        if (e.pointerType !== 'mouse') e.preventDefault();
    };

    pointerEnterHandler = (e) => {
        if (e.pointerType !== 'mouse') return;
        mouseInside = true;
    };

    // Touch pointers are captured on down, so only mouse pointers report a meaningful leave.
    pointerLeaveHandler = (e) => {
        if (e.pointerType !== 'mouse') return;
        mouseInside = false;
    };

    pointerDownHandler = (e) => {
        if (!canvas) return;
        if (e.pointerType === 'mouse') {
            const rect = canvas.getBoundingClientRect();
            mouseX = e.clientX - rect.left;
            mouseY = e.clientY - rect.top;
            mouseInside = true;
            pressButton(e.button);
            return;
        }
        if (activePrimaryPointerId === undefined) activePrimaryPointerId = e.pointerId;
        if (e.pointerId !== activePrimaryPointerId) return;
        const rect = canvas.getBoundingClientRect();
        mouseX = e.clientX - rect.left;
        mouseY = e.clientY - rect.top;
        mouseInside = true;
        pressButton(0);
        if (canvas.setPointerCapture) canvas.setPointerCapture(e.pointerId);
        e.preventDefault();
    };

    pointerUpHandler = (e) => {
        if (!canvas) return;
        if (e.pointerType === 'mouse') { releaseButton(e.button); return; }
        if (e.pointerId !== activePrimaryPointerId) return;
        releaseButton(0);
        activePrimaryPointerId = undefined;
        // A touch has no hover: once the finger lifts the pointer is no longer over the canvas.
        mouseInside = false;
        if (canvas.hasPointerCapture?.(e.pointerId)) canvas.releasePointerCapture(e.pointerId);
        e.preventDefault();
    };

    pointerCancelHandler = (e) => {
        if (!canvas || e.pointerType === 'mouse' || e.pointerId !== activePrimaryPointerId) return;
        releaseButton(0);
        activePrimaryPointerId = undefined;
        mouseInside = false;
        if (canvas.hasPointerCapture?.(e.pointerId)) canvas.releasePointerCapture(e.pointerId);
        e.preventDefault();
    };

    wheelHandler = (e) => { scrollDelta += normalizeWheelDeltaToPixels(e); e.preventDefault(); };
    // Losing focus releases everything held, so gameplay still sees the release edges.
    blurHandler = () => {
        for (const code of [...pressedKeys]) releaseKey(code);
        for (const button of [...mouseButtons]) releaseButton(button);
        activePrimaryPointerId = undefined;
        // The last position is kept for callers who want it.
        mouseInside = false;
    };
    contextMenuHandler = (e) => e.preventDefault();

    resizeCanvasHandler();
    window.addEventListener('resize', resizeCanvasHandler);
    window.addEventListener('keydown', keyDownHandler);
    window.addEventListener('keyup', keyUpHandler);
    canvas.addEventListener('pointermove', pointerMoveHandler, POINTER_EVENT_OPTIONS);
    canvas.addEventListener('pointerdown', pointerDownHandler, POINTER_EVENT_OPTIONS);
    canvas.addEventListener('pointerenter', pointerEnterHandler, POINTER_EVENT_OPTIONS);
    canvas.addEventListener('pointerleave', pointerLeaveHandler, POINTER_EVENT_OPTIONS);
    window.addEventListener('pointerup', pointerUpHandler, POINTER_EVENT_OPTIONS);
    window.addEventListener('pointercancel', pointerCancelHandler, POINTER_EVENT_OPTIONS);
    canvas.addEventListener('wheel', wheelHandler, WHEEL_EVENT_OPTIONS);
    window.addEventListener('blur', blurHandler);
    canvas.addEventListener('contextmenu', contextMenuHandler);
}

function removeInputListeners() {
    if (resizeCanvasHandler) {
        window.removeEventListener('resize', resizeCanvasHandler);
        resizeCanvasHandler = undefined;
    }
    if (keyDownHandler) {
        window.removeEventListener('keydown', keyDownHandler);
        keyDownHandler = undefined;
    }
    if (keyUpHandler) {
        window.removeEventListener('keyup', keyUpHandler);
        keyUpHandler = undefined;
    }
    if (pointerMoveHandler && canvas) {
        canvas.removeEventListener('pointermove', pointerMoveHandler, POINTER_EVENT_OPTIONS);
        pointerMoveHandler = undefined;
    }
    if (pointerDownHandler && canvas) {
        canvas.removeEventListener('pointerdown', pointerDownHandler, POINTER_EVENT_OPTIONS);
        pointerDownHandler = undefined;
    }
    if (pointerEnterHandler && canvas) {
        canvas.removeEventListener('pointerenter', pointerEnterHandler, POINTER_EVENT_OPTIONS);
        pointerEnterHandler = undefined;
    }
    if (pointerLeaveHandler && canvas) {
        canvas.removeEventListener('pointerleave', pointerLeaveHandler, POINTER_EVENT_OPTIONS);
        pointerLeaveHandler = undefined;
    }
    if (pointerUpHandler) {
        window.removeEventListener('pointerup', pointerUpHandler, POINTER_EVENT_OPTIONS);
        pointerUpHandler = undefined;
    }
    if (pointerCancelHandler) {
        window.removeEventListener('pointercancel', pointerCancelHandler, POINTER_EVENT_OPTIONS);
        pointerCancelHandler = undefined;
    }
    if (wheelHandler && canvas) {
        canvas.removeEventListener('wheel', wheelHandler, WHEEL_EVENT_OPTIONS);
        wheelHandler = undefined;
    }
    if (blurHandler) {
        window.removeEventListener('blur', blurHandler);
        blurHandler = undefined;
    }
    if (contextMenuHandler && canvas) {
        canvas.removeEventListener('contextmenu', contextMenuHandler);
        contextMenuHandler = undefined;
    }
}

// ---------------------------------------------------------------------------
// Exported API
// ---------------------------------------------------------------------------

/**
 * Initialises the WebGL 2.0 rendering context, compiles shaders, allocates GPU
 * buffers, and registers input event listeners for the given canvas element.
 * Must be called once before any other export.
 */
export function initWebGL(canvasId) {
    disposeCanvas();

    canvas = document.getElementById(canvasId);
    if (!canvas) throw new Error(`[Yaeger] Canvas element '#${canvasId}' not found.`);

    gl = canvas.getContext('webgl2');
    if (!gl) throw new Error('[Yaeger] WebGL 2.0 is not supported in this browser.');

    canvas.style.touchAction = 'none';

    // Shaders
    shaderProgram = createShaderProgram(VERTEX_SHADER_SOURCE, FRAGMENT_SHADER_SOURCE);
    viewProjUniformLocation = gl.getUniformLocation(shaderProgram, 'uViewProj');
    textureUniformLocation = gl.getUniformLocation(shaderProgram, 'uTexture');

    gl.useProgram(shaderProgram);
    gl.uniform1i(textureUniformLocation, 0);
    // Identity view-projection as default
    gl.uniformMatrix4fv(viewProjUniformLocation, false, [
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1,
    ]);

    // VBO — dynamic, large enough for one full batch
    vbo = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
    gl.bufferData(
        gl.ARRAY_BUFFER,
        MAX_QUADS * VERTICES_PER_QUAD * FLOATS_PER_VERTEX * 4, // bytes
        gl.DYNAMIC_DRAW
    );

    // EBO — static index buffer, shared across all batches
    ebo = gl.createBuffer();
    gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, ebo);
    gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, generateQuadIndices(MAX_QUADS), gl.STATIC_DRAW);

    // VAO — captures attribute layout once
    vao = gl.createVertexArray();
    gl.bindVertexArray(vao);
    gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
    gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, ebo);

    const stride = FLOATS_PER_VERTEX * 4; // bytes per vertex
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 3, gl.FLOAT, false, stride, 0);        // position (3 floats)
    gl.enableVertexAttribArray(1);
    gl.vertexAttribPointer(1, 2, gl.FLOAT, false, stride, 3 * 4);    // texcoord (2 floats)
    gl.enableVertexAttribArray(2);
    gl.vertexAttribPointer(2, 4, gl.FLOAT, false, stride, 5 * 4);    // color    (4 floats)

    gl.bindVertexArray(null);

    // 1×1 white fallback texture (used for empty paths and while async loads are in flight)
    whiteTexture = createWhiteTexture();

    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);

    setupInputListeners();
}

/**
 * Clears the colour buffer and updates the WebGL viewport if the canvas was resized.
 */
export function getViewport() {
    if (!canvas) return [0, 0, window.devicePixelRatio || 1];
    return [canvas.clientWidth, canvas.clientHeight, window.devicePixelRatio || 1];
}

export function clearFrame(r, g, b, a) {
    if (!gl) return;

    const dpr = window.devicePixelRatio || 1;
    const w = Math.round(canvas.clientWidth * dpr);
    const h = Math.round(canvas.clientHeight * dpr);
    if (canvas.width !== w || canvas.height !== h) {
        canvas.width = w;
        canvas.height = h;
        gl.viewport(0, 0, w, h);
    }

    gl.clearColor(r, g, b, a);
    gl.clear(gl.COLOR_BUFFER_BIT);
}

/**
 * Updates the view-projection matrix uniform used for all subsequent draw calls.
 * <paramref name="matrixBytes"/> is a Uint8Array of 64 bytes — the raw memory of a
 * System.Numerics.Matrix4x4 (16 × IEEE 754 float, row-major). The bytes are
 * reinterpreted as a Float32Array and passed with transpose=false, matching the
 * convention used by the desktop OpenGL renderer.
 */
export function setViewProjection(matrixBytes) {
    if (!gl || !shaderProgram) return;
    gl.useProgram(shaderProgram);
    // Copy to a fresh aligned buffer so Float32Array view is always valid.
    const aligned = new Uint8Array(64);
    aligned.set(matrixBytes.subarray(0, 64));
    gl.uniformMatrix4fv(viewProjUniformLocation, false, new Float32Array(aligned.buffer));
}

/**
 * Renders one texture batch.  <paramref name="vertexBytes"/> is a Uint8Array containing
 * the raw bytes of the C# float vertex scratch buffer (9 floats × 4 bytes per vertex,
 * 4 vertices per quad); only the first <c>quadCount * 4 * 9 * 4</c> bytes are uploaded.
 * The texture for <paramref name="textureUrl"/> is loaded asynchronously on first use;
 * a 1×1 white fallback is used while it is in flight so tint colour still renders.
 */
export function drawBatch(textureUrl, vertexBytes, quadCount) {
    if (!gl || quadCount <= 0) return;

    const texture = getOrLoadTexture(textureUrl);
    const byteCount = quadCount * VERTICES_PER_QUAD * FLOATS_PER_VERTEX * 4;

    gl.useProgram(shaderProgram);
    gl.bindVertexArray(vao);

    gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
    // Upload only the live portion. bufferSubData with a Uint8Array uses byte counts.
    gl.bufferSubData(gl.ARRAY_BUFFER, 0, vertexBytes, 0, byteCount);

    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, texture);

    gl.drawElements(gl.TRIANGLES, quadCount * INDICES_PER_QUAD, gl.UNSIGNED_INT, 0);

    gl.bindVertexArray(null);
}

/**
 * Releases all WebGL resources, removes input event listeners, and resets state.
 */
export function disposeCanvas() {
    if (gl) {
        textureCache.forEach((tex) => {
            if (tex && tex !== whiteTexture) gl.deleteTexture(tex);
        });
        textureCache.clear();
        textureLoads.clear();
        textureSizes.clear();
        textureErrors.clear();
        glyphAtlases.clear();
        if (whiteTexture) { gl.deleteTexture(whiteTexture); whiteTexture = null; }
        if (vao) { gl.deleteVertexArray(vao); vao = null; }
        if (vbo) { gl.deleteBuffer(vbo); vbo = null; }
        if (ebo) { gl.deleteBuffer(ebo); ebo = null; }
        if (shaderProgram) { gl.deleteProgram(shaderProgram); shaderProgram = null; }
        gl = null;
    }

    removeInputListeners();
    clearInputState();
    mouseX = 0;
    mouseY = 0;
    mouseInside = false;
    canvas = null;
}

export function setPreventDefaultKeys(codes) {
    preventDefaultKeys = new Set(codes);
}

export function isKeyPressed(key) {
    return pressedKeys.has(key);
}

export function isMouseButtonPressed(button) {
    return mouseButtons.has(button);
}

// Returns the keys / buttons that went down or up since the previous call, then clears them.
// Called once per tick by BrowserInputState.BeginFrame.
export function takeKeyDownCodes() {
    const codes = [...keysDownThisFrame];
    keysDownThisFrame.clear();
    return codes;
}

export function takeKeyUpCodes() {
    const codes = [...keysUpThisFrame];
    keysUpThisFrame.clear();
    return codes;
}

export function takeMouseDownButtons() {
    const buttons = [...buttonsDownThisFrame];
    buttonsDownThisFrame.clear();
    return buttons;
}

export function takeMouseUpButtons() {
    const buttons = [...buttonsUpThisFrame];
    buttonsUpThisFrame.clear();
    return buttons;
}

export function isMouseInside() {
    return mouseInside;
}

export function getMouseX() {
    return mouseX;
}

export function getMouseY() {
    return mouseY;
}

export function getMouseXNdc() {
    if (!canvas || canvas.clientWidth === 0) return 0;
    return (mouseX / canvas.clientWidth) * 2 - 1;
}

export function getMouseYNdc() {
    if (!canvas || canvas.clientHeight === 0) return 0;
    return 1 - (mouseY / canvas.clientHeight) * 2;
}

export function getAndResetScrollDelta() {
    const delta = scrollDelta;
    scrollDelta = 0;
    return delta;
}

// --- requestAnimationFrame pump ------------------------------------------------------------
// Drives the game loop from JS and calls straight into a C# callback, so hosts don't need a
// DotNetObjectReference / invokeMethodAsync round trip per frame.
let rafRunning = false;
let rafHandle = 0;

export function startGameLoop(tick) {
    if (rafRunning) return;
    rafRunning = true;
    function loop(timestamp) {
        if (!rafRunning) return;
        try {
            tick(timestamp);
        } catch (err) {
            console.error('[Yaeger] Game loop error:', err);
            rafRunning = false;
            return;
        }
        if (rafRunning) rafHandle = requestAnimationFrame(loop);
    }
    rafHandle = requestAnimationFrame(loop);
}

export function stopGameLoop() {
    rafRunning = false;
    cancelAnimationFrame(rafHandle);
}

// ---------------------------------------------------------------------------
// Audio: WebAudio backend for Yaeger.Platform.IAudioOutput
// ---------------------------------------------------------------------------
// Graph: SFX voice -> sfxGain -> masterGain -> destination
//        <audio> element -> per-stream gain -> musicGain -> masterGain -> destination
//
// Browsers block audio until a user gesture, and creating/resuming an AudioContext earlier logs
// an autoplay-policy warning. So the live AudioContext is only created inside the first
// pointer/key/touch event. Until then SFX are decoded on an OfflineAudioContext (allowed at any
// time, and the resulting AudioBuffer plays in any context), SFX plays are dropped, and a music
// stream asked to play is queued and starts on that first gesture.

const AUDIO_GROUP_MUSIC = 0;
const AUDIO_UNLOCK_EVENTS = ['pointerdown', 'pointerup', 'touchend', 'keydown'];
const AUDIO_UNLOCK_OPTIONS = { capture: true, passive: true };

let audioCtx; // live context; undefined until the first user gesture
let audioMaster;
let audioMusic;
let audioSfx;
let audioDecoder; // OfflineAudioContext, used for decodeAudioData before/after unlock
let audioInitialized = false;
let audioMaxVoices = 8;
const audioVolumes = { master: 1, music: 1, sfx: 1 };
const audioSounds = new Map(); // id -> { buffer, voices: AudioBufferSourceNode[] }
const audioStreams = new Map(); // id -> stream record
let audioNextId = 1;

function audioCanPlay(type) {
    const probe = document.createElement('audio');
    return !!probe.canPlayType && probe.canPlayType(type) !== '';
}

let audioOggSupported;

/**
 * Ogg Vorbis is not reliably decodable in Safari. When a path ends in .ogg and the browser can't
 * play it, load the sibling file with the same basename instead: .m4a (AAC) if supported,
 * otherwise .mp3. Ship those siblings next to the .ogg to support such browsers.
 */
function audioResolveUrl(path) {
    if (audioOggSupported === undefined) {
        audioOggSupported = audioCanPlay('audio/ogg; codecs="vorbis"');
    }
    if (audioOggSupported || !/\.ogg(\?.*)?$/i.test(path)) return path;
    const ext = audioCanPlay('audio/mp4; codecs="mp4a.40.2"') ? 'm4a' : 'mp3';
    return path.replace(/\.ogg(\?.*)?$/i, `.${ext}$1`);
}

function audioApplyVolumes() {
    if (!audioCtx) return;
    audioMaster.gain.value = audioVolumes.master;
    audioMusic.gain.value = audioVolumes.music;
    audioSfx.gain.value = audioVolumes.sfx;
}

function audioStartStream(stream) {
    if (!audioCtx) return;
    if (!stream.node) {
        stream.node = audioCtx.createGain();
        stream.node.gain.value = stream.gain;
        audioCtx.createMediaElementSource(stream.el).connect(stream.node);
        stream.node.connect(audioMusic);
    }
    stream.el.play().catch((err) => console.warn('[Yaeger] Music playback failed:', err));
}

function audioUnlock() {
    if (audioCtx) return;
    for (const type of AUDIO_UNLOCK_EVENTS) {
        window.removeEventListener(type, audioUnlock, AUDIO_UNLOCK_OPTIONS);
    }
    const Ctor = window.AudioContext || window.webkitAudioContext;
    if (!Ctor) {
        console.warn('[Yaeger] WebAudio is not supported in this browser.');
        return;
    }
    audioCtx = new Ctor();
    audioMaster = audioCtx.createGain();
    audioMusic = audioCtx.createGain();
    audioSfx = audioCtx.createGain();
    audioMusic.connect(audioMaster);
    audioSfx.connect(audioMaster);
    audioMaster.connect(audioCtx.destination);
    audioApplyVolumes();
    // Created inside a gesture it is already running; resume() covers browsers that start
    // contexts suspended regardless.
    if (audioCtx.state === 'suspended') audioCtx.resume().catch(() => {});
    for (const stream of audioStreams.values()) {
        if (stream.wantPlay) audioStartStream(stream);
    }
}

/** Installs the first-gesture unlock. maxVoicesPerSound caps simultaneous plays of one sound. */
export function audioInit(maxVoicesPerSound) {
    if (audioInitialized) return;
    audioInitialized = true;
    audioMaxVoices = Math.max(1, maxVoicesPerSound | 0);
    for (const type of AUDIO_UNLOCK_EVENTS) {
        window.addEventListener(type, audioUnlock, AUDIO_UNLOCK_OPTIONS);
    }
}

export function audioSetVolumes(master, music, sfx) {
    audioVolumes.master = master;
    audioVolumes.music = music;
    audioVolumes.sfx = sfx;
    audioApplyVolumes();
}

/** Fetches and fully decodes a short sound; resolves to its handle id (> 0). */
export async function audioLoad(path) {
    const url = audioResolveUrl(path);
    const response = await fetch(url);
    if (!response.ok) {
        throw new Error(`Failed to load audio '${url}': HTTP ${response.status}`);
    }
    const bytes = await response.arrayBuffer();
    audioDecoder ??= new (window.OfflineAudioContext || window.webkitOfflineAudioContext)(
        1,
        1,
        48000,
    );
    const buffer = await audioDecoder.decodeAudioData(bytes);
    const id = audioNextId++;
    audioSounds.set(id, { buffer, voices: [] });
    return id;
}

export function audioPlay(id, gain, pitch, group) {
    const sound = audioSounds.get(id);
    if (!sound || !audioCtx || audioCtx.state !== 'running') return;

    // Voice budget per sound: steal the oldest voice rather than stack unboundedly.
    while (sound.voices.length >= audioMaxVoices) {
        const oldest = sound.voices.shift();
        oldest.onended = null;
        try {
            oldest.stop();
        } catch {}
        oldest.disconnect();
    }

    const source = audioCtx.createBufferSource();
    source.buffer = sound.buffer;
    source.playbackRate.value = pitch;
    const voiceGain = audioCtx.createGain();
    voiceGain.gain.value = gain;
    source.connect(voiceGain);
    voiceGain.connect(group === AUDIO_GROUP_MUSIC ? audioMusic : audioSfx);
    source.onended = () => {
        const index = sound.voices.indexOf(source);
        if (index >= 0) sound.voices.splice(index, 1);
        source.disconnect();
        voiceGain.disconnect();
    };
    sound.voices.push(source);
    source.start();
}

/** Opens a streamed track backed by an <audio> element; resolves to its handle id. */
export function audioOpenStream(path) {
    const el = new Audio();
    el.preload = 'auto';
    el.crossOrigin = 'anonymous';
    el.src = audioResolveUrl(path);
    const id = audioNextId++;
    audioStreams.set(id, { el, node: undefined, gain: 1, wantPlay: false });
    return id;
}

export function audioStreamPlay(id) {
    const stream = audioStreams.get(id);
    if (!stream) return;
    stream.wantPlay = true;
    audioStartStream(stream); // no-op until unlocked; audioUnlock starts it then
}

export function audioStreamPause(id) {
    const stream = audioStreams.get(id);
    if (!stream) return;
    stream.wantPlay = false;
    stream.el.pause();
}

export function audioStreamStop(id) {
    const stream = audioStreams.get(id);
    if (!stream) return;
    stream.wantPlay = false;
    stream.el.pause();
    stream.el.currentTime = 0;
}

export function audioStreamSetLooping(id, looping) {
    const stream = audioStreams.get(id);
    if (stream) stream.el.loop = looping;
}

export function audioStreamSetGain(id, gain) {
    const stream = audioStreams.get(id);
    if (!stream) return;
    stream.gain = gain;
    if (stream.node) stream.node.gain.value = gain;
}

export function audioStreamDispose(id) {
    const stream = audioStreams.get(id);
    if (!stream) return;
    audioStreams.delete(id);
    stream.el.pause();
    stream.el.removeAttribute('src');
    stream.el.load();
    stream.node?.disconnect();
}
