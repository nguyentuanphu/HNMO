// Boxes of page elements that only move when the window changes size (the game's canvas, the HUD strip),
// read once per size: reading a box in the frame loop after other writes makes the browser lay the
// whole page out again every frame.

const boxes = new Map<Element, DOMRect>();
let listening = false;

function forget(): void { boxes.clear(); }

/** The element's box (getBoundingClientRect), kept until the window's size or full screen changes (or a UI size change, which sends a resize). */
export function cachedRect(element: Element): DOMRect {
  if (!listening) {
    listening = true;
    window.addEventListener('resize', forget);
    document.addEventListener('fullscreenchange', forget);
  }
  let box = boxes.get(element);
  if (!box) {
    box = element.getBoundingClientRect();
    if (box.width > 0 && box.height > 0) boxes.set(element, box); // not laid out yet: asked again next time
  }
  return box;
}

/** Forgets the boxes (something moved without a resize). */
export function forgetRects(): void { forget(); }
