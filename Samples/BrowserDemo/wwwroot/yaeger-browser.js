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
let activePrimaryPointerId;
const PIXELS_PER_LINE = 16;
const WHEEL_EVENT_OPTIONS = { passive: false };
const POINTER_EVENT_OPTIONS = { passive: false };

let resizeCanvasHandler;
let keyDownHandler;
let keyUpHandler;
let pointerDownHandler;
let pointerMoveHandler;
let pointerUpHandler;
let pointerCancelHandler;
let wheelHandler;
let blurHandler;
let contextMenuHandler;

function clearInputState() {
    pressedKeys.clear();
    mouseButtons.clear();
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

/**
 * Initialises the 2D rendering context and input listeners for the given canvas element.
 * Must be called once before any other export.
 */
export function initCanvas(canvasId) {
    disposeCanvas();

    canvas = document.getElementById(canvasId);
    if (!canvas) {
        throw new Error(`[Yaeger] Canvas element '#${canvasId}' not found.`);
    }
    ctx = canvas.getContext('2d');
    if (!ctx) {
        throw new Error(`[Yaeger] Failed to acquire 2D context for canvas '#${canvasId}'.`);
    }
    canvas.style.touchAction = 'none';

    resizeCanvasHandler = () => {
        if (!canvas) {
            return;
        }

        const dpr = window.devicePixelRatio || 1;
        canvas.width = Math.round(canvas.clientWidth * dpr);
        canvas.height = Math.round(canvas.clientHeight * dpr);
    };

    keyDownHandler = (e) => {
        if (e.code) {
            pressedKeys.add(e.code);
        }
    };
    keyUpHandler = (e) => {
        if (e.code) {
            pressedKeys.delete(e.code);
        }
    };

    pointerMoveHandler = (e) => {
        if (!canvas) {
            return;
        }

        if (e.pointerType !== 'mouse' && e.pointerId !== activePrimaryPointerId) {
            return;
        }

        const rect = canvas.getBoundingClientRect();
        mouseX = e.clientX - rect.left;
        mouseY = e.clientY - rect.top;

        if (e.pointerType !== 'mouse') {
            e.preventDefault();
        }
    };

    pointerDownHandler = (e) => {
        if (!canvas) {
            return;
        }

        if (e.pointerType === 'mouse') {
            const rect = canvas.getBoundingClientRect();
            mouseX = e.clientX - rect.left;
            mouseY = e.clientY - rect.top;
            mouseButtons.add(e.button);
            return;
        }

        if (activePrimaryPointerId === undefined) {
            activePrimaryPointerId = e.pointerId;
        }

        if (e.pointerId !== activePrimaryPointerId) {
            return;
        }

        const rect = canvas.getBoundingClientRect();
        mouseX = e.clientX - rect.left;
        mouseY = e.clientY - rect.top;
        mouseButtons.add(0);
        if (canvas.setPointerCapture) {
            canvas.setPointerCapture(e.pointerId);
        }
        e.preventDefault();
    };

    pointerUpHandler = (e) => {
        if (!canvas) {
            return;
        }

        if (e.pointerType === 'mouse') {
            mouseButtons.delete(e.button);
            return;
        }

        if (e.pointerId !== activePrimaryPointerId) {
            return;
        }

        mouseButtons.delete(0);
        activePrimaryPointerId = undefined;
        if (canvas.hasPointerCapture?.(e.pointerId)) {
            canvas.releasePointerCapture(e.pointerId);
        }
        e.preventDefault();
    };

    pointerCancelHandler = (e) => {
        if (!canvas || e.pointerType === 'mouse' || e.pointerId !== activePrimaryPointerId) {
            return;
        }

        mouseButtons.delete(0);
        activePrimaryPointerId = undefined;
        if (canvas.hasPointerCapture?.(e.pointerId)) {
            canvas.releasePointerCapture(e.pointerId);
        }
        e.preventDefault();
    };

    wheelHandler = (e) => {
        scrollDelta += normalizeWheelDeltaToPixels(e);
        e.preventDefault();
    };
    blurHandler = () => clearInputState();
    contextMenuHandler = (e) => e.preventDefault();

    resizeCanvasHandler();
    window.addEventListener('resize', resizeCanvasHandler);
    window.addEventListener('keydown', keyDownHandler);
    window.addEventListener('keyup', keyUpHandler);
    canvas.addEventListener('pointermove', pointerMoveHandler, POINTER_EVENT_OPTIONS);
    canvas.addEventListener('pointerdown', pointerDownHandler, POINTER_EVENT_OPTIONS);
    window.addEventListener('pointerup', pointerUpHandler, POINTER_EVENT_OPTIONS);
    window.addEventListener('pointercancel', pointerCancelHandler, POINTER_EVENT_OPTIONS);
    canvas.addEventListener('wheel', wheelHandler, WHEEL_EVENT_OPTIONS);
    window.addEventListener('blur', blurHandler);
    canvas.addEventListener('contextmenu', contextMenuHandler);
}

/**
 * Removes canvas/input event listeners and clears browser-side input state.
 */
export function disposeCanvas() {
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

    clearInputState();
    mouseX = 0;
    mouseY = 0;
    ctx = undefined;
    canvas = undefined;
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
