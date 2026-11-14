// The cash shop's script (Data/InGameShopScript/<zone>.<year>.<id>: IBSCategory.txt, IBSPackage.txt,
// IBSProduct.txt) and what the desktop's CInGameShopSystem makes of it (GameShop/InGameShopSystem.cpp,
// ShopListManager/*): the sales zones, the categories of a zone, the packages of a category paged by 9,
// and the product details (name, quantity, duration, price, item) of a package's price options.

/** CShopCategory: a sales zone (root) or a category of one. */
export interface ShopCategory {
  seq: number; name: string; eventFlag: number; openFlag: number; parentSeq: number; displayOrder: number; root: boolean;
  /** The categories of a zone, in the script's order. */
  children: number[];
  /** The packages of a category, in the script's order. */
  packages: number[];
}

/** CShopPackage. */
export interface ShopPackage {
  displaySeq: number; viewOrder: number; seq: number; name: string; type: number; price: number; description: string; caution: string;
  salesFlag: number; giftFlag: number; capsuleFlag: number; capsuleCount: number; cashName: string; priceUnit: string;
  deleteFlag: number; eventFlag: number; productAmount: number; productSeqs: number[]; itemCode: number; productCashSeq: number;
  priceCount: number; priceSeqs: number[]; deductMileage: boolean; cashType: number; cashTypeFlag: number;
}

/** CShopProduct: one property of a product's price option. */
export interface ShopProduct {
  seq: number; name: string; propertyName: string; value: string; unitName: string; price: number; priceSeq: number;
  propertyType: number; mustFlag: number; order: number; deleteFlag: number; storageGroup: number; shareFlag: number;
  itemCode: number; propertySeq: number; productType: number; unitType: number;
}

/** INGAMESHOP_DISPLAY_ITEMLIST_SIZE: the packages on a page (3 x 3). */
export const PACKAGES_PER_PAGE = 9;
/** The GiftFlag of packages that may be sent as a gift. */
export const GIFT_ENABLED = 184;

const int = (text: string | undefined): number => { const n = parseInt(text ?? '', 10); return Number.isFinite(n) ? n : 0; };
const seqList = (text: string | undefined): number[] => (text ?? '').split('|').filter((s) => s.trim() !== '').map(int);
const lines = (text: string): string[] => text.split(/\r?\n/).filter((line) => line.trim() !== '');

/** The texts the product details use (I18N::Game). */
export const UNIT = { day: 'Day', hour: 'Hour', minute: 'Minute', second: 'Second' } as const;

/** What GetProductInfo* answers: a value and a text (a unit or a name). */
export interface ProductInfo { value: number; text: string }

export class CashShopScript {
  /** By seq, iterated by seq (a std::map). */
  readonly categories = new Map<number, ShopCategory>();
  readonly packages = new Map<number, ShopPackage>();
  /** By product seq, in the script's order (a std::multimap). */
  readonly products = new Map<number, ShopProduct[]>();

  static parse(categoryText: string, packageText: string, productText: string): CashShopScript {
    const script = new CashShopScript();
    for (const line of lines(categoryText)) {
      const t = line.split('@');
      const category: ShopCategory = {
        seq: int(t[0]), name: t[1] ?? '', eventFlag: int(t[2]), openFlag: int(t[3]), parentSeq: int(t[4]), displayOrder: int(t[5]),
        root: int(t[6]) === 1, children: [], packages: [],
      };
      // CShopCategoryList::Append: the first one of a seq stays; a category goes under its parent if that came before.
      if (script.categories.has(category.seq)) continue;
      script.categories.set(category.seq, category);
      if (!category.root) script.categories.get(category.parentSeq)?.children.push(category.seq);
    }
    for (const line of lines(packageText)) {
      const t = line.split('@');
      const pack: ShopPackage = {
        displaySeq: int(t[0]), viewOrder: int(t[1]), seq: int(t[2]), name: t[3] ?? '', type: int(t[4]), price: int(t[5]),
        description: t[6] ?? '', caution: t[7] ?? '', salesFlag: int(t[8]), giftFlag: int(t[9]), capsuleFlag: int(t[12]),
        capsuleCount: int(t[13]), cashName: t[14] ?? '', priceUnit: t[15] ?? '', deleteFlag: int(t[16]), eventFlag: int(t[17]),
        productAmount: int(t[18]), productSeqs: seqList(t[19]), itemCode: int(t[20]), productCashSeq: int(t[21]), priceCount: int(t[22]),
        priceSeqs: seqList(t[23]), deductMileage: int(t[24]) !== 0, cashType: int(t[25]), cashTypeFlag: int(t[26]),
      };
      if (!script.packages.has(pack.seq)) script.packages.set(pack.seq, pack);
      script.categories.get(pack.displaySeq)?.packages.push(pack.seq);
    }
    for (const line of lines(productText)) {
      const t = line.split('@');
      const product: ShopProduct = {
        seq: int(t[0]), name: t[1] ?? '', propertyName: t[2] ?? '', value: t[3] ?? '', unitName: t[4] ?? '', price: int(t[5]),
        priceSeq: int(t[6]), propertyType: int(t[7]), mustFlag: int(t[8]), order: int(t[9]), deleteFlag: int(t[10]),
        storageGroup: int(t[11]), shareFlag: int(t[12]), itemCode: int(t[13]), propertySeq: int(t[14]), productType: int(t[15]), unitType: int(t[16]),
      };
      const list = script.products.get(product.seq) ?? [];
      list.push(product);
      script.products.set(product.seq, list);
    }
    return script;
  }

  /** InitZoneInfo: the root categories by seq. */
  zones(): ShopCategory[] {
    return [...this.categories.values()].sort((a, b) => a.seq - b.seq).filter((c) => c.root);
  }

  categoriesOf(zone: ShopCategory | null): ShopCategory[] {
    return (zone?.children ?? []).map((seq) => this.categories.get(seq)).filter((c): c is ShopCategory => Boolean(c));
  }

  /** SetNormalPackage: a category's packages (stopping at the first one the script lacks). */
  packagesOf(category: ShopCategory | null): ShopPackage[] {
    const result: ShopPackage[] = [];
    for (const seq of category?.packages ?? []) {
      const pack = this.packages.get(seq);
      if (!pack) break;
      result.push(pack);
    }
    return result;
  }

  /** GetProductInfoFromPriceSeq. */
  infoByPrice(productSeq: number, priceSeq: number, attribute: ProductAttribute): ProductInfo | null {
    for (const product of this.products.get(productSeq) ?? []) {
      if (product.priceSeq !== priceSeq) continue;
      const info = productInfo(product, attribute);
      if (info) return info;
    }
    return null;
  }

  /** GetProductInfoFromProductSeq. */
  infoByProduct(productSeq: number, attribute: ProductAttribute): ProductInfo | null {
    for (const product of this.products.get(productSeq) ?? []) {
      const info = productInfo(product, attribute);
      if (info) return info;
    }
    return null;
  }
}

export type ProductAttribute = 'period' | 'available' | 'count' | 'price' | 'itemCode' | 'name' | 'priceSeq';

// CInGameShopSystem::GetProductInfo: the property rows each attribute is read from.
function productInfo(product: ShopProduct, attribute: ProductAttribute): ProductInfo | null {
  const p = product.propertySeq;
  switch (attribute) {
    case 'period': {
      if (![2, 28, 12, 58, 10].includes(p)) return null;
      let value = int(product.value);
      let text: string = product.unitName;
      if (product.unitType === 386) {
        if (value >= 86400) { value = Math.floor(value / 86400); text = UNIT.day; }
        else if (value >= 3600) { value = Math.floor(value / 3600); text = UNIT.hour; }
        else if (value >= 60) { value = Math.floor(value / 60); text = UNIT.minute; }
        else text = UNIT.second;
      } else if (product.unitType === 174) {
        if (value >= 1440) { value = Math.floor(value / 1440); text = UNIT.day; }
        else if (value >= 60) { value = Math.floor(value / 60); text = UNIT.hour; }
        else text = UNIT.minute;
      } else if (product.unitType === 172) {
        if (value >= 24) { value = Math.floor(value / 24); text = UNIT.day; } else text = UNIT.hour;
      }
      return { value, text };
    }
    case 'available':
      return [46, 49, 48, 51, 52, 53, 50, 60].includes(p) ? { value: int(product.value), text: product.unitName } : null;
    case 'count':
      return [30, 11, 7, 8, 9, 31].includes(p) ? { value: int(product.value), text: product.unitName } : null;
    case 'price': return { value: product.price, text: convertGold(product.price) };
    case 'itemCode': return { value: product.itemCode, text: '' };
    case 'name': return { value: -1, text: product.name };
    case 'priceSeq': return { value: product.priceSeq, text: '' };
  }
}

/** ConvertGold: thousands with commas, and the given number of decimals. */
export function convertGold(amount: number, decimals = 0): string {
  const whole = Math.max(0, Math.floor(amount));
  let text = whole.toLocaleString('en-US');
  if (decimals > 0) text += `.${Math.floor(amount * 10 ** decimals) % 10 ** decimals}`;
  return text;
}

/** The script's folder of a version (ShopListManager: "<zone>.<year>.<id with three digits>"). */
export function scriptFolder(zone: number, year: number, id: number): string {
  return `${zone}.${year}.${String(id).padStart(3, '0')}`;
}

const scripts = new Map<string, Promise<CashShopScript>>();

/** Loads a script version from the game data (the desktop downloads it into the same folder). */
export function loadCashShopScript(folder: string): Promise<CashShopScript> {
  if (!scripts.has(folder)) {
    const text = async (name: string) => {
      const response = await fetch(`/data/InGameShopScript/${folder}/${name}`);
      if (!response.ok) throw new Error(`${name}: ${response.status}`);
      return new TextDecoder('windows-1252').decode(await response.arrayBuffer());
    };
    scripts.set(folder, Promise.all([text('IBSCategory.txt'), text('IBSPackage.txt'), text('IBSProduct.txt')])
      .then(([c, p, r]) => CashShopScript.parse(c, p, r)));
    scripts.get(folder)!.catch(() => scripts.delete(folder));
  }
  return scripts.get(folder)!;
}

/** The banner of a banner version (IBSBanner.txt: the image's file name from its address). */
export async function loadCashShopBanner(folder: string): Promise<string | null> {
  const response = await fetch(`/data/InGameShopBanner/${folder}/IBSBanner.txt`);
  if (!response.ok) return null;
  const line = lines(new TextDecoder('windows-1252').decode(await response.arrayBuffer()))[0];
  const url = line?.split('@')[2] ?? '';
  const file = url.slice(url.lastIndexOf('/') + 1).replace(/\.jpg$/i, '.OZJ');
  return file ? `/data/InGameShopBanner/${folder}/${file}` : null;
}
