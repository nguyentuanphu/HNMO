// The public account page and game share this origin's browser identity. It is not an account credential.
const STORAGE_KEY = 'hnmo.deviceId';
let memoryId: string | null = null;
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

/** A random browser id retained across reloads, or unknown when secure UUID creation/storage is unavailable. */
export function deviceId(): string {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved && UUID.test(saved)) return saved;
    memoryId ??= crypto.randomUUID();
    localStorage.setItem(STORAGE_KEY, memoryId);
    return memoryId;
  } catch {
    // Never claim a newly randomized id as persistent when storage is blocked.
    return '';
  }
}
