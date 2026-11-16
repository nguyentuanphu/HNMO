// Local EMVCo / NAPAS 247 VietQR payload + Byte-mode QR code SVG generator (no external service).

export interface VietQrInput {
  bin: string;
  account: string;
  accountName?: string;
  amount?: number;
  message?: string;
}

function tlv(tag: string, value: string): string {
  return `${tag}${String(value.length).padStart(2, '0')}${value}`;
}

function sanitizeAscii(text: string): string {
  return text
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .replace(/[^\x20-\x7E]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

/** Computes EMVCo CRC-16/CCITT-FALSE checksum (polynomial 0x1021, initial value 0xFFFF). */
export function crc16CcittFalse(text: string): string {
  let crc = 0xffff;
  for (let i = 0; i < text.length; i++) {
    crc ^= text.charCodeAt(i) << 8;
    for (let bit = 0; bit < 8; bit++) {
      crc = (crc & 0x8000) !== 0 ? ((crc << 1) ^ 0x1021) & 0xffff : (crc << 1) & 0xffff;
    }
  }
  return crc.toString(16).toUpperCase().padStart(4, '0');
}

/** Builds a NAPAS 247 VietQR EMVCo string. */
export function buildVietQrPayload(input: VietQrInput): string {
  const bin = input.bin.trim();
  const account = input.account.trim();
  const beneficiary = tlv('00', bin) + tlv('01', account);
  const merchantInfo = tlv('00', 'A000000727') + tlv('01', beneficiary) + tlv('02', 'QRIBFTTA');
  const amount = Math.max(0, Math.floor(input.amount ?? 0));
  const initiation = amount > 0 ? '12' : '11';

  let payload = tlv('00', '01')
    + tlv('01', initiation)
    + tlv('38', merchantInfo)
    + tlv('53', '704');

  if (amount > 0) {
    payload += tlv('54', String(amount));
  }

  payload += tlv('58', 'VN');

  const name = sanitizeAscii(input.accountName ?? '').slice(0, 25);
  if (name.length > 0) {
    payload += tlv('59', name);
  }

  const msg = sanitizeAscii(input.message ?? '').slice(0, 50);
  if (msg.length > 0) {
    payload += tlv('62', tlv('08', msg));
  }

  const unsigned = payload + '6304';
  return unsigned + crc16CcittFalse(unsigned);
}

// ---------- Compact QR Code Encoder (Versions 1..10, Error Correction M, Byte mode) ----------

interface QrVersionSpec {
  version: number;
  size: number;
  dataCodewords: number;
  eccPerBlock: number;
  blocks: number;
  alignment: readonly number[];
}

// QR ECC Level M tables for Versions 1..10 (capacity up to 213 bytes — VietQR is typically 70..130 bytes).
const QR_VERSIONS: readonly QrVersionSpec[] = [
  { version: 1, size: 21, dataCodewords: 16, eccPerBlock: 10, blocks: 1, alignment: [] },
  { version: 2, size: 25, dataCodewords: 28, eccPerBlock: 16, blocks: 1, alignment: [6, 18] },
  { version: 3, size: 29, dataCodewords: 44, eccPerBlock: 26, blocks: 1, alignment: [6, 22] },
  { version: 4, size: 33, dataCodewords: 64, eccPerBlock: 18, blocks: 2, alignment: [6, 26] },
  { version: 5, size: 37, dataCodewords: 86, eccPerBlock: 24, blocks: 2, alignment: [6, 30] },
  { version: 6, size: 41, dataCodewords: 108, eccPerBlock: 16, blocks: 4, alignment: [6, 34] },
  { version: 7, size: 45, dataCodewords: 124, eccPerBlock: 18, blocks: 4, alignment: [6, 22, 38] },
  { version: 8, size: 49, dataCodewords: 154, eccPerBlock: 22, blocks: 4, alignment: [6, 24, 42] },
  { version: 9, size: 53, dataCodewords: 182, eccPerBlock: 22, blocks: 5, alignment: [6, 26, 46] },
  { version: 10, size: 57, dataCodewords: 216, eccPerBlock: 26, blocks: 5, alignment: [6, 28, 50] },
];

const GF_EXP = new Uint8Array(512);
const GF_LOG = new Uint8Array(256);
(() => {
  let x = 1;
  for (let i = 0; i < 255; i++) {
    GF_EXP[i] = x;
    GF_LOG[x] = i;
    x <<= 1;
    if ((x & 0x100) !== 0) x ^= 0x11d;
  }
  for (let i = 255; i < 512; i++) GF_EXP[i] = GF_EXP[i - 255];
})();

function gfMul(a: number, b: number): number {
  return a === 0 || b === 0 ? 0 : GF_EXP[GF_LOG[a] + GF_LOG[b]];
}

function rsGeneratorPoly(degree: number): Uint8Array {
  let poly = new Uint8Array([1]);
  for (let i = 0; i < degree; i++) {
    const next = new Uint8Array(poly.length + 1);
    const root = GF_EXP[i];
    for (let j = 0; j < poly.length; j++) {
      next[j] ^= poly[j];
      next[j + 1] ^= gfMul(poly[j], root);
    }
    poly = next;
  }
  return poly;
}

function rsRemainder(data: Uint8Array, eccLen: number): Uint8Array {
  const gen = rsGeneratorPoly(eccLen);
  const res = new Uint8Array(eccLen);
  for (let i = 0; i < data.length; i++) {
    const factor = data[i] ^ res[0];
    res.copyWithin(0, 1);
    res[eccLen - 1] = 0;
    for (let j = 0; j < eccLen; j++) {
      res[j] ^= gfMul(gen[j + 1], factor);
    }
  }
  return res;
}

function appendBits(bits: number[], value: number, len: number): void {
  for (let i = len - 1; i >= 0; i--) {
    bits.push((value >>> i) & 1);
  }
}

function pickVersion(byteLen: number): QrVersionSpec {
  for (const spec of QR_VERSIONS) {
    const charCountBits = spec.version <= 9 ? 8 : 16;
    const totalBits = 4 + charCountBits + byteLen * 8;
    if (totalBits <= spec.dataCodewords * 8) return spec;
  }
  return QR_VERSIONS[QR_VERSIONS.length - 1];
}

function buildCodewords(bytes: Uint8Array, spec: QrVersionSpec): Uint8Array {
  const bits: number[] = [];
  const charCountBits = spec.version <= 9 ? 8 : 16;
  appendBits(bits, 0b0100, 4); // Byte mode
  appendBits(bits, Math.min(bytes.length, spec.dataCodewords - 3), charCountBits);
  const maxBytes = Math.min(bytes.length, spec.dataCodewords - 3);
  for (let i = 0; i < maxBytes; i++) appendBits(bits, bytes[i], 8);

  const maxBits = spec.dataCodewords * 8;
  appendBits(bits, 0, Math.min(4, maxBits - bits.length));
  while (bits.length % 8 !== 0) bits.push(0);
  const padBytes = [0xec, 0x11];
  let padIdx = 0;
  while (bits.length < maxBits) {
    appendBits(bits, padBytes[padIdx % 2], 8);
    padIdx++;
  }

  const data = new Uint8Array(spec.dataCodewords);
  for (let i = 0; i < data.length; i++) {
    let b = 0;
    for (let j = 0; j < 8; j++) b = (b << 1) | bits[i * 8 + j];
    data[i] = b;
  }

  const blocks: Uint8Array[] = [];
  const eccBlocks: Uint8Array[] = [];
  const shortLen = Math.floor(spec.dataCodewords / spec.blocks);
  const extraBlocks = spec.dataCodewords % spec.blocks;
  const longStart = spec.blocks - extraBlocks;
  let offset = 0;
  for (let b = 0; b < spec.blocks; b++) {
    const len = b >= longStart ? shortLen + 1 : shortLen;
    const slice = data.subarray(offset, offset + len);
    blocks.push(slice);
    eccBlocks.push(rsRemainder(slice, spec.eccPerBlock));
    offset += len;
  }

  const out: number[] = [];
  const maxDataLen = shortLen + (extraBlocks > 0 ? 1 : 0);
  for (let i = 0; i < maxDataLen; i++) {
    for (const blk of blocks) {
      if (i < blk.length) out.push(blk[i]);
    }
  }
  for (let i = 0; i < spec.eccPerBlock; i++) {
    for (const ecc of eccBlocks) out.push(ecc[i]);
  }
  return new Uint8Array(out);
}

function drawFinder(modules: number[][], reserved: boolean[][], row: number, col: number): void {
  const size = modules.length;
  for (let r = -1; r <= 7; r++) {
    for (let c = -1; c <= 7; c++) {
      const rr = row + r;
      const cc = col + c;
      if (rr < 0 || rr >= size || cc < 0 || cc >= size) continue;
      reserved[rr][cc] = true;
      const inBox = r >= 0 && r <= 6 && c >= 0 && c <= 6;
      const border = r === 0 || r === 6 || c === 0 || c === 6;
      const center = r >= 2 && r <= 4 && c >= 2 && c <= 4;
      modules[rr][cc] = inBox && (border || center) ? 1 : 0;
    }
  }
}

function drawAlignment(modules: number[][], reserved: boolean[][], cr: number, cc: number): void {
  for (let r = -2; r <= 2; r++) {
    for (let c = -2; c <= 2; c++) {
      const rr = cr + r;
      const cc2 = cc + c;
      reserved[rr][cc2] = true;
      modules[rr][cc2] = Math.abs(r) === 2 || Math.abs(c) === 2 || (r === 0 && c === 0) ? 1 : 0;
    }
  }
}

function formatBits(mask: number): number {
  const data = (0b00 << 3) | mask; // ECC level M = 00
  let rem = data << 10;
  for (let i = 14; i >= 10; i--) {
    if (((rem >>> i) & 1) !== 0) rem ^= 0x537 << (i - 10);
  }
  return ((data << 10) | rem) ^ 0x5412;
}

function writeFormat(modules: number[][], mask: number): void {
  const size = modules.length;
  const bits = formatBits(mask);
  for (let i = 0; i < 15; i++) {
    const bit = (bits >>> i) & 1;
    if (i < 6) modules[8][i] = bit;
    else if (i < 8) modules[8][i + 1] = bit;
    else modules[14 - i][8] = bit;

    if (i < 8) modules[size - 1 - i][8] = bit;
    else if (i < 9) modules[8][size - 8] = bit;
    else modules[8][size - 15 + i] = bit;
  }
  modules[size - 8][8] = 1;
}

function maskBit(mask: number, r: number, c: number): boolean {
  switch (mask) {
    case 0: return (r + c) % 2 === 0;
    case 1: return r % 2 === 0;
    case 2: return c % 3 === 0;
    case 3: return (r + c) % 3 === 0;
    case 4: return (Math.floor(r / 2) + Math.floor(c / 3)) % 2 === 0;
    case 5: return ((r * c) % 2) + ((r * c) % 3) === 0;
    case 6: return (((r * c) % 2) + ((r * c) % 3)) % 2 === 0;
    default: return (((r + c) % 2) + ((r * c) % 3)) % 2 === 0;
  }
}

function penaltyScore(modules: number[][]): number {
  const n = modules.length;
  let score = 0;
  for (let r = 0; r < n; r++) {
    let run = 1;
    for (let c = 1; c < n; c++) {
      if (modules[r][c] === modules[r][c - 1]) {
        run++;
        if (run === 5) score += 3;
        else if (run > 5) score += 1;
      } else run = 1;
    }
  }
  for (let c = 0; c < n; c++) {
    let run = 1;
    for (let r = 1; r < n; r++) {
      if (modules[r][c] === modules[r - 1][c]) {
        run++;
        if (run === 5) score += 3;
        else if (run > 5) score += 1;
      } else run = 1;
    }
  }
  for (let r = 0; r < n - 1; r++) {
    for (let c = 0; c < n - 1; c++) {
      const v = modules[r][c];
      if (v === modules[r][c + 1] && v === modules[r + 1][c] && v === modules[r + 1][c + 1]) score += 3;
    }
  }
  return score;
}

/** Encodes text into a square 0/1 module matrix. */
export function encodeQrMatrix(text: string): number[][] {
  const bytes = new TextEncoder().encode(text);
  const spec = pickVersion(bytes.length);
  const size = spec.size;
  const codewords = buildCodewords(bytes, spec);

  const base = Array.from({ length: size }, () => new Array<number>(size).fill(0));
  const reserved = Array.from({ length: size }, () => new Array<boolean>(size).fill(false));

  drawFinder(base, reserved, 0, 0);
  drawFinder(base, reserved, 0, size - 7);
  drawFinder(base, reserved, size - 7, 0);

  for (const r of spec.alignment) {
    for (const c of spec.alignment) {
      if (reserved[r][c]) continue;
      drawAlignment(base, reserved, r, c);
    }
  }

  for (let i = 8; i < size - 8; i++) {
    reserved[6][i] = reserved[i][6] = true;
    base[6][i] = base[i][6] = i % 2 === 0 ? 1 : 0;
  }

  // Reserve format & version areas.
  for (let i = 0; i < 9; i++) {
    if (i < size) {
      reserved[8][i] = true;
      reserved[i][8] = true;
    }
  }
  for (let i = 0; i < 8; i++) {
    reserved[8][size - 1 - i] = true;
    reserved[size - 1 - i][8] = true;
  }
  reserved[size - 8][8] = true;
  if (spec.version >= 7) {
    for (let r = 0; r < 6; r++) {
      for (let c = 0; c < 3; c++) {
        reserved[r][size - 11 + c] = true;
        reserved[size - 11 + c][r] = true;
      }
    }
  }

  // Place data bits in zig-zag columns.
  let bitIndex = 0;
  const totalBits = codewords.length * 8;
  let upward = true;
  for (let right = size - 1; right > 0; right -= 2) {
    if (right === 6) right = 5;
    for (let step = 0; step < size; step++) {
      const r = upward ? size - 1 - step : step;
      for (let dx = 0; dx < 2; dx++) {
        const c = right - dx;
        if (reserved[r][c]) continue;
        const bit = bitIndex < totalBits ? (codewords[bitIndex >>> 3] >>> (7 - (bitIndex & 7))) & 1 : 0;
        base[r][c] = bit;
        bitIndex++;
      }
    }
    upward = !upward;
  }

  let bestMask = 0;
  let bestScore = Infinity;
  let bestGrid = base;
  for (let mask = 0; mask < 8; mask++) {
    const candidate = base.map((row, r) => row.map((v, c) => (reserved[r][c] ? v : v ^ (maskBit(mask, r, c) ? 1 : 0))));
    writeFormat(candidate, mask);
    const score = penaltyScore(candidate);
    if (score < bestScore) {
      bestScore = score;
      bestMask = mask;
      bestGrid = candidate;
    }
  }
  void bestMask;
  return bestGrid;
}

/** Renders a VietQR EMVCo string as a local inline SVG data URI (no external HTTP calls). */
export function renderVietQrSvgDataUrl(payload: string): string {
  const grid = encodeQrMatrix(payload);
  const n = grid.length;
  const quiet = 3;
  const total = n + quiet * 2;
  let path = '';
  for (let r = 0; r < n; r++) {
    for (let c = 0; c < n; c++) {
      if (grid[r][c] === 1) path += `M${c + quiet},${r + quiet}h1v1h-1z`;
    }
  }
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${total} ${total}" shape-rendering="crispEdges"><rect width="${total}" height="${total}" fill="#fff"/><path d="${path}" fill="#000"/></svg>`;
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
}
