// The (i) button: the HNMO guide (public/guide.html) over the game — what to play and what each
// feature does. An HNMO extra; the page loads on first open.

export function setUpGuide(): void {
  const button = document.getElementById('guideButton');
  const overlay = document.getElementById('guide');
  const frame = overlay?.querySelector('iframe');
  const close = overlay?.querySelector<HTMLButtonElement>('.close');
  if (!button || !overlay || !frame || !close) return;
  const hide = () => { overlay.hidden = true; };
  const toggle = () => {
    if (frame.getAttribute('src') !== 'guide.html') frame.setAttribute('src', 'guide.html');
    overlay.hidden = !overlay.hidden;
  };
  button.addEventListener('click', toggle);
  // The login screen links to it too.
  document.getElementById('loginGuide')?.addEventListener('click', (event) => { event.preventDefault(); toggle(); });
  close.addEventListener('click', hide);
  overlay.addEventListener('click', (event) => { if (event.target === overlay) hide(); });
  window.addEventListener('keydown', (event) => { if (event.key === 'Escape' && !overlay.hidden) hide(); });
  // Keys pressed while the guide is open stay in the guide.
  button.addEventListener('pointerdown', (event) => event.stopPropagation());
  overlay.addEventListener('pointerdown', (event) => event.stopPropagation());
}
