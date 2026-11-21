/**
 * Makes a text safe inside an HTML template. Everything which comes from a player, the server or the admin
 * panel goes through this before it's put into `innerHTML`.
 */
export function escapeHtml(text: unknown): string {
  return String(text ?? '').replace(/[&<>"']/g, (c) => `&#${c.charCodeAt(0)};`);
}
