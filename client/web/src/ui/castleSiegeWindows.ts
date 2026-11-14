// The castle siege's windows, the desktop's own RML documents (Data/Interface/RmlUi), docked at the right:
//   guard_window   the guardsman (CGuardWindow): status, announce / register the guild marks, the lists
//   castle_window  the senior (CCastleWindow): castle gates, guardian statues, taxes, the store
//   gateman        the gatekeeper (CGatemanWindow): the hunting zone's entrance for guests, members, the master
//   gate_switch    the castle gate switch (CGateSwitchWindow)
//   catapult       the siege weapons (CCatapultWindow)
// and the siege HUD on the Valley of Loren (siege_warfare.rml, CSiegeWarfare). The state is game/castleSiege.ts.

import { CastleSiege, GATE_NPC, NpcUpgrade, STATUE_NPC, SiegePhase, senatus, type DefenseNpc } from '../game/castleSiege.js';
import { GuildStatus } from '../game/guild.js';
import { centreScreen, DockedWindow, format, gold, sizedLine, syncImages, TEXT_PX, screenScale, type FieldDialogs, type SizedLine } from './fieldEventDocs.js';
import { openDocument, prepareDocument, type RmlDocument, type RmlModel } from './rml.js';
import type { Item } from '../game/inventory.js';

const T = {
  GuardNPC: 'Guard NPC', OfficialSealOfKingS: 'Official seal of king: %s', None: 'None', AffiliatedGuildS: 'Affiliated guild: %s',
  Status: 'Status', Announce: 'Announce', Register: 'Register', List: 'List', StartingUUUUU: 'Starting %u-%u-%u %u : %u',
  UntillUUUUU: 'untill %u-%u-%u %u : %u', SiegePeriodIsOver: 'Siege period is over.', SiegeRegistrationPeriod: 'Siege registration period.',
  StandbyPeriodForSignRegistration: 'Standby period for sign registration.', PeriodForSignRegistration: 'Period for sign registration.',
  StandbyPeriodForAnnouncement: 'Standby period for announcement.', AnnouncementPeriod: 'Announcement period.',
  SiegePreparationPeriod: 'Siege preparation period.', SiegePeriod: 'Siege period.', TrucePeriod: 'Truce period.', SiegeIsOver: 'Siege is over.',
  ExpectedSiegePeriodIs: 'Expected siege period is', UUUUU: '%u-%u-%u %u : %u.', UUURemainedForTheNextStage: '%u : %u : %u remained for the next stage.',
  Announced: 'Announced', NotAGuildMaster: 'Not a guild master', RegisterTheAcquiredSign: 'Register the acquired sign.',
  AcquiredNoOfSignU: 'Acquired no. of sign: %u', RegisteredNoOfSignU: 'Registered no. of sign: %u',
  AnnouncementAndRegistrationPeriod: 'Announcement and registration period', HasEnded: 'has ended.',
  ThisGuildIsNotRegisteredInCastleSiege: 'This guild is not registered in Castle Siege.', AbandonCastleSiege: 'Abandon Castle Siege',
  NAME: 'NAME', NoReg: 'No. Reg.', Stat: 'Stat', Order: 'Order', Camp: 'Camp', Maintain: 'Maintain', Score: 'Score', Failed: 'Failed',
  Processing: 'Processing', DefendingTeam: 'Defending team', InvadingTeam: 'Invading team', Assist: 'Assist', Close388: 'Close',
  YouHaveNoAbility: 'You have no ability', ToAttackTheCastle: 'To attack the castle.',
  AreYouReallyWantToQuitTheSiegeWargare: 'Are you really want to quit the Siege Wargare?',
  // the senior
  DZenIsRequired: '%s zen is required.', ToPurchaseSelectedCastleGate: 'To purchase selected castle gate', WouldYouLikeToPurchase: 'Would you like to purchase?',
  ToRepairSelectedCastleGate: 'To repair selected castle gate', DGuardianJewelAndDZenAreRequired: '%d Guardian jewel and %s zen are required.',
  UpgradingTheDurabilityOfSelectedCastleGate: 'Upgrading the durability of selected castle gate', WouldYouLikeToRepair: 'Would you like to repair?',
  UpgradingTheDefensivePowerOfSelectedCastleGate: 'Upgrading the defensive power of selected castle gate', ToPurchaseSelectedStatue: 'To purchase selected statue',
  ToRepairSelectedStatue: 'To repair selected statue', UpgradingDurabilityOfSelectedCastleGate: 'Upgrading durability of selected castle gate',
  UpgradingDefensivePowerOfSelectedStatue: 'Upgrading defensive power of selected statue', UpgradingRecoveryPowerOfSelectedStatue: 'Upgrading recovery power of selected statue',
  ChaosCombinationGoblinTaxRateD: 'Chaos combination Goblin tax rate %d%%', VariousNPCTaxRateD: 'Various NPC tax rate %d%%', Apply1568: 'Apply?',
  EnterTheWithdrawalAmount: 'Enter the withdrawal amount.', Maximum15000000Zen: '(Maximum 15,000,000 Zen)', SeniorNPC: 'Senior NPC',
  CastleGate: 'Castle Gate', GuardianStatue: 'Guardian Statue', Tax: 'Tax', Store1640: 'Store', PurchaseAndRepair: 'Purchase and repair',
  Buy1124: 'Buy', Repair: 'Repair', Improve: 'Improve', DURDD: 'DUR : %s/%s', DPD: 'DP : %d', RRD: 'RR : %d%%', DURD: 'DUR +%s', DPD1564: 'DP +%s',
  RRD1565: 'RR +%s%%', AdjustTaxRate: 'Adjust tax rate', ChaosCombinationGoblinDD: 'Chaos combination Goblin: %d(%d)%%', NPCDD: 'NPC: %d(%d)%%',
  Apply: 'Apply', OnlyTheLordOfTheCastle: 'Only the lord of the castle', CanAdjustTheTaxRate: 'can adjust the tax rate.',
  TaxAdjustmentAvailable: 'Tax adjustment available', DuringTrucePeriod: 'during Truce Period.', MaximumTaxRates3: 'Maximum Tax rates: 3%%',
  NPCsInclude: 'NPCs include', ElfLalaPotionGirl: 'Elf Lala, Potion Girl', WizardArenaGuard: 'Wizard, Arena Guard', AndEtc: 'and etc.', Zen: 'Zen',
  Withdraw: 'Withdraw', TaxBelongsToTheCastle: 'Tax belongs to the castle', AndCanBeUsed: 'and can be used', ToOperateTheCastle: 'to operate the castle.',
  // the gatekeeper
  EnteringIsNotAllowed: 'Entering is not allowed', InsufficientZenForEntering: 'Insufficient zen for entering', EntranceRestriction: 'Entrance restriction',
  OnlyTheGuildMembers: 'Only the guild members', AreAllowedToEnter: 'are allowed to enter.', IsAllowed: 'is allowed', OpenItToNonMembers: 'Open it to non-members.',
  EntranceFeeSZen: 'Entrance fee : %s Zen', EntranceFeeSetting: 'Entrance fee setting', Confirm: 'Confirm', EntranceFeeRange0SZen: 'Entrance fee range: 0 ~ %s zen',
  ForSetting: 'for setting', IncreaseUnitSZen: '(Increase unit:%s zen)', Enter: 'Enter', WouldYouLikeToEnter: 'Would you like to enter?',
  EntranceFeeSzen: 'Entrance fee %szen', PayEntranceFeeToEnter: 'Pay entrance fee to enter',
  ApprovalFromTheLordOfACastleIsRequired: 'Approval from the lord of a castle is required', ForEntering: 'for entering', PleaseGoBack: 'Please go back',
  // the gate switch
  CastleGateSwitch: 'Castle Gate Switch', CanCommandToOpenOrClose: 'Can command to open or close', TheCastleGateInFront: 'the castle gate in front',
  BeCarefulItMightBeBeneficialToTheEnemy: 'Be careful! It might be beneficial to the enemy', Open1107: 'Open',
  // the catapult
  WeaponForInvadingTeam: 'Weapon for Invading team', WeaponForDefendingTeam: 'Weapon for defending team', DesiredAttackingLocation: 'Desired attacking location',
  SelectTheButtonAndPress: 'Select the button and press', ToShoot: 'To shoot.', Shoot: 'Shoot',
} as const;

/** What the windows read of the game. */
export interface SiegeWindowsOptions {
  siege: CastleSiege;
  dialogs: FieldDialogs;
  click: () => void;
  closeNpc: () => void;
  /** The crafting window closes the senior's (SendCraftingDialogCloseRequest). */
  closeCrafting: () => void;
  /** HideAllGroupA: the other docked windows close. */
  closeOthers: () => void;
  hero: () => { level: number; classIndex: number; money: number; guildStatus: number; guildName: string; allianceName: string };
  /** The bag's items (slot → item). */
  bag: () => ReadonlyMap<number, Item>;
  /** The senior's store tab (MIXTYPE_CASTLE_SENIOR); false when the web game has no such mix window. */
  openStore: () => boolean;
}

type Button = { label: string; shown: boolean; locked: boolean };
const hidden = (): Button => ({ label: '', shown: false, locked: false });
const empty = (): SizedLine => ({ text: '', text_px: 0 });
const nativePx = () => Math.round(TEXT_PX * screenScale());
const pageLine = (text: string, bold = false) => sizedLine(text, bold, 190);
const date = (pattern: string, d: { year: number; month: number; day: number; hour: number; minute: number }) => format(pattern, d.year, d.month, d.day, d.hour, d.minute);

// ---------------- the guardsman ----------------

const GUARD_LINES = ['title', 'owner_master', 'owner_guild', 'status_start', 'status_end', 'status_period', 'status_expected_label', 'status_expected_time',
  'status_next_stage', 'register_message', 'register_message2', 'register_acquired', 'register_registered', 'list_message'] as const;
type GuardLineName = typeof GUARD_LINES[number];

type GuardModel = RmlModel & Record<GuardLineName, SizedLine> & {
  text_px: number; line_height_px: number; active_tab: number; tabs: { label: string; selected: boolean }[]; register_bold: boolean;
  proclaim_button: Button; register_button: Button; give_up_button: Button; list_kind: number;
  declare_rows: { name: string; mark_count: string; state: string; order: string; selected: boolean }[];
  siege_rows: { name: string; side: string; involvement: string; selected: boolean; defending: boolean }[];
  header_name: string; header_mark_count: string; header_state: string; header_order: string; header_side: string; header_involvement: string;
  score_label: string; score_value: string; exit_tooltip: string;
  guard_tab: (tab: number) => void; guard_button: (button: number) => void; guard_declare_select: (name: string) => void; guard_siege_select: (name: string) => void;
};

const GUILD_MARK = { group: 14, number: 21, level: 3 }; // the Sign of Lord (Rena +3)

export class GuardWindow {
  readonly window: DockedWindow<GuardModel>;
  private tab = 0;

  constructor(private readonly o: SiegeWindowsOptions) {
    const lines = Object.fromEntries(GUARD_LINES.map((name) => [name, empty()])) as Record<GuardLineName, SizedLine>;
    const model: GuardModel = {
      ...lines, text_px: TEXT_PX, line_height_px: 14, active_tab: 0, tabs: [], register_bold: false,
      proclaim_button: hidden(), register_button: hidden(), give_up_button: hidden(), list_kind: 0, declare_rows: [], siege_rows: [],
      header_name: T.NAME, header_mark_count: T.NoReg, header_state: T.Stat, header_order: T.Order, header_side: T.Camp, header_involvement: T.Maintain,
      score_label: T.Score, score_value: '', exit_tooltip: T.Close388,
      guard_tab: (tab) => this.selectTab(tab),
      guard_button: (button) => this.press(button),
      guard_declare_select: (name) => this.select(true, name),
      guard_siege_select: (name) => this.select(false, name),
    };
    this.window = new DockedWindow('guard_window', model, {
      sync: () => this.sync(), click: o.click,
      onClose: () => { o.siege.clearLists(); o.closeNpc(); },
    });
  }

  get isOpen(): boolean { return this.window.isOpen; }

  /** UI::Siege::ShowGuardStatus: Show(INTERFACE_GUARDSMAN), whose OpeningProcess asks for the registration. */
  open(): void {
    if (!this.window.isOpen) {
      this.o.closeOthers();
      this.tab = 0;
      this.o.siege.openingGuard();
    }
    void this.window.open();
  }

  private marks(): { count: number; gridIndex: number } {
    let count = 0, gridIndex = -1;
    for (const [slot, item] of [...this.o.bag()].sort((a, b) => a[0] - b[0])) {
      if (slot < 12 || slot >= 76 || item.group !== GUILD_MARK.group || item.number !== GUILD_MARK.number || item.level !== GUILD_MARK.level) continue;
      count += item.durability;
      if (gridIndex < 0) gridIndex = slot - 12;
    }
    return { count, gridIndex };
  }

  private proclaimLocked(): boolean {
    const { siege } = this.o, hero = this.o.hero();
    return hero.allianceName === siege.ownerGuild || hero.guildName === siege.ownerGuild;
  }

  private selectTab(tab: number): void {
    if (tab < 0 || tab > 2) return;
    this.o.click();
    this.tab = tab;
    if (tab === 2) this.o.siege.requestList();
  }

  private press(button: number): void {
    const { siege } = this.o, hero = this.o.hero();
    const master = hero.guildStatus === GuildStatus.MASTER;
    this.o.click();
    if (button === 3) { this.window.close(); return; }
    if (this.tab === 1) {
      if (siege.phase === SiegePhase.REGSIEGE && button === 0 && master && !siege.registered && !this.proclaimLocked()) {
        if (CastleSiege.canDeclare(master, hero.level)) siege.register();
        else void this.o.dialogs.ok([T.YouHaveNoAbility, T.ToAttackTheCastle]);
      } else if (siege.phase === SiegePhase.REGMARK && button === 1 && siege.registered) {
        const { count, gridIndex } = this.marks();
        if (count > 0 && gridIndex >= 0) siege.registerMark(gridIndex);
      }
    } else if (this.tab === 2 && button === 2 && siege.registered && siege.phase >= SiegePhase.REGSIEGE && siege.phase <= SiegePhase.REGMARK && master) {
      void this.o.dialogs.ask([T.AreYouReallyWantToQuitTheSiegeWargare]).then((accepted) => { if (accepted) siege.giveUp(); });
    }
  }

  private select(declaration: boolean, name: string): void {
    const { siege } = this.o, hero = this.o.hero();
    if (this.tab !== 2) return;
    const kind = this.window.model.list_kind;
    if (declaration && kind === 1 && (name === hero.guildName || name === hero.allianceName) && siege.declarations.some((d) => d.name === name)) siege.selectedDeclaration = name;
    else if (!declaration && kind === 2 && siege.siegeGuilds.some((g) => g.name === name)) { siege.selectedSiegeGuild = name; siege.showScore = true; }
  }

  // CGuardWindow::SyncContent
  private sync(): void {
    const m = this.window.model, { siege } = this.o, hero = this.o.hero();
    const phase = siege.phase;
    const master = hero.guildStatus === GuildStatus.MASTER;
    m.text_px = nativePx();
    m.line_height_px = Math.round(14 * screenScale());
    m.title = sizedLine(T.GuardNPC, true, 160);
    m.owner_master = pageLine(format(T.OfficialSealOfKingS, siege.ownerGuildMaster || T.None), true);
    m.owner_guild = pageLine(format(T.AffiliatedGuildS, siege.ownerGuild || T.None), true);
    m.tabs = [T.Status, phase === SiegePhase.REGSIEGE ? T.Announce : T.Register, T.List].map((label, i) => ({ label, selected: i === this.tab }));
    m.active_tab = this.tab;
    m.register_bold = phase === SiegePhase.IDLE_3 || phase === SiegePhase.ENDSIEGE;
    for (const name of GUARD_LINES.slice(3)) m[name] = empty();
    m.proclaim_button = hidden(); m.register_button = hidden(); m.give_up_button = hidden();
    m.list_kind = 0;
    const periods: Record<number, string> = {
      [SiegePhase.NONE]: T.SiegePeriodIsOver, [SiegePhase.IDLE_1]: T.SiegePeriodIsOver, [SiegePhase.REGSIEGE]: T.SiegeRegistrationPeriod,
      [SiegePhase.IDLE_2]: T.StandbyPeriodForSignRegistration, [SiegePhase.REGMARK]: T.PeriodForSignRegistration, [SiegePhase.IDLE_3]: T.StandbyPeriodForAnnouncement,
      [SiegePhase.NOTIFY]: T.AnnouncementPeriod, [SiegePhase.READYSIEGE]: T.SiegePreparationPeriod, [SiegePhase.STARTSIEGE]: T.SiegePeriod,
      [SiegePhase.ENDSIEGE]: T.TrucePeriod, [SiegePhase.ENDCYCLE]: T.SiegeIsOver,
    };
    if (this.tab === 0) {
      m.status_start = pageLine(date(T.StartingUUUUU, siege.registrationStart));
      m.status_end = pageLine(date(T.UntillUUUUU, siege.registrationEnd));
      m.status_period = pageLine(periods[phase] ?? '');
      if (phase < SiegePhase.STARTSIEGE) {
        const left = siege.secondsRemaining;
        m.status_expected_label = pageLine(T.ExpectedSiegePeriodIs);
        m.status_expected_time = pageLine(date(T.UUUUU, siege.battleStart));
        m.status_next_stage = pageLine(format(T.UUURemainedForTheNextStage, Math.floor(left / 3600), Math.floor((left % 3600) / 60), (left % 3600) % 60));
      }
    } else if (this.tab === 1) {
      if (phase === SiegePhase.REGSIEGE) {
        if (!master) m.register_message = pageLine(T.NotAGuildMaster);
        else if (!siege.registered) m.proclaim_button = { label: T.Announce, shown: true, locked: this.proclaimLocked() };
        else m.register_message = pageLine(T.Announced);
      } else if (phase === SiegePhase.REGMARK) {
        if (siege.registered) {
          const { count } = this.marks();
          m.register_message = pageLine(T.RegisterTheAcquiredSign);
          m.register_acquired = pageLine(format(T.AcquiredNoOfSignU, count));
          m.register_registered = pageLine(format(T.RegisteredNoOfSignU, siege.markCount));
          m.register_button = { label: T.Register, shown: true, locked: count <= 0 };
        } else m.register_message = pageLine(T.ThisGuildIsNotRegisteredInCastleSiege);
      } else if (phase === SiegePhase.IDLE_3) {
        m.register_message = pageLine(T.AnnouncementAndRegistrationPeriod, true);
        m.register_message2 = pageLine(T.HasEnded, true);
      } else {
        const page: Record<number, string> = {
          [SiegePhase.NONE]: T.SiegePeriodIsOver, [SiegePhase.IDLE_1]: T.SiegePeriodIsOver, [SiegePhase.IDLE_2]: T.StandbyPeriodForSignRegistration,
          [SiegePhase.NOTIFY]: T.AnnouncementPeriod, [SiegePhase.READYSIEGE]: T.SiegePreparationPeriod, [SiegePhase.STARTSIEGE]: T.SiegePeriod,
          [SiegePhase.ENDSIEGE]: T.TrucePeriod, [SiegePhase.ENDCYCLE]: T.SiegeIsOver,
        };
        m.register_message = pageLine(page[phase] ?? '', phase === SiegePhase.ENDSIEGE);
      }
    } else {
      if (phase === SiegePhase.REGSIEGE || phase === SiegePhase.REGMARK) m.list_kind = 1;
      else if (phase === SiegePhase.NOTIFY || phase === SiegePhase.READYSIEGE) m.list_kind = 2;
      else if (phase === SiegePhase.ENDSIEGE) m.list_message = pageLine(T.TrucePeriod, true);
      if (siege.registered && phase >= SiegePhase.REGSIEGE && phase <= SiegePhase.REGMARK && master) m.give_up_button = { label: T.AbandonCastleSiege, shown: true, locked: false };
    }
    // The guild's own declarations only (GuardGuildLists::IsOwnDeclaration).
    m.declare_rows = siege.declarations.filter((d) => d.name === hero.guildName || d.name === hero.allianceName)
      .map((d) => ({ name: d.name, mark_count: String(d.markCount), state: d.gaveUp ? T.Failed : T.Processing, order: String(d.order), selected: d.name === siege.selectedDeclaration }));
    m.siege_rows = siege.siegeGuilds.map((g) => ({
      name: g.name, side: g.side === 1 ? T.DefendingTeam : T.InvadingTeam, involvement: g.involved ? T.Maintain : T.Assist,
      selected: g.name === siege.selectedSiegeGuild, defending: g.side === 1,
    }));
    const scored = siege.showScore ? siege.siegeGuilds.find((g) => g.name === siege.selectedSiegeGuild) : undefined;
    m.score_value = !scored ? '' : scored.side === 1 ? '--' : `${scored.name} :     ${scored.score}`;
  }
}

// ---------------- the senior ----------------

type CastleLine = SizedLine;
const SENIOR_LINES = ['title', 'map_title', 'improve_title', 'stat_hp', 'stat_defense', 'stat_recover', 'next_hp', 'next_defense', 'next_recover',
  'tax_title', 'chaos_rate', 'store_rate', 'note1', 'note2', 'note3', 'rule1', 'rule2', 'rule3', 'rule4', 'rule5', 'rule6',
  'zen_label', 'castle_money', 'footer1', 'footer2', 'footer3'] as const;
type SeniorLineName = typeof SENIOR_LINES[number];
const SENIOR_BUTTONS = ['buy_button', 'repair_button', 'hp_button', 'defense_button', 'recover_button', 'apply_button', 'withdraw_button'] as const;
type SeniorButtonName = typeof SENIOR_BUTTONS[number];
interface MapItem { statue: boolean; live: boolean; current: boolean; hp_width: number; hp_fill_width: number; defense_width: number; recover_width: number }

type SeniorModel = RmlModel & Record<SeniorLineName, CastleLine> & Record<SeniorButtonName, Button> & {
  text_px: number; line_height_px: number; active_tab: number; tabs: { label: string; text_px: number; selected: boolean }[];
  statue_page: boolean; item_live: boolean; map_items: MapItem[]; exit_tooltip: string;
  senatus_tab: (tab: number) => void; senatus_pick: (index: number) => void; senatus_button: (button: number) => void;
};

export class SeniorWindow {
  readonly window: DockedWindow<SeniorModel>;
  private tab = 0;

  constructor(private readonly o: SiegeWindowsOptions) {
    const lines = Object.fromEntries(SENIOR_LINES.map((name) => [name, empty()])) as Record<SeniorLineName, CastleLine>;
    const buttons = Object.fromEntries(SENIOR_BUTTONS.map((name) => [name, hidden()])) as Record<SeniorButtonName, Button>;
    const model: SeniorModel = {
      ...lines, ...buttons, text_px: TEXT_PX, line_height_px: 14, active_tab: 0, tabs: [], statue_page: false, item_live: false, map_items: [], exit_tooltip: T.Close388,
      senatus_tab: (tab) => this.selectTab(tab),
      senatus_pick: (index) => this.pick(index),
      senatus_button: (button) => this.press(button),
    };
    this.window = new DockedWindow('castle_window', model, { sync: () => this.sync(), click: o.click, onClose: () => o.closeCrafting() });
  }

  get isOpen(): boolean { return this.window.isOpen; }

  open(): void {
    if (!this.window.isOpen) { this.o.closeOthers(); this.tab = 0; this.o.siege.openingSenior(); }
    void this.window.open();
  }

  private selectTab(tab: number): void {
    if (tab < 0 || tab > 3) return;
    this.o.click();
    this.tab = tab;
    if (tab === 3 && !this.o.openStore()) this.tab = 3;
  }

  private pick(index: number): void {
    const { siege } = this.o;
    if (this.tab === 0 && index < 6) siege.currentGate = index;
    else if (this.tab === 1 && index < 4) siege.currentStatue = index;
  }

  private current(): DefenseNpc {
    const { siege } = this.o;
    return this.tab === 1 ? siege.statues[siege.currentStatue] : siege.gates[siege.currentGate];
  }

  private press(button: number): void {
    const { siege, dialogs } = this.o;
    const m = this.window.model;
    this.o.click();
    if (button === 11) { this.window.close(); return; }
    const locked = [m.buy_button, m.repair_button, m.hp_button, m.defense_button, m.recover_button, m.apply_button, m.withdraw_button][button]?.locked;
    if (locked) return;
    const statue = this.tab === 1;
    const npc = this.current();
    const number = statue ? STATUE_NPC : GATE_NPC;
    const index = (statue ? siege.currentStatue : siege.currentGate) + 1;
    const confirm = (lines: string[], action: () => void) => { void dialogs.ask(lines).then((accepted) => { if (accepted) action(); }); };
    const jewels = (level: number) => (statue ? [3, 5, 7] : [2, 3, 4])[Math.min(level, 2)];
    if (this.tab === 0 || this.tab === 1) {
      const cost = gold(senatus.repairCost(npc));
      if (button === 0) confirm([statue ? T.ToPurchaseSelectedStatue : T.ToPurchaseSelectedCastleGate, format(T.DZenIsRequired, cost), T.WouldYouLikeToPurchase], () => siege.repair(npc, number, index));
      else if (button === 1) confirm([statue ? T.ToRepairSelectedStatue : T.ToRepairSelectedCastleGate, format(T.DZenIsRequired, cost), statue ? T.WouldYouLikeToRepair : T.WouldYouLikeToPurchase], () => siege.repair(npc, number, index));
      else if (button === 2) confirm([statue ? T.UpgradingDurabilityOfSelectedCastleGate : T.UpgradingTheDurabilityOfSelectedCastleGate, format(T.DGuardianJewelAndDZenAreRequired, jewels(senatus.hpLevel(npc)), gold(1_000_000)), T.WouldYouLikeToRepair], () => siege.upgrade(npc, NpcUpgrade.HP));
      else if (button === 3) confirm([statue ? T.UpgradingDefensivePowerOfSelectedStatue : T.UpgradingTheDefensivePowerOfSelectedCastleGate, format(T.DGuardianJewelAndDZenAreRequired, jewels(npc.defenseLevel), gold(3_000_000)), T.WouldYouLikeToRepair], () => siege.upgrade(npc, NpcUpgrade.DEFENSE));
      else if (button === 4 && statue) confirm([T.UpgradingRecoveryPowerOfSelectedStatue, format(T.DGuardianJewelAndDZenAreRequired, jewels(senatus.recoverLevel(npc)), gold(5_000_000)), T.WouldYouLikeToRepair], () => siege.upgrade(npc, NpcUpgrade.RECOVER));
    } else if (this.tab === 2) {
      if (button === 5) confirm([format(T.ChaosCombinationGoblinTaxRateD, siege.chaosTaxRate), format(T.VariousNPCTaxRateD, siege.normalTaxRate), T.Apply1568], () => siege.applyTax());
      else if (button === 6) {
        dialogs.askInput([T.EnterTheWithdrawalAmount, T.Maximum15000000Zen], (typed) => {
          const amount = Number.parseInt(typed.replace(/\D/g, '').slice(0, 8) || '0', 10);
          if (!amount) return false;
          siege.withdraw(amount);
          return true;
        });
      } else if (button === 7) siege.changeTax(true, 1);
      else if (button === 8) siege.changeTax(true, -1);
      else if (button === 9) siege.changeTax(false, 1);
      else if (button === 10) siege.changeTax(false, -1);
    }
  }

  // CCastleWindow::SyncContent
  private sync(): void {
    const m = this.window.model, { siege } = this.o;
    m.text_px = nativePx();
    m.line_height_px = Math.round(14 * screenScale());
    m.active_tab = this.tab;
    m.title = sizedLine(T.SeniorNPC, true, 160);
    m.tabs = [T.CastleGate, T.GuardianStatue, T.Tax, T.Store1640].map((label, i) => ({ label, text_px: sizedLine(label, false, 40).text_px, selected: i === this.tab }));
    for (const name of SENIOR_LINES.slice(1)) m[name] = empty();
    for (const name of SENIOR_BUTTONS) m[name] = hidden();
    m.map_items = []; m.statue_page = false; m.item_live = false;
    if (this.tab === 0 || this.tab === 1) {
      const statue = this.tab === 1;
      m.statue_page = statue;
      const npc = this.current();
      m.map_title = sizedLine(T.PurchaseAndRepair, true, 190);
      const list = statue ? siege.statues : siege.gates;
      const currentIndex = (statue ? siege.currentStatue : siege.currentGate) + 1;
      const block = 24 / (3 + 1);
      m.map_items = list.map((n) => {
        const item: MapItem = { statue, live: n.live, current: n.index === currentIndex, hp_width: 0, hp_fill_width: 0, defense_width: 0, recover_width: 0 };
        if (n.live) {
          const rate = n.maxHp > 0 ? n.hp / n.maxHp : 0;
          item.hp_width = block * (senatus.hpLevel(n) + 1);
          item.hp_fill_width = item.hp_width * rate;
          item.defense_width = block * (n.defenseLevel + 1);
          if (statue) item.recover_width = block * (senatus.recoverLevel(n) + 1);
        }
        return item;
      });
      m.item_live = npc.live;
      if (!npc.live) { m.buy_button = { label: T.Buy1124, shown: true, locked: false }; return; }
      m.repair_button = { label: T.Repair, shown: true, locked: !(npc.hp < npc.maxHp) };
      m.hp_button = { label: T.Improve, shown: true, locked: !(senatus.hpLevel(npc) < 3) };
      m.defense_button = { label: T.Improve, shown: true, locked: !(npc.defenseLevel < 3) };
      if (statue) m.recover_button = { label: T.Improve, shown: true, locked: !(senatus.recoverLevel(npc) < 3) };
      m.stat_hp = sizedLine(format(T.DURDD, gold(npc.hp), gold(npc.maxHp)));
      m.stat_defense = sizedLine(format(T.DPD, senatus.defense(npc.number, npc.defenseLevel)));
      if (statue) m.stat_recover = sizedLine(format(T.RRD, senatus.recover(npc.number, npc.recoverLevel)));
      m.improve_title = sizedLine(T.Improve, true, 190);
      m.next_hp = sizedLine(format(T.DURD, gold(senatus.nextAddHp(npc))));
      m.next_defense = sizedLine(format(T.DPD1564, gold(senatus.nextAddDefense(npc))));
      if (statue) m.next_recover = sizedLine(format(T.RRD1565, gold(senatus.nextAddRecover(npc))));
    } else if (this.tab === 2) {
      m.tax_title = sizedLine(T.AdjustTaxRate, true, 190);
      m.chaos_rate = sizedLine(format(T.ChaosCombinationGoblinDD, siege.realChaosTaxRate, siege.chaosTaxRate), false, 175);
      m.store_rate = sizedLine(format(T.NPCDD, siege.realStoreTaxRate, siege.normalTaxRate), false, 175);
      m.apply_button = { label: T.Apply, shown: true, locked: false };
      m.note1 = sizedLine(T.OnlyTheLordOfTheCastle, false, 160);
      m.note2 = sizedLine(T.CanAdjustTheTaxRate, false, 160);
      m.note3 = sizedLine(T.TaxAdjustmentAvailable, false, 160);
      m.rule1 = sizedLine(T.DuringTrucePeriod, true, 160);
      m.rule2 = sizedLine(format(T.MaximumTaxRates3), true, 160);
      m.rule3 = sizedLine(T.NPCsInclude, true, 160);
      m.rule4 = sizedLine(T.ElfLalaPotionGirl, true, 160);
      m.rule5 = sizedLine(T.WizardArenaGuard, true, 160);
      m.rule6 = sizedLine(T.AndEtc, true, 160);
      m.zen_label = sizedLine(T.Zen, true);
      m.castle_money = sizedLine(gold(siege.castleMoney), true, 80);
      m.withdraw_button = { label: T.Withdraw, shown: true, locked: false };
      m.footer1 = sizedLine(T.TaxBelongsToTheCastle, true, 160);
      m.footer2 = sizedLine(T.AndCanBeUsed, true, 160);
      m.footer3 = sizedLine(T.ToOperateTheCastle, true, 160);
    }
  }
}

// ---------------- the gatekeeper ----------------

const GATEMAN_LINES = ['title', 'restriction', 'members_line1', 'members_line2', 'members_line3', 'open_to_non_members', 'entrance_fee', 'fee_setting_label',
  'view_fee', 'fee_range', 'fee_range_for', 'fee_increment', 'member_question', 'guest_fee', 'guest_pay_prompt', 'guest_question',
  'denied_line1', 'denied_line2', 'denied_line3', 'denied_line4'] as const;
type GatemanLineName = typeof GATEMAN_LINES[number];
type GatemanModel = RmlModel & Record<GatemanLineName, SizedLine> & {
  text_px: number; line_height_px: number; page: number; is_public: boolean; guest_can_afford: boolean; confirm_button: Button; enter_button: Button;
  exit_tooltip: string; gateman_public_toggle: () => void; gateman_button: (button: number) => void;
};
const TouchType = { PERSON: 1, GUILD_STAFF: 2, GUILD_MASTER: 3 } as const;

export class GatekeeperWindow {
  readonly window: DockedWindow<GatemanModel>;

  constructor(private readonly o: SiegeWindowsOptions) {
    const lines = Object.fromEntries(GATEMAN_LINES.map((name) => [name, empty()])) as Record<GatemanLineName, SizedLine>;
    const model: GatemanModel = {
      ...lines, text_px: TEXT_PX, line_height_px: 14, page: 0, is_public: false, guest_can_afford: true, confirm_button: hidden(), enter_button: hidden(),
      exit_tooltip: T.Close388,
      gateman_public_toggle: () => { if (o.siege.gatekeeper.type === TouchType.GUILD_MASTER) { o.click(); o.siege.togglePublic(); } },
      gateman_button: (button) => this.press(button),
    };
    this.window = new DockedWindow('gateman', model, { sync: () => this.sync(), click: o.click, onClose: () => o.closeNpc() });
  }

  get isOpen(): boolean { return this.window.isOpen; }
  open(): void { if (!this.window.isOpen) this.o.closeOthers(); void this.window.open(); }

  private press(button: number): void {
    const { siege } = this.o, keeper = siege.gatekeeper;
    this.o.click();
    if (button === 4) { this.window.close(); return; }
    if (keeper.type === TouchType.GUILD_MASTER) {
      if (button === 1) siege.sendFee();
      else if (button === 2) siege.feeUp();
      else if (button === 3) siege.feeDown();
      else if (button === 0) { siege.enterHuntingZone(); this.window.close(); }
    } else if (keeper.type === TouchType.GUILD_STAFF) {
      if (button === 0) { siege.enterHuntingZone(); this.window.close(); }
    } else if (keeper.type === TouchType.PERSON && button === 0 && keeper.public) {
      if (this.o.hero().money >= keeper.fee) { siege.enterHuntingZone(); this.window.close(); }
      else void this.o.dialogs.ok([T.EnteringIsNotAllowed, T.InsufficientZenForEntering]);
    }
  }

  // CGatemanWindow::SyncContent
  private sync(): void {
    const m = this.window.model, keeper = this.o.siege.gatekeeper;
    m.text_px = nativePx();
    m.line_height_px = Math.round(14 * screenScale());
    m.title = sizedLine(T.GuardNPC, true, 160);
    for (const name of GATEMAN_LINES.slice(1)) m[name] = empty();
    m.confirm_button = hidden(); m.enter_button = hidden();
    m.guest_can_afford = true;
    m.page = 0;
    m.is_public = keeper.public;
    if (keeper.type === TouchType.GUILD_MASTER) {
      m.page = 1;
      m.restriction = pageLine(T.EntranceRestriction, true);
      m.members_line1 = pageLine(T.OnlyTheGuildMembers);
      m.members_line2 = pageLine(T.AreAllowedToEnter);
      m.members_line3 = pageLine(T.IsAllowed);
      m.open_to_non_members = sizedLine(T.OpenItToNonMembers);
      m.entrance_fee = sizedLine(format(T.EntranceFeeSZen, gold(keeper.fee)));
      m.fee_setting_label = pageLine(T.EntranceFeeSetting, true);
      m.view_fee = sizedLine(`${gold(keeper.viewFee)} ${T.Zen}`, false, 200);
      m.confirm_button = { label: T.Confirm, shown: true, locked: false };
      m.fee_range = pageLine(format(T.EntranceFeeRange0SZen, gold(keeper.max)));
      m.fee_range_for = pageLine(T.ForSetting);
      m.fee_increment = pageLine(format(T.IncreaseUnitSZen, gold(keeper.unit)));
      m.enter_button = { label: T.Enter, shown: true, locked: false };
    } else if (keeper.type === TouchType.GUILD_STAFF) {
      m.page = 2;
      m.member_question = pageLine(T.WouldYouLikeToEnter, true);
      m.enter_button = { label: T.Enter, shown: true, locked: false };
    } else if (keeper.type === TouchType.PERSON) {
      m.page = 3;
      if (keeper.public) {
        m.guest_can_afford = keeper.fee <= this.o.hero().money;
        m.guest_fee = pageLine(format(T.EntranceFeeSzen, gold(keeper.fee)), true);
        m.guest_pay_prompt = pageLine(T.PayEntranceFeeToEnter, true);
        m.guest_question = pageLine(T.WouldYouLikeToEnter, true);
      } else {
        m.denied_line1 = pageLine(T.EnteringIsNotAllowed, true);
        m.denied_line2 = pageLine(T.ApprovalFromTheLordOfACastleIsRequired, true);
        m.denied_line3 = pageLine(T.ForEntering, true);
        m.denied_line4 = pageLine(T.PleaseGoBack, true);
      }
      m.enter_button = { label: T.Enter, shown: true, locked: !keeper.public };
    }
  }
}

// ---------------- the gate switch ----------------

type GateSwitchModel = RmlModel & {
  text_px: number; bold_text_px: number; title: string; title_px: number; line1: string; line2: string; warning: string; gate_opened: boolean;
  button_text: string; label_line_px: number; exit_tooltip: string; gate_switch_toggle: () => void; gate_switch_exit: () => void;
};

export class GateSwitchWindow {
  readonly window: DockedWindow<GateSwitchModel>;

  constructor(private readonly o: SiegeWindowsOptions) {
    const model: GateSwitchModel = {
      text_px: TEXT_PX, bold_text_px: TEXT_PX, title: T.CastleGateSwitch, title_px: TEXT_PX, line1: T.CanCommandToOpenOrClose, line2: T.TheCastleGateInFront,
      warning: T.BeCarefulItMightBeBeneficialToTheEnemy, gate_opened: false, button_text: T.Open1107, label_line_px: 14, exit_tooltip: T.Close388,
      gate_switch_toggle: () => { o.click(); o.siege.toggleGate(); this.window.close(); },
      gate_switch_exit: () => { o.click(); this.window.close(); },
    };
    this.window = new DockedWindow('gate_switch', model, { sync: () => this.sync(), click: o.click, onClose: () => o.closeNpc() });
  }

  get isOpen(): boolean { return this.window.isOpen; }
  open(): void { if (!this.window.isOpen) this.o.closeOthers(); void this.window.open(); }

  private sync(): void {
    const m = this.window.model;
    m.text_px = m.bold_text_px = nativePx();
    m.title_px = sizedLine(T.CastleGateSwitch, true, 160).text_px;
    m.gate_opened = this.o.siege.gateSwitch.opened;
    m.button_text = m.gate_opened ? T.Close388 : T.Open1107;
    m.label_line_px = Math.round(14 * screenScale());
  }
}

// ---------------- the catapult ----------------

// CCatapultGroupButton::Create(): each side's target areas.
const ATTACK_AREAS: [string, boolean][] = [['Castle Gate 1', false], ['Castle Gate 2', false], ['Castle Gate 3', false], ['Front yard', true]];
const DEFENSE_AREAS: [string, boolean][] = [['Front yard1', true], ['Front yard2', true], ['Bridge', true]];
const CATAPULT_ATTACK = 1, CATAPULT_DEFENSE = 2;

type CatapultModel = RmlModel & {
  text_px: number; title: string; title_px: number; lines: SizedLine[]; mode: number; line_height_px: number; fire_text: string; fire_locked: boolean;
  areas: { label: string; index: number; big: boolean; locked: boolean; label_px: number }[]; exit_tooltip: string;
  catapult_area: (index: number) => void; catapult_fire: () => void; catapult_exit: () => void;
};

export class CatapultWindow {
  readonly window: DockedWindow<CatapultModel>;

  constructor(private readonly o: SiegeWindowsOptions) {
    const model: CatapultModel = {
      text_px: TEXT_PX, title: '', title_px: TEXT_PX, lines: [], mode: 0, line_height_px: 14, fire_text: T.Shoot, fire_locked: true, areas: [], exit_tooltip: T.Close388,
      catapult_area: (index) => {
        const c = o.siege.catapult, count = c.type === CATAPULT_ATTACK ? ATTACK_AREAS.length : c.type === CATAPULT_DEFENSE ? DEFENSE_AREAS.length : 0;
        if (index >= 0 && index < count && index !== c.area) { o.click(); c.area = index; c.fireLocked = false; }
      },
      catapult_fire: () => {
        const c = o.siege.catapult;
        if (c.fireLocked || c.area < 0) return;
        o.click();
        o.siege.fire();
        this.window.close();
      },
      catapult_exit: () => { o.click(); this.window.close(); },
    };
    this.window = new DockedWindow('catapult', model, { sync: () => this.sync(), click: o.click, onClose: () => o.closeNpc() });
  }

  get isOpen(): boolean { return this.window.isOpen; }
  open(): void { if (!this.window.isOpen) this.o.closeOthers(); void this.window.open(); }

  private sync(): void {
    const m = this.window.model, c = this.o.siege.catapult;
    m.text_px = nativePx();
    m.line_height_px = Math.round(14 * screenScale());
    m.title = c.type === CATAPULT_ATTACK ? T.WeaponForInvadingTeam : c.type === CATAPULT_DEFENSE ? T.WeaponForDefendingTeam : '';
    m.title_px = sizedLine(m.title, true, 190).text_px;
    m.lines = [T.DesiredAttackingLocation, T.SelectTheButtonAndPress, T.ToShoot].map((t) => sizedLine(t, true, 190));
    m.mode = c.type;
    const areas = c.type === CATAPULT_ATTACK ? ATTACK_AREAS : c.type === CATAPULT_DEFENSE ? DEFENSE_AREAS : [];
    m.areas = areas.map(([label, big], index) => ({ label, index, big, locked: index === c.area, label_px: sizedLine(label, false, big ? 77 : 46).text_px }));
    m.fire_locked = c.fireLocked;
  }
}

// ---------------- the siege HUD (siege_warfare.rml) ----------------

const MINI_MAP = { x: 25, y: 28 }; // kMiniMapPos
type SiegeHudModel = RmlModel & {
  bold_text_px: number; big_text_px: number; alpha: number; map_rect: string; alpha_label: string; time_visible: boolean; time_text: string;
  dots: { left: number; top: number }[]; hero_left: number; hero_top: number;
  commands: { left: number; top: number; command: number; team: string; color: string }[];
  skill_visible: boolean; skill_rect: string; skill_affordable: boolean; kills_needed: string; kills: string;
  teams: { selected: boolean; label: string }[]; orders_visible: boolean; chosen_team: number; cursor_visible: boolean; cursor_left: number; cursor_top: number;
  cursor_command: number; cursor_team: string;
  siege_alpha_click: () => void; siege_scale_click: () => void; siege_scroll_up_click: () => void; siege_scroll_down_click: () => void;
  siege_team_click: (team: number) => void; siege_order_click: (order: number) => void;
};

/** CSiegeWarfare with its observer / soldier / commander (CSiegeWarBase): the mini map, the time, the orders. */
export class SiegeHud {
  private doc: RmlDocument | null = null;
  private opening: Promise<void> | null = null;
  private scale = 1;
  private offset = { x: 0, y: 0 };
  private team = -1;
  private order = -1;
  private pointer: { x: number; y: number } | null = null;
  readonly model: SiegeHudModel;

  constructor(private readonly o: { siege: CastleSiege; hero: () => { x: number; y: number }; others: () => Iterable<{ x: number; y: number }>;
    /** The commander: a guild master of a guild alone or leading its alliance, on a side of the siege. */
    commander: () => boolean; click: () => void; }) {
    this.model = {
      bold_text_px: TEXT_PX, big_text_px: 15, alpha: 1, map_rect: '0 0 128 128', alpha_label: '100', time_visible: false, time_text: '', dots: [], hero_left: 0, hero_top: 0,
      commands: [], skill_visible: false, skill_rect: '0 0 20 28', skill_affordable: false, kills_needed: '', kills: '', teams: [], orders_visible: false,
      chosen_team: -1, cursor_visible: false, cursor_left: 0, cursor_top: 0, cursor_command: 0, cursor_team: "",
      siege_alpha_click: () => { this.model.alpha = this.model.alpha <= 0.5 ? 1 : this.model.alpha - 0.1; },
      siege_scale_click: () => { this.scale = this.scale === 1 ? 2 : 1; },
      siege_scroll_up_click: () => undefined,
      siege_scroll_down_click: () => undefined,
      siege_team_click: (team) => { if (team >= 0 && team < 7) { this.team = this.team === team ? -1 : team; this.order = -1; } },
      siege_order_click: (order) => { if (this.team !== -1 && this.order === -1 && order >= 0 && order < 3) this.order = order; },
    };
    prepareDocument('siege_warfare').catch((error: unknown) => console.warn('Interface', error));
    window.addEventListener('resize', () => this.place());
  }

  get shown(): boolean { return this.doc !== null; }

  private point(x: number, y: number): { x: number; y: number } {
    return { x: Math.floor(x / this.scale) - this.offset.x + MINI_MAP.x, y: Math.floor((256 - y) / this.scale) - this.offset.y + MINI_MAP.y };
  }

  /** Every frame: shown on the siege map only (CSiegeWarfare::SyncRmlModel). */
  frame(onMap: boolean): void {
    if (!onMap) { this.hide(); return; }
    this.fill();
    if (this.doc) { this.doc.update(); syncImages(this.doc); return; }
    this.opening ??= openDocument('siege_warfare', this.model, { parent: document.body }).then((doc) => {
      this.doc = doc;
      doc.host.style.zIndex = '11';
      doc.host.style.pointerEvents = 'none';
      const map = doc.$('#map');
      // The commander's order lands where the map is clicked.
      for (const id of ['#siege_hud', '#map', '#frame']) doc.$(id)?.addEventListener('pointerdown', (event) => event.stopPropagation());
      map?.addEventListener('pointermove', (event) => { const r = map.getBoundingClientRect(); this.pointer = { x: (event.clientX - r.left) / r.width * 128, y: (event.clientY - r.top) / r.height * 128 }; });
      map?.addEventListener('pointerleave', () => { this.pointer = null; });
      map?.addEventListener('click', () => this.placeOrder());
      for (const element of doc.root.querySelectorAll<HTMLElement>('#scale_button, #alpha_button, .team, .order-button, .scroll, #map')) element.style.pointerEvents = 'auto';
      this.place();
      syncImages(doc);
    }).catch((error: unknown) => console.warn('Interface', error)).finally(() => { this.opening = null; });
  }

  private placeOrder(): void {
    const p = this.pointer;
    if (!p || this.order === -1 || !this.o.commander()) return;
    const x = (Math.floor(p.x) + this.offset.x) * this.scale, y = 256 - (Math.floor(p.y) + this.offset.y) * this.scale;
    this.o.siege.command(this.team, x & 0xff, y & 0xff, this.order);
    this.order = -1;
  }

  private fill(): void {
    const m = this.model, { siege } = this.o, hero = this.o.hero();
    // UpdateHeroPos
    const wx = Math.floor(hero.x / this.scale), wy = Math.floor((256 - hero.y) / this.scale);
    this.offset = { x: Math.max(wx - 64 * this.scale, 0), y: Math.min(Math.max(wy - 64 * this.scale, 0), 128) };
    m.hero_left = wx - this.offset.x + MINI_MAP.x;
    m.hero_top = wy - this.offset.y + MINI_MAP.y;
    m.map_rect = `${this.offset.x * this.scale} ${this.offset.y * this.scale} ${128 * this.scale} ${128 * this.scale}`;
    m.alpha_label = String(Math.floor(m.alpha * 100.5));
    m.bold_text_px = m.big_text_px = TEXT_PX; // the board is scaled as a whole
    m.time_visible = siege.battleStarted;
    if (m.time_visible) {
      const { hour, minute } = siege.time();
      m.time_text = format(minute < 10 ? '%d:0%d' : '%d:%d', hour, minute);
    } else m.time_text = '';
    const commander = this.o.commander();
    m.dots = [...this.o.others()].map((c) => { const p = this.point(c.x, c.y); return { left: p.x, top: p.y }; });
    if (commander) {
      for (const unit of siege.memberLocations) {
        const p = this.point(unit.x, unit.y);
        if (p.x >= MINI_MAP.x && p.x <= MINI_MAP.x + 128 && p.y >= MINI_MAP.y && p.y <= MINI_MAP.y + 128) m.dots.push({ left: p.x, top: p.y });
      }
    }
    // FillCommands: the soldiers and the commander see the orders; a new one pulses.
    m.commands = [];
    if (siege.formation !== 0 || commander) {
      for (const c of siege.commands) {
        if (!c || c.command > 2 || c.team > 6) continue;
        const p = this.point(c.x, c.y);
        if (p.x < MINI_MAP.x || p.x > MINI_MAP.x + 128 || p.y < MINI_MAP.y || p.y > MINI_MAP.y + 128) continue;
        let pulse = 255;
        if (c.lifetime > 0) { pulse = Math.round(Math.min(1, Math.max(0, 1 + Math.sin(c.lifetime * 0.2))) * 255); c.lifetime--; }
        m.commands.push({ left: p.x, top: p.y, command: c.command, team: String(c.team + 1), color: `rgb(255, ${pulse}, ${pulse})` });
      }
    }
    m.teams = commander ? Array.from({ length: 7 }, (_, i) => ({ selected: i === this.team, label: String(i + 1) })) : [];
    m.orders_visible = commander && this.team !== -1 && this.order === -1;
    m.chosen_team = this.team;
    m.cursor_visible = commander && this.team !== -1 && this.order !== -1 && this.pointer !== null;
    if (m.cursor_visible && this.pointer) {
      m.cursor_left = this.pointer.x + MINI_MAP.x;
      m.cursor_top = this.pointer.y + MINI_MAP.y;
      m.cursor_command = this.order;
      m.cursor_team = String(this.team + 1);
    }
  }

  private place(): void {
    if (!this.doc) return;
    centreScreen(this.doc);
    this.doc.update();
  }

  hide(): void {
    this.doc?.close();
    this.doc = null;
    this.team = -1;
    this.order = -1;
  }
}
