// HNMO's new-player guide in the web game and on the website. The guide panel (Y) shows the state the server sends
// (HnmoOnboardingState, C2 F5 0F): the current step, its hint, where to go (with an arrow on the screen), the count, the
// reward, the other steps and the waiting rewards; steps done, rewards and the starter kit are told in the chat; the
// buttons send HnmoOnboardingAction. The state is fed to the client (the test server may run without the guide), so
// what's checked is the panel, the arrow, the chat and the packets; the server's rules are covered by its unit tests
// (OnboardingEngineTest). The website's "New players" section reads /website/onboarding (answered here by the test).
import { connect } from 'node:net';
import { test, expect, type Page } from '@playwright/test';
import { enterWorld, expectNoPageErrors } from './helpers.js';

interface Sent { name: string; fields: Record<string, unknown> }
interface Panel { handle(bytes: Uint8Array): boolean; open(): void; close(): void; toggle(): void; start(): void; isOpen: boolean; arrowLine: HTMLDivElement }
type Win = Window & {
  __game: {
    connection: { send(group: string, name: string, fields?: Record<string, unknown>): void; dispatch(bytes: Uint8Array): void };
    onboarding?: Pick<Panel, 'isOpen' | 'handle'>;
  };
  __sent: Sent[];
  __chat: string[];
  __confirm: string[];
  __panel: Panel;
  __feedingGuide?: boolean;
};

const reward = (zen: number, coins = 0, items: { group: number; number: number; level: number; count: number }[] = []) => ({ zen, coins, coinType: 'GoblinPoints', items });
const STEPS = [
  { id: 'talk-merchant', kind: 'TalkToNpc', titleEn: 'Talk to a merchant', titleVi: 'Nói chuyện với một thương nhân', hintEn: 'Click an NPC in town.', hintVi: 'Bấm vào một NPC trong thành.', target: 1, optional: false, reward: reward(10000) },
  { id: 'kill-10', kind: 'KillMonsters', titleEn: 'Defeat 10 monsters', titleVi: 'Hạ gục 10 quái vật', hintEn: 'Click a monster to attack it.', hintVi: 'Bấm vào quái vật để tấn công.', target: 10, optional: false, reward: reward(20000, 0, [{ group: 14, number: 1, level: 0, count: 2 }]) },
  { id: 'join-party', kind: 'JoinParty', titleEn: 'Join a party', titleVi: 'Vào một tổ đội', hintEn: 'Press P.', hintVi: 'Nhấn P.', target: 1, optional: true, reward: reward(20000, 5) },
];

function state(current: string | null, options: { done?: string[]; value?: number; where?: Record<string, unknown>; event?: Record<string, unknown>; kit?: boolean; owed?: boolean; status?: string } = {}) {
  const done = options.done ?? [];
  const step = STEPS.find((s) => s.id === current);
  return {
    v: 1,
    status: options.status ?? 'active',
    steps: STEPS.map((s) => ({ ...s, done: done.includes(s.id), skipped: false })),
    current: step ? { id: step.id, value: options.value ?? 0, target: step.target, where: options.where } : undefined,
    done: done.length,
    total: STEPS.length,
    final: reward(200000, 50, [{ group: 14, number: 13, level: 0, count: 1 }]),
    owed: options.owed ? [{ kind: 'kit', titleEn: 'Starter kit', titleVi: 'Quà tân thủ', zen: 50000, coins: 0, coinType: 'GoblinPoints', items: [{ group: 0, number: 1, level: 3, count: 1 }], buffMinutes: 60 }] : [],
    kit: options.kit ? { perAccount: false, zen: 50000, coins: 20, coinType: 'GoblinPoints', points: 0, buffMinutes: 60, items: [{ group: 14, number: 1, level: 0, count: 3, durability: 3 }, { group: 0, number: 1, level: 3, count: 1 }] } : undefined,
    event: options.event,
  };
}

async function feed(page: Page, value: unknown): Promise<void> {
  await page.evaluate(async (json) => {
    const { write } = await import('/js/net/packets.js' as string) as { write(group: string, name: string, values: Record<string, unknown>): Uint8Array };
    const w = window as unknown as Win;
    const bytes = write('ServerToClient', 'HnmoOnboardingState', { Json: json });
    w.__feedingGuide = true;
    try {
      if (w.__panel) w.__panel.handle(bytes); else w.__game.connection.dispatch(bytes);
    } finally { w.__feedingGuide = false; }
  }, JSON.stringify(value));
}

const sent = (page: Page) => page.evaluate(() => (window as unknown as Win).__sent.filter((s) => s.name.startsWith('HnmoOnboarding')));
const chat = (page: Page) => page.evaluate(() => (window as unknown as Win).__chat);

function gameServerUp(): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = connect({ host: '127.0.0.1', port: 44406 }, () => { socket.destroy(); resolve(true); });
    socket.on('error', () => resolve(false));
    socket.setTimeout(2000, () => { socket.destroy(); resolve(false); });
  });
}

test('the guide panel: the step, the arrow, the chat notices and the buttons', async ({ page }) => {
  test.setTimeout(3 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('/index.html');
  await page.evaluate(async () => {
    const { OnboardingWindow } = await import('/js/ui/onboardingWindow.js' as string) as { OnboardingWindow: new (options: unknown) => Panel };
    const { itemName, loadItemCatalog } = await import('/js/game/items.js' as string) as { itemName(g: number, n: number): string; loadItemCatalog(): Promise<void> };
    await loadItemCatalog().catch(() => undefined);
    const w = window as unknown as Win;
    w.__sent = [];
    w.__chat = [];
    w.__confirm = [];
    w.__panel = new OnboardingWindow({
      send: (name: string, fields: Record<string, unknown>) => w.__sent.push({ name, fields: { ...fields } }),
      message: (text: string) => w.__chat.push(text),
      itemName: (g: number, n: number) => itemName(g, n),
      mapName: (map: number) => (map === 0 ? 'Lorencia' : `Map ${map}`),
      mapNumber: () => 0,
      // The target is up and to the right of the hero.
      direction: () => ({ angle: Math.PI / 4, steps: 23 }),
      confirm: async (text: string) => { w.__confirm.push(text); return true; },
    });
    w.__panel.start();
  });
  await expect.poll(() => sent(page).then((list) => list.map((s) => s.name))).toEqual(['HnmoOnboardingRequest']);

  // The first state of a new character: the kit is told, the panel opens by itself with the first step and where to go.
  await feed(page, state('talk-merchant', { kit: true, where: { map: 0, x: 127, y: 86, name: 'Potion Girl Amy' } }));
  const panel = page.locator('.rml-hnmo_onboarding');
  await expect(panel).toHaveCount(1);
  await expect.poll(() => chat(page).then((lines) => lines.find((l) => l.startsWith('Welcome! Your starter kit:')))).toMatch(/50,000 zen · 20 Goblin Points · an EXP buff for 60 minutes/);
  await expect.poll(() => chat(page)).toContain('[Guide] Next: Talk to a merchant. Click an NPC in town.');
  await expect.poll(() => chat(page)).toContain('[Guide] Potion Girl Amy: Lorencia (127, 86).');
  await expect(panel).toContainText('Step 1 of 3');
  await expect(panel.locator('.ob-current-title')).toHaveText('Talk to a merchant');
  await expect(panel).toContainText('Potion Girl Amy: Lorencia (127, 86).');
  await expect(panel).toContainText('Reward: 10,000 zen');
  await expect(panel.locator('.ob-step[data-step="join-party"]')).toContainText('Join a party (optional)');
  await expect(panel).toContainText('When every step is done: 200,000 zen · 50 Goblin Points');
  await expect(panel.locator('.ob-skip-step')).toBeHidden();
  const arrow = page.locator('.hnmo-guide-arrow');
  await expect(arrow).toBeVisible();
  await expect(arrow).toContainText('Potion Girl Amy · 23 steps');
  await expect(arrow.locator('.hnmo-guide-pointer')).toHaveAttribute('style', /rotate\(-45deg\)/);
  await page.screenshot({ path: 'test-results/onboarding-panel.png' });

  // The step is done: the chat tells it with its reward; the next step counts.
  await feed(page, state('kill-10', { done: ['talk-merchant'], value: 3, event: { type: 'step', result: 'Ok', ids: ['talk-merchant'], count: 1 } }));
  await expect.poll(() => chat(page)).toContain('[Guide] Step done: Talk to a merchant. Reward: 10,000 zen');
  await expect.poll(() => chat(page)).toContain('[Guide] Next: Defeat 10 monsters. Click a monster to attack it.');
  await expect(panel).toContainText('Step 2 of 3');
  await expect(panel).toContainText('3 / 10');
  await expect(panel.locator('.ob-step[data-step="talk-merchant"]')).toContainText('✓ Talk to a merchant');
  await expect(arrow).toBeHidden();

  // A full bag: the rewards wait; Take sends the action.
  await feed(page, state('join-party', { done: ['talk-merchant', 'kill-10'], owed: true, event: { type: 'step', result: 'BagFull', ids: ['kill-10'] } }));
  await expect.poll(() => chat(page).then((lines) => lines.some((l) => l.startsWith('[Guide] Your bag is full: the guide\'s rewards wait.')))).toBe(true);
  await expect(panel).toContainText('Starter kit: 50,000 zen');
  await panel.locator('.ob-claim').click();
  await expect.poll(() => sent(page).then((list) => list.find((s) => s.name === 'HnmoOnboardingAction')?.fields)).toEqual({ Action: 3, Argument: '' });

  // An optional step can be skipped alone; the whole guide after a confirmation.
  await expect(panel.locator('.ob-skip-step')).toBeVisible();
  await panel.locator('.ob-skip-step').click();
  await expect.poll(() => sent(page).then((list) => list.filter((s) => s.name === 'HnmoOnboardingAction').map((s) => s.fields.Action))).toEqual([3, 2]);
  await panel.locator('.ob-skip').click();
  await expect.poll(() => page.evaluate(() => (window as unknown as Win).__confirm.length)).toBe(1);
  await expect.poll(() => sent(page).then((list) => list.filter((s) => s.name === 'HnmoOnboardingAction').map((s) => s.fields.Action))).toEqual([3, 2, 1]);
  await feed(page, state(null, { status: 'skipped', done: ['talk-merchant', 'kill-10'], event: { type: 'skip', result: 'Ok' } }));
  await expect.poll(() => chat(page)).toContain('[Guide] The guide was skipped. You can still read it with Y.');
  await expect(panel.locator('.ob-status')).toHaveText('You skipped the new-player guide.');
  await expect(panel.locator('.ob-skip')).toBeHidden();

  // Vietnamese.
  await feed(page, state('kill-10', { done: ['talk-merchant'], value: 4 }));
  await page.evaluate(async () => {
    const { setLanguage } = await import('/js/i18n.js' as string) as { setLanguage(language: string): Promise<void> };
    await setLanguage('vi');
  });
  await expect(panel.locator('.ob-current-title')).toHaveText('Hạ gục 10 quái vật');
  await expect(panel).toContainText('Bước 2 / 3');
  await expect(panel).toContainText('Bấm vào quái vật để tấn công.');
  await feed(page, state('join-party', { done: ['talk-merchant', 'kill-10'], event: { type: 'step', result: 'Ok', ids: ['kill-10'], count: 1 } }));
  await expect.poll(() => chat(page).then((lines) => lines.some((l) => l.startsWith('[Hướng dẫn] Xong bước: Hạ gục 10 quái vật.')))).toBe(true);
  await page.screenshot({ path: 'test-results/onboarding-panel-vi.png' });
  await page.evaluate(async () => {
    const { setLanguage } = await import('/js/i18n.js' as string) as { setLanguage(language: string): Promise<void> };
    await setLanguage('en');
  });

  // The last step: the final reward.
  await feed(page, state(null, { status: 'done', done: ['talk-merchant', 'kill-10', 'join-party'], event: { type: 'final', result: 'Ok', ids: ['join-party'], count: 2 } }));
  await expect.poll(() => chat(page)).toContain('[Guide] You finished the new-player guide! Reward: 200,000 zen · 50 Goblin Points · Jewel of Bless x1');
  await expect(panel.locator('.ob-status')).toHaveText('You finished the new-player guide. Good hunting!');
  await page.keyboard.press('Escape');
  await expect(panel).toHaveCount(0);
  await expectNoPageErrors(errors);
});

// As with the gift-code fixture, recorded states must not be replaced by a live server response.
async function onlyFedGuideStates(page: Page): Promise<void> {
  await page.evaluate(() => {
    const w = window as unknown as Win;
    const panel = w.__game.onboarding;
    if (!panel) throw new Error('The game has not created its onboarding window.');
    const handle = panel.handle.bind(panel);
    panel.handle = (bytes: Uint8Array) => {
      if (bytes[0] === 0xc2 && bytes[3] === 0xf5 && bytes[4] === 0x0f && !w.__feedingGuide) return true;
      return handle(bytes);
    };
  });
}

test('in the game, the guide is asked for, Y opens it, and the (i) guide and the touch menu have it', async ({ page }) => {
  test.skip(!(await gameServerUp()), 'No game server runs on 44406.');
  test.setTimeout(6 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.addInitScript(() => {
    // Collect what the game sends from the start (it asks for the guide when it enters the world).
    const w = window as unknown as Win;
    w.__sent = [];
    const timer = setInterval(() => {
      const connection = w.__game?.connection;
      if (!connection || (connection as unknown as { __wrapped?: boolean }).__wrapped) return;
      const send = connection.send.bind(connection);
      connection.send = (group, name, fields) => { w.__sent.push({ name, fields: { ...(fields ?? {}) } }); send(group, name, fields); };
      (connection as unknown as { __wrapped?: boolean }).__wrapped = true;
      clearInterval(timer);
    }, 5);
  });
  await enterWorld(page, 'test8');
  await expect.poll(() => sent(page).then((list) => list.some((s) => s.name === 'HnmoOnboardingRequest')), { timeout: 60_000 }).toBe(true);
  await onlyFedGuideStates(page);
  const panel = page.locator('.rml-hnmo_onboarding');
  // A server without the guide doesn't answer: the state comes through the game's connection, and the panel opens by itself.
  await feed(page, state('talk-merchant', { where: { map: 0, x: 127, y: 86, name: 'Potion Girl Amy' } }));
  // The game's documents load slowly while the map loads.
  await expect(panel).toHaveCount(1, { timeout: 90_000 });
  await expect(panel.locator('.ob-current-title')).toHaveText('Talk to a merchant');
  await page.screenshot({ path: 'test-results/onboarding-game.png' });
  await page.keyboard.press('y');
  await expect(panel).toHaveCount(0);
  await page.keyboard.press('y');
  await expect(panel).toHaveCount(1, { timeout: 90_000 });
  await page.keyboard.press('y');
  await expect(panel).toHaveCount(0);
  // The (i) guide's button.
  await page.locator('#guideButton').click();
  await expect(page.locator('#guide')).toBeVisible();
  await page.locator('#guideOnboarding').click();
  await expect(page.locator('#guide')).toBeHidden();
  await expect(panel).toHaveCount(1, { timeout: 90_000 });
  await expect(page.locator('#touchMore button[title="New-player guide"]')).toHaveCount(1);
  await page.goto('about:blank');
  await expectNoPageErrors(errors);
});

const PUBLIC = () => ({
  v: 1, enabled: true, guideEnabled: true, newCharacterMaxLevel: 10,
  steps: STEPS.map(({ id, kind, titleEn, titleVi, hintEn, hintVi, target, optional, reward: r }) => ({ id, kind, titleEn, titleVi, hintEn, hintVi, target, optional, reward: { ...r, items: r.items.map((i) => ({ ...i, name: 'Small Healing Potion' })) } })),
  final: { zen: 200000, coins: 50, coinType: 'GoblinPoints', items: [{ group: 14, number: 13, level: 0, count: 1, name: 'Jewel of Bless' }] },
  kit: {
    perAccount: false, zen: 50000, coins: 20, coinType: 'GoblinPoints', points: 15, buffMinutes: 60, buffName: 'Seal of Ascension',
    items: [
      { group: 14, number: 1, level: 0, count: 3, durability: 3, skill: false, luck: false, optionLevel: 0, excellent: 0, classNumber: -1, name: 'Small Healing Potion' },
      { group: 0, number: 1, level: 3, count: 1, durability: 0, skill: true, luck: true, optionLevel: 0, excellent: 0, classNumber: 4, className: 'Dark Knight', name: 'Short Sword' },
    ],
  },
});

test('the website shows the starter kit and the guide steps with their rewards, in both languages', async ({ page }) => {
  let available = true;
  await page.route('**/game-name.json', (route) => route.fulfill({ json: { name: 'Test realm', links: {} } }));
  await page.route('**/website/data', (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/details', (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/missions', (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/world-bosses', (route) => route.fulfill({ status: 503 }));
  await page.route('**/website/onboarding', (route) => (available ? route.fulfill({ json: PUBLIC() }) : route.fulfill({ status: 503 })));
  await page.goto('/home.html');
  await page.locator('#en').click();
  const section = page.locator('#newplayers');
  await expect(page.locator('nav a[href="#newplayers"]')).toHaveText('New players');
  await expect(section.locator('h2')).toHaveText('Your first half hour');
  const kit = section.locator('.newplayers-kit');
  await expect(kit).toContainText('For every new character.');
  await expect(kit.locator('[data-kit-class="Every class"]')).toContainText('Small Healing Potion x3 (stacks of 3)');
  await expect(kit.locator('[data-kit-class="Dark Knight"]')).toContainText('Short Sword +3 (skill, luck)');
  await expect(kit).toContainText('50,000 Zen · 20 Goblin Points');
  await expect(kit).toContainText('15 extra stat points');
  await expect(kit).toContainText('Seal of Ascension for 60 minutes');
  await expect(section.locator('[data-step]')).toHaveCount(3);
  await expect(section.locator('[data-step="talk-merchant"]')).toContainText('1. Talk to a merchant');
  await expect(section.locator('[data-step="talk-merchant"]')).toContainText('Reward: 10,000 Zen');
  await expect(section.locator('[data-step="join-party"]')).toContainText('3. Join a party (optional)');
  await expect(section.locator('#newplayers-extra')).toContainText('When every step is done: 200,000 Zen · 50 Goblin Points · Jewel of Bless x1.');
  await expect(section.locator('#newplayers-extra')).toContainText('Characters up to level 10 count as new too.');
  await section.scrollIntoViewIfNeeded();
  await page.screenshot({ path: 'test-results/onboarding-website.png', fullPage: false });

  await page.locator('#vi').click();
  await expect(page.locator('nav a[href="#newplayers"]')).toHaveText('Bắt đầu chơi');
  await expect(section.locator('[data-step="talk-merchant"]')).toContainText('Nói chuyện với một thương nhân');
  await expect(section.locator('[data-step="talk-merchant"]')).toContainText('Phần thưởng:');
  await expect(kit).toContainText('Cho mỗi nhân vật mới.');
  await expect(kit).toContainText('15 điểm cộng thêm');

  // The server doesn't answer: a notice instead of old data.
  available = false;
  await page.locator('#en').click();
  await page.locator('#refresh').click();
  await expect(section.locator('#newplayers-notice')).toHaveText('The new-player guide is unavailable: the server didn\'t answer.');
  await expect(section.locator('[data-step]')).toHaveCount(0);
});

// The panel opens by itself once per character (its first time in the world, remembered in the browser); on later
// visits the guide waits as a small button with the step, which opens it; a step done while it's closed makes the
// button glow instead of opening the panel.
test('the guide opens by itself once per character, then waits as a button', async ({ page }) => {
  test.setTimeout(2 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('/index.html');
  const visit = () => page.evaluate(async () => {
    const { OnboardingWindow } = await import('/js/ui/onboardingWindow.js' as string) as { OnboardingWindow: new (options: unknown) => Panel };
    const w = window as unknown as Win;
    w.__sent = [];
    w.__chat = [];
    for (const button of document.querySelectorAll('.hnmo-guide-button')) button.remove();
    w.__panel?.close();
    w.__panel = new OnboardingWindow({
      send: (name: string, fields: Record<string, unknown>) => w.__sent.push({ name, fields: { ...fields } }),
      message: (text: string) => w.__chat.push(text),
      itemName: () => 'item',
      characterName: () => 'GuideOnce',
    });
    w.__panel.start();
  });
  await page.evaluate(() => { try { localStorage.removeItem('hnmo.onboarding.shown.GuideOnce'); } catch { /* none */ } });
  const panel = page.locator('.rml-hnmo_onboarding');
  const button = page.locator('.hnmo-guide-button');

  // The first visit: the panel opens by itself, no button while it's open; closed, the button shows the step.
  await visit();
  await feed(page, state('talk-merchant'));
  await expect(panel).toHaveCount(1);
  await expect(button).toBeHidden();
  await page.evaluate(() => (window as unknown as Win).__panel.close());
  await expect(panel).toHaveCount(0);
  await expect(button).toBeVisible();
  await expect(button).toHaveText('Guide 1/3');

  // The next visit: no panel, the button; the button opens the panel.
  await visit();
  await feed(page, state('talk-merchant'));
  await expect(button).toBeVisible();
  await expect(button).toHaveText('Guide 1/3');
  await expect(panel).toHaveCount(0);
  await button.click();
  await expect(panel).toHaveCount(1);
  await expect(button).toBeHidden();

  // A step done while closed: the panel stays closed, the button moves on and glows.
  await page.evaluate(() => (window as unknown as Win).__panel.close());
  await feed(page, state('kill-10', { done: ['talk-merchant'], event: { type: 'step', ids: ['talk-merchant'] } }));
  await expect(panel).toHaveCount(0);
  await expect(button).toHaveText('Guide 2/3');
  await expect(button).toHaveClass(/hnmo-guide-button-flash/);
  await expectNoPageErrors(errors);
});
