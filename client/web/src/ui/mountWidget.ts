// Self-contained Mount Chain collection widget for standalone embedding on ChatGPT's account page or home.html#mounts.
// Usage from account.html: mountCollectionWidget(containerEl, accountLoginName, 'en' | 'vi')

export interface PublicMountTier {
  id: string;
  nameEn: string;
  nameVi: string;
  shardsRequired: number;
  pveDefenseBonus: number;
  bonusExpPercent: number;
  bonusZenPercent: number;
  collectorsCount: number;
}

export interface MountWidgetPayload {
  enabled: boolean;
  accountShards?: number;
  accountActiveTierId?: string;
  accountUnlocked?: string[];
  publicState?: MountWidgetPayload;
  tiers?: PublicMountTier[];
}

export async function renderMountCollectionWidget(
  container: HTMLElement,
  login: string | null,
  lang: 'en' | 'vi' = 'en',
): Promise<void> {
  const qs = login ? `?login=${encodeURIComponent(login)}` : '';
  try {
    const res = await fetch(`/website/mounts${qs}`, { cache: 'no-store' });
    if (!res.ok) throw new Error('offline');
    const raw = (await res.json()) as MountWidgetPayload;
    const base = raw.publicState ?? raw;
    container.innerHTML = '';
    if (!raw.enabled) {
      container.textContent = lang === 'vi' ? 'Chuỗi thú cưỡi hiện đang tắt.' : 'Mount chain is currently disabled.';
      return;
    }

    const unlocked = new Set((raw.accountUnlocked ?? []).map((x) => x.toLowerCase()));
    if (typeof raw.accountShards === 'number') {
      const summary = document.createElement('p');
      summary.textContent = lang === 'vi'
        ? `Mảnh Thú Cưỡi hiện có: ${raw.accountShards} · Đã mở: ${unlocked.size}/${(base.tiers ?? []).length}`
        : `Mount Shards: ${raw.accountShards} · Unlocked: ${unlocked.size}/${(base.tiers ?? []).length}`;
      container.appendChild(summary);
    }

    const list = document.createElement('ul');
    for (const tier of base.tiers ?? []) {
      const li = document.createElement('li');
      const isOwned = unlocked.has(tier.id.toLowerCase());
      const badge = isOwned ? (lang === 'vi' ? ' [Đã sở hữu]' : ' [Unlocked]') : '';
      li.textContent = `${lang === 'vi' ? tier.nameVi : tier.nameEn}${badge} — +${tier.pveDefenseBonus} Def, +${tier.bonusExpPercent}% EXP (${tier.collectorsCount})`;
      list.appendChild(li);
    }
    container.appendChild(list);
  } catch {
    container.textContent = lang === 'vi' ? 'Không thể tải dữ liệu thú cưỡi.' : 'Unable to load mount collection.';
  }
}
