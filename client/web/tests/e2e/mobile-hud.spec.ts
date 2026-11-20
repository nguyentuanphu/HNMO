// Mobile & Tablet Web App + Modern Touch HUD tests (ROADMAP 17.1-17.8):
// - /sw-game.js PWA service worker script + /home.html viewport-fit=cover & responsive mobile layout
// - Saved accounts multi-login switcher on /index.html (EN/VI, no plaintext password stored)
// - MobileHud module: Hero card, collapsible top ribbon, minimap tap-to-walk & pinch/zoom,
//   virtual thumbstick drag vector, swipeable tabbed chat + 5 editable macros, bottom-sheet QoL,
//   Battery Saver (30 FPS) / 120 FPS cap / Wake Lock settings, and EN/VI live switching.
import { test, expect } from '@playwright/test';

test.use({ hasTouch: true, viewport: { width: 844, height: 390 } });

test('PWA service worker, home responsive viewport, and saved multi-account switcher (EN/VI)', async ({ page, request }) => {
  const swResp = await request.get('/sw-game.js');
  expect(swResp.status()).toBe(200);
  const swText = await swResp.text();
  expect(swText).toContain('NEVER_CACHE_PREFIXES');
  expect(swText).toContain('/relay');

  // Home page has viewport-fit=cover and renders cleanly on portrait phone (390x844) and tablet (1024x768)
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/home.html');
  const viewportMeta = await page.locator('meta[name="viewport"]').getAttribute('content');
  expect(viewportMeta).toContain('viewport-fit=cover');

  const homeOverflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  expect(homeOverflow).toBeLessThanOrEqual(4);

  // Pre-seed two saved accounts on /index.html (never storing passwords) and test switcher + EN/VI
  await page.setViewportSize({ width: 844, height: 390 });
  await page.addInitScript(() => {
    localStorage.setItem('hnmo.savedAccounts', JSON.stringify([
      { username: 'knight01', host: '127.0.0.1', port: '44406', relay: 'ws://127.0.0.1:18080/relay', updatedAt: 100 },
      { username: 'elf02', host: '127.0.0.1', port: '44407', relay: 'ws://127.0.0.1:18080/relay', updatedAt: 90 },
    ]));
  });
  await page.goto('/index.html');

  const savedBox = page.locator('#savedAccounts');
  await expect(savedBox).toBeVisible();
  await expect(savedBox).toContainText('Saved accounts');
  await expect(savedBox.locator('.saved-account-pick')).toHaveCount(2);

  // Tap elf02 chip fills username & port without storing any password
  await savedBox.locator('.saved-account-pick', { hasText: 'elf02' }).click();
  await expect(page.locator('#username')).toHaveValue('elf02');
  await expect(page.locator('#port')).toHaveValue('44407');

  // Remove knight01 chip updates DOM & localStorage
  await savedBox.locator('.saved-account-del').first().click();
  await expect(savedBox.locator('.saved-account-pick')).toHaveCount(1);

  // Switch to VI and verify Saved accounts heading updates
  await page.locator('#loginLanguage button[data-lang="vi"]').click();
  await expect(savedBox).toContainText('Tài khoản đã lưu');
});

test('Modern Touch HUD: thumbstick vector, minimap tap-to-walk/zoom, swipeable chat & 5 macros, QoL & EN/VI', async ({ page }) => {
  await page.goto('/index.html');
  const errors: string[] = [];
  page.on('pageerror', (err) => errors.push(err.message));

  const harness = await page.evaluate(async () => {
    const jsBase = '/js';
    const { Chat } = await import(`${jsBase}/game/chat.js`);
    const { MobileHud } = await import(`${jsBase}/ui/mobileHud.js`);
    const { BATTERY_SAVER, FPS_CAPS, isBatterySaver } = await import(`${jsBase}/render/quality.js`);
    const { setLanguage } = await import(`${jsBase}/i18n.js`);

    const sentChat: string[] = [];
    const walkedTo: { x: number; y: number }[] = [];
    const sticks: { nx: number; ny: number }[] = [];
    const qolCalls: string[] = [];

    const chat = new Chat();
    chat.enable((text: string) => sentChat.push(text), false);
    chat.add('Welcome to Lorencia', 'system');
    chat.add('Party spot at LT7', 'party', 'WizardPro');

    const hud = new MobileHud({
      chat,
      stickMove: (nx: number, ny: number) => sticks.push({ nx, ny }),
      walkToCell: (cell: { x: number; y: number }) => walkedTo.push(cell),
      fight: () => qolCalls.push('fight'),
      cycleTarget: () => 'Golden Dragon',
      pickNearest: () => { qolCalls.push('pick'); return true; },
      toggleAutoPlay: () => qolCalls.push('auto'),
      openAutoSettings: () => qolCalls.push('autoSettings'),
      openBag: () => qolCalls.push('bag'),
      openCharacter: () => qolCalls.push('char'),
      openParty: () => qolCalls.push('party'),
      openWarp: () => qolCalls.push('warp'),
      openFullMap: () => qolCalls.push('fullMap'),
      openRanking: () => qolCalls.push('ranking'),
      openGuide: () => qolCalls.push('guide'),
      openTimetable: () => qolCalls.push('timetable'),
      openCashShop: () => qolCalls.push('shop'),
      openSettings: () => qolCalls.push('settings'),
      sendCommand: (cmd: string) => qolCalls.push(`cmd:${cmd}`),
      triggerAutoBuff: () => qolCalls.push('autoBuff'),
      toggleHidePlayers: () => true,
      toggleBatterySaver: () => true,
      toggleWakeLock: () => true,
      sellJunk: () => qolCalls.push('sellJunk'),
      repairAll: () => qolCalls.push('repairAll'),
    });

    // Expose touch fight bar inside DOM so button-sync is tested too
    const login = document.getElementById('login');
    if (login) login.hidden = true;
    hud.setMode('modern');
    hud.setInWorld(true);
    hud.update({
      heroName: 'DKKySi',
      className: 'Dark Knight',
      classIndex: 1,
      level: 347,
      resets: 5,
      safeZone: true,
      fps: 120,
      batterySaver: isBatterySaver(BATTERY_SAVER),
      wakeLock: true,
      hideOtherPlayers: false,
      autoPlayActive: true,
      hp: 5200,
      maxHp: 5293,
      sd: 1500,
      maxSd: 1600,
      mp: 3200,
      maxMp: 3393,
      ag: 800,
      maxAg: 900,
      expFraction: 0.425,
      expPercentText: '42.50%',
      mapNumber: 0,
      mapName: 'Lorencia',
      cell: { x: 130, y: 63 },
      buffs: [{ label: 'Greater', secondsLeft: 45 }],
      partyDots: [{ name: 'ElfHealer', cell: { x: 135, y: 68 } }],
      targetName: null,
      skillHotkeys: [
        { hotkey: '1', selected: true, cooldown: 0, usable: true },
        { hotkey: '2', selected: false, cooldown: 0, usable: true },
        { hotkey: '3', selected: false, cooldown: 0, usable: true },
        { hotkey: '4', selected: false, cooldown: 0, usable: true },
        { hotkey: '5', selected: false, cooldown: 0, usable: true },
      ],
      potionCounts: [245, 198, 0, 99],
    });

    (window as unknown as Record<string, unknown>).__mhTest = { sentChat, walkedTo, sticks, qolCalls, hud, setLanguage };
    return { has120Fps: FPS_CAPS.includes(120), batteryCheck: isBatterySaver(BATTERY_SAVER) };
  });

  expect(harness.has120Fps).toBe(true);
  expect(harness.batteryCheck).toBe(true);

  await expect(page.locator('#mobileHud')).toBeVisible();
  await expect(page.locator('#mobileHeroCard')).toContainText('DKKySi');
  await expect(page.locator('#mobileHeroCard')).toContainText('Safe Zone');

  // 0. Compact by default: chat and mini map closed, and no HUD part over the centre 40% of the screen.
  await expect(page.locator('#mobileChatBox')).toBeHidden();
  await expect(page.locator('#mobileMiniMapCanvas')).toBeHidden();
  const overCentre = await page.evaluate(() => {
    const w = innerWidth, h = innerHeight;
    const box = { left: w * 0.3, right: w * 0.7, top: h * 0.3, bottom: h * 0.7 };
    const parts = [...document.querySelectorAll('#mobileHud *, #touchFight button, #touchBar button')] as HTMLElement[];
    return parts.filter((el) => {
      const r = el.getBoundingClientRect();
      if (r.width === 0 || r.height === 0 || getComputedStyle(el).visibility === 'hidden') return false;
      return r.left < box.right && r.right > box.left && r.top < box.bottom && r.bottom > box.top;
    }).map((el) => el.id || el.className);
  });
  expect(overCentre).toEqual([]);

  // 1. One top row: auto play on / off and the menu; the mini map opens from its round button.
  await expect(page.locator('#mobileAutoToggle')).toContainText('Auto ON');
  // The menu: ☰ of the HUD, or the key bar's ⋯ (☰) when the touch key bar shows on this page.
  const menuButton = page.locator('#mobileMenuBtn:visible, #touchBar button.more:visible');
  await expect(menuButton).toHaveCount(1);
  await page.locator('#mobileMiniMapToggle').click();
  await expect(page.locator('#mobileMiniMap')).toContainText('Lorencia 130,63');
  await expect(page.locator('#mobileMiniMapCanvas')).toBeVisible();

  // 2. Virtual Analog Thumbstick drag emits normalized vector then resets to 0
  const stick = page.locator('#mobileThumbstick');
  const box = (await stick.boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();
  await page.mouse.move(box.x + box.width / 2 + 28, box.y + box.height / 2 - 20, { steps: 4 });
  const activeVec = await stick.evaluate((el) => ({ nx: Number(el.dataset.nx), ny: Number(el.dataset.ny) }));
  expect(activeVec.nx).toBeGreaterThan(0.2);
  expect(activeVec.ny).toBeLessThan(-0.15);
  await page.mouse.up();
  const resetVec = await stick.evaluate((el) => ({ nx: Number(el.dataset.nx), ny: Number(el.dataset.ny) }));
  expect(resetVec).toEqual({ nx: 0, ny: 0 });

  // 3. Minimap tap-to-walk calculates non-center target cell
  const mapCanvas = page.locator('#mobileMiniMapCanvas');
  const mBox = (await mapCanvas.boundingBox())!;
  await page.mouse.click(mBox.x + mBox.width * 0.85, mBox.y + mBox.height * 0.2);
  const walks = await page.evaluate(() => (window as unknown as { __mhTest: { walkedTo: { x: number; y: number }[] } }).__mhTest.walkedTo);
  expect(walks.length).toBeGreaterThanOrEqual(1);
  expect(walks[0].x).toBeGreaterThanOrEqual(0);
  expect(walks[0].x).toBeLessThanOrEqual(255);

  // 4. Target lock (on the fight ring) says the locked monster
  await page.locator('#mobileTargetLock').click();
  await expect(page.locator('#mobileTargetLock')).toHaveAttribute('title', /Golden Dragon/);
  await expect(page.locator('#mobileTargetLock')).toHaveClass(/on/);

  // 5. Chat opens from 💬; macros (click + edit & save) and EN/VI language switch
  await page.locator('#mobileChatToggle').click();
  await expect(page.locator('#mobileChatBox')).toBeVisible();
  await page.locator('#mobileMacroBar button[data-macro="0"]').click();
  await page.locator('#mobileMacroEditBtn').click();
  await expect(page.locator('#mobileHudSheet')).toBeVisible();
  const macroInputs = page.locator('#mobileHudSheet .mh-macro-row input');
  await expect(macroInputs).toHaveCount(10); // 5 label + 5 text inputs
  await macroInputs.nth(0).fill('Boss!');
  await macroInputs.nth(1).fill('Boss up in Tarkan!');
  await page.locator('#mobileMacroSaveBtn').click();
  await expect(page.locator('#mobileMacroBar button[data-macro="0"]')).toContainText('Boss!');

  // 6. Quick QoL menu sheet (Sell Junk / Repair All / Auto-Loot Filter)
  await menuButton.click();
  await expect(page.locator('#mobileHudSheet')).toBeVisible();
  await page.locator('#mobileQolLootFilter').click();
  await expect(page.locator('#mobileQolLootFilter')).toContainText('Jewels + Exc');
  await page.locator('#mobileQolSellJunk').click();
  const calls = await page.evaluate(() => (window as unknown as { __mhTest: { qolCalls: string[]; sentChat: string[] } }).__mhTest);
  expect(calls.qolCalls).toContain('sellJunk');
  expect(calls.sentChat).toContain('Hello!');

  // 7. Language switch to VI updates HUD zone + tabs cleanly
  await page.evaluate(() => (window as unknown as { __mhTest: { setLanguage(l: string): Promise<void> } }).__mhTest.setLanguage('vi'));
  await expect(page.locator('#mobileHeroCard')).toContainText('An Toàn');
  await expect(page.locator('#mobileChatBox')).toContainText('Thế giới');

  // 8. Hero bars with numbers, no target bar without a target, the skill page and the grouped menu
  await expect(page.locator('#mobileHeroCard')).toContainText('HP 5200 / 5293');
  await expect(page.locator('#mobileHeroCard')).toContainText('SD 1500 / 1600');
  await expect(page.locator('#mobileTargetBar')).toBeHidden();
  await expect(page.locator('#mobileSkillPage')).toHaveText('1/2');
  await page.locator('#mobileSkillPage').click();
  await expect(page.locator('#mobileSkillPage')).toHaveText('2/2');
  await page.locator('#mobileSkillPage').click();
  await menuButton.click();
  await expect(page.locator('#mobileMenuGroups .mh-menu-group')).toHaveCount(7);
  await page.locator('#mobileMenuGroups .mh-menu-group').nth(1).click();
  await expect(page.locator('#mobileMenuGroups .mh-menu-item').first()).toBeVisible();
  // Warp, Auto buff and Party live in the menu now (Character / Social groups).
  await page.locator('#mobileMenuGroups .mh-menu-group').nth(0).click();
  await page.locator('#mobileMenuGroups .mh-menu-item', { hasText: 'Tự buff' }).click();
  await expect(page.locator('#mobileHudSheet')).toBeHidden();
  const after = await page.evaluate(() => (window as unknown as { __mhTest: { qolCalls: string[] } }).__mhTest.qolCalls);
  expect(after).toContain('autoBuff');

  expect(errors).toEqual([]);
});
