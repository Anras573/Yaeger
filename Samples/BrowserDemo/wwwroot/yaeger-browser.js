/**
 * Yaeger browser interop module.
 *
 * All exports are imported by Yaeger.Browser via [JSImport("functionName", "yaeger-browser")].
 * Load this module once at startup with JSHost.ImportAsync("yaeger-browser", "./yaeger-browser.js").
 */

let canvas;
let ctx;
const pressedKeys = new Set();
let mouseX = 0;
let mouseY = 0;
let scrollDelta = 0;
const mouseButtons = new Set();

/**
 * Initialises the 2D rendering context and input listeners for the given canvas element.
 * Must be called once before any other export.
 */
export function initCanvas(canvasId) {
    canvas = document.getElementById(canvasId);
    if (!canvas) {
        throw new Error(`[Yaeger] Canvas element '#${canvasId}' not found.`);
    }
    ctx = canvas.getContext('2d');
    if (!ctx) {
        throw new Error(`[Yaeger] Failed to acquire 2D context for canvas '#${canvasId}'.`);
    }

    function resizeCanvas() {
        canvas.width = canvas.clientWidth;
        canvas.height = canvas.clientHeight;
    }
    resizeCanvas();
    window.addEventListener('resize', resizeCanvas);

    window.addEventListener('keydown', (e) => pressedKeys.add(e.key.length === 1 ? e.key.toLowerCase() : e.key));
    window.addEventListener('keyup', (e) => pressedKeys.delete(e.key.length === 1 ? e.key.toLowerCase() : e.key));

    canvas.addEventListener('mousemove', (e) => {
        const rect = canvas.getBoundingClientRect();
        mouseX = e.clientX - rect.left;
        mouseY = e.clientY - rect.top;
    });
    canvas.addEventListener('mousedown', (e) => mouseButtons.add(e.button));
    window.addEventListener('mouseup', (e) => mouseButtons.delete(e.button));
    canvas.addEventListener('wheel', (e) => {
        scrollDelta += e.deltaY;
        e.preventDefault();
    }, { passive: false });
}

/**
 * Clears the canvas and establishes the NDC-to-pixel base transform.
 * After this call the canvas coordinate system maps NDC [-1, 1] to pixels,
 * with the Y-axis pointing upward (matching OpenGL conventions).
 */
export function clearFrame() {
    if (!ctx) return;
    ctx.resetTransform();
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    const hw = canvas.width / 2;
    const hh = canvas.height / 2;
    // NDC (0, 0) → canvas centre; NDC y increases upward; canvas y increases downward.
    ctx.setTransform(hw, 0, 0, -hh, hw, hh);
}

/**
 * Draws a unit quad [-0.5, 0.5]^2 using the given 2-D affine model matrix and RGBA fill color.
 *
 * The six matrix parameters map directly from System.Numerics.Matrix4x4 (row-major):
 *   Canvas ctx.transform(a, b, c, d, e, f) where a=m11, b=m12, c=m21, d=m22, e=tx, f=ty.
 */
export function drawQuad(m11, m12, m21, m22, tx, ty, r, g, b, a) {
    if (!ctx) return;
    ctx.save();
    ctx.transform(m11, m12, m21, m22, tx, ty);
    ctx.fillStyle = `rgba(${Math.round(r * 255)},${Math.round(g * 255)},${Math.round(b * 255)},${a})`;
    ctx.fillRect(-0.5, -0.5, 1, 1);
    ctx.restore();
}

export function isKeyPressed(key) {
    return pressedKeys.has(key);
}

export function isMouseButtonPressed(button) {
    return mouseButtons.has(button);
}

export function getMouseX() {
    return mouseX;
}

export function getMouseY() {
    return mouseY;
}

export function getMouseXNdc() {
    if (!canvas || canvas.width === 0) return 0;
    return (mouseX / canvas.width) * 2 - 1;
}

export function getMouseYNdc() {
    if (!canvas || canvas.height === 0) return 0;
    return 1 - (mouseY / canvas.height) * 2;
}

export function getScrollDelta() {
    return scrollDelta;
}

export function resetScrollDelta() {
    scrollDelta = 0;
}
