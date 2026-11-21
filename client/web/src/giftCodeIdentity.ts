/** Bilingual identity-limit messages shared by the game and account page, outside the general dictionaries. */
export function giftCodeIdentityText(result: string | undefined, language: string): string | null {
  const vi = language === 'vi';
  switch (result) {
    case 'AlreadyUsedNetwork': return vi ? 'Gift code này đã được dùng từ mạng này.' : 'This code was already used from this network.';
    case 'AlreadyUsedDevice': return vi ? 'Gift code này đã được dùng trên thiết bị này.' : 'This code was already used on this device.';
    case 'NetworkAddressUnknown': return vi ? 'Chưa xác định được địa chỉ mạng. Hãy kết nối lại rồi thử lại.' : 'Your network address is unavailable. Reconnect and try again.';
    default: return null;
  }
}
