// The socket bytes of an item as the desktop client reads them (CItemMng::CreateItem, UI/Inventory/ItemMng.cpp):
// 0xFE is an open, empty socket ("No item application"), 0xFF is no socket at all, and the item's sockets end at the
// first 0xFF. Any other byte is a seed sphere: seed = byte % 50, sphere level = byte / 50 + 1.

/** SOCKET_EMPTY (Core/Globals/_define.h): an open socket without a seed sphere. */
export const SOCKET_EMPTY = 0xfe;
/** No socket in this slot (the server's ItemSerializerHelper.NoSocket). */
export const NO_SOCKET = 0xff;

/** The item's sockets: the bytes before the first 0xFF. */
export function socketBytes(bytes: readonly number[]): number[] {
  const end = bytes.indexOf(NO_SOCKET);
  return end < 0 ? [...bytes] : bytes.slice(0, end);
}
