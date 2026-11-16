import { loadGameName, showCommunityLinks } from "./gameName.js";

type Language = "en" | "vi";
const copy = {
  en: {
    missionsNav: "Missions",
    missionsTitle: "Today's missions",
    missionsHint: "Finish them in the game and claim the rewards in the missions window (J). Daily missions start again every day, weekly ones on Monday.",
    missionsDaily: "Daily",
    missionsWeekly: "Weekly",
    missionsReset: "New daily missions in",
    missionsOffline: "The missions are unavailable: the server didn't answer.",
    missionsNone: "No missions right now.",
    missionsReward: "Reward",
    missionsStreak: "Finish the daily missions several days in a row: every %d days pays a bonus (%s).",
    missionsCap: "Earned coins (online time, level milestones, invites, login calendar): at most %d a day per account.",
    newNav: "New players",
    newTitle: "Your first half hour",
    newHint: "New characters get a starter kit at their first login and a guide in the game (Y): every step pays a reward, and finishing them all pays a final one. You can skip it at any time.",
    newKit: "Starter kit",
    newKitPerAccount: "Once per account, for its first new character.",
    newKitPerCharacter: "For every new character.",
    newKitAll: "Every class",
    newKitPoints: "%d extra stat points",
    newKitBuff: "%s for %d minutes",
    newKitBuffPlain: "An EXP buff for %d minutes",
    newSkill: "skill",
    newLuck: "luck",
    newStack: "stacks of %d",
    newSteps: "Guide steps",
    newOptional: "optional",
    newFinal: "When every step is done",
    newOffline: "The new-player guide is unavailable: the server didn't answer.",
    newOff: "The new-player guide and starter kit are switched off on this server.",
    newLevel: "Characters up to level %d count as new too.",
    bossesNav: "World bosses",
    bossesTitle: "World bosses",
    bossesHint: "Strong bosses appear on a schedule (Sydney time; your own time in brackets). Everyone who deals damage shares the rewards by rank: the top 3, the top 10, everyone with enough damage, and the last hit. No damage, no reward. Kills give boss-hunt points for the weekly ranking.",
    bossesOffline: "The world bosses are unavailable: the server didn't answer.",
    bossesNone: "No world bosses right now.",
    bossesOff: "World bosses are switched off on this server.",
    bossesNext: "Next spawn",
    bossesIn: "in",
    bossesYourTime: "Your time",
    bossesAt: "At",
    bossesAlive: "On the map now — leaves in",
    bossesMap: "Map",
    bossesTimes: "Every day at",
    bossesEvery: "Every %d hours from",
    bossesDays: "On",
    bossesRewardTop1: "Rank 1",
    bossesRewardTop2: "Rank 2",
    bossesRewardTop3: "Rank 3",
    bossesRewardTop10: "Ranks 4–10",
    bossesRewardParticipation: "Everyone with at least %s% of the damage",
    bossesRewardLastHit: "Last hit",
    bossesLastKill: "Last kill",
    bossesLeft: "left without being killed",
    bossesLastHit: "last hit",
    bossesNews: "Announcements",
    bossesWeek: "This week's rankings",
    bossesWeekEnds: "The week ends in",
    bossesLastWeek: "Last week's winners",
    guildsNav: "Guilds",
    guildsTitle: "Guild levels and the weekly ladder",
    guildsHint: "Members raise their guild's level by playing: monsters, events, world bosses, missions, time in the game and the castle siege. Each level gives the online members a small buff. Every Monday at 00:00 (Sydney time) the top guilds of the week get rewards for every member.",
    guildsOffline: "The guilds are unavailable: the server didn't answer.",
    guildsOff: "Guild levels are switched off on this server.",
    guildsLadder: "This week's ladder",
    guildsLastWeek: "Last week's winners",
    guildsWeekOf: "Week of",
    guildsAll: "Guilds by level",
    guildsSieges: "Castle siege results",
    guildsNoSieges: "No castle siege was won yet.",
    guildsLevels: "Levels and buffs",
    guildsGuild: "Guild",
    guildsLevel: "Level",
    guildsPoints: "Points",
    guildsXp: "Guild XP",
    guildsMembers: "Members",
    guildsCoins: "Coins each",
    guildsBuff: "Buff",
    guildsNone: "No guild has points yet.",
    guildsNoBuff: "none",
    guildsExp: "EXP",
    guildsZen: "zen from monsters",
    guildsDefense: "defense",
    guildsRewards: "Monday rewards for every member",
    guildsMaster: "the guild master gets %d more",
    guildsSources: "Guild XP",
    guildsCap: "up to %d a day per member",
    guildsWeekEnds: "The week ends in",
    guildsSourceMonsterKill: "a monster",
    guildsSourceEventFinish: "an event finished",
    guildsSourceEventWin: "an event won",
    guildsSourceWorldBossKill: "a world boss kill",
    guildsSourceWorldBossDamage: "per % of a world boss's damage",
    guildsSourceMission: "a mission",
    guildsSourceOnlineTime: "per %d minutes in the game",
    guildsSourceCastleSiege: "the castle siege won",
    communityNav: "Community",
    communityTitle: "Join the community",
    communityHint: "Chat with other players, hear about events first and get free coins: once for linking your account to our Discord, once for joining our Zalo group. Community events give rewards for a code word or to the winners.",
    communityJoin: "Join us:",
    communityCoins: "How to get the coins",
    communityEvents: "Community events",
    communityHighlights: "Recent highlights",
    communityNoEvents: "No community events right now.",
    communityNoHighlights: "Nothing to show yet: rare drops, world boss kills and guild news appear here.",
    communityOffline: "The community section is unavailable: the server didn't answer.",
    communityOff: "Community rewards are switched off on this server.",
    communityDiscordHow: "Discord: join our server, open the community window (K) in the game, get a link code and type /link CODE in Discord. You get %s once.",
    communityZaloHow: "Zalo: join our group, claim in the community window (K) and post the code you get in the group. The admin approves it and you get %s once.",
    communityCodeHow: "Enter the event's code in the community window (K), once per account.",
    communityWinnersHow: "Winners",
    communityReward: "Reward",
    communityOpen: "Running",
    communityUpcoming: "Starts",
    communityEnded: "Over",
    communityUntil: "until",
    racesNav: "Top races",
    racesTitle: "Top races",
    racesHint: "Race the other players by day, week or month: resets, levels, boss hunting, events and more, some per class. The best ranks win coins and items, paid once when a race ends. In the game: the top races window (F9).",
    racesRunning: "Running now",
    racesNone: "No top race is running right now.",
    racesUpcoming: "Coming races",
    racesHistory: "Past winners",
    racesNoHistory: "No race has finished yet.",
    racesOffline: "The top races are unavailable: the server didn't answer.",
    racesOff: "Top races are switched off on this server.",
    racesEnds: "Ends in %s",
    racesEndsAt: "%s Sydney time (%s your time)",
    racesStarts: "Starts %s Sydney time (%s your time)",
    racesPrizes: "Prizes",
    racesMinimum: "At least %s for a prize.",
    racesNobody: "Nobody yet.",
    racesNobodyTook: "nobody took part",
    racesRank: "Rank %d",
    racesRanks: "Ranks %d-%d",
    racesPlayer: "Player",
    racesValue: "Value",
    racesAll: "all classes",
    racesOnly: "only %s",
    racesPerClass: "a table per class",
    racesGuilds: "guilds (every member gets the prize)",
    racesDaily: "daily",
    racesWeekly: "weekly",
    racesMonthly: "monthly",
    racesCustom: "special dates",
    racesKindResetsGained: "Resets done",
    racesKindLevelsGained: "Levels gained",
    racesKindLevelReached: "Highest level reached",
    racesKindBossHuntPoints: "Boss-hunt points",
    racesKindPkKills: "Players defeated (PK)",
    racesKindEventWins: "Event wins",
    racesKindGuildPoints: "Guild points",
    racesKindMonsterKills: "Monsters defeated",
    racesKindOnlineMinutes: "Minutes in the game",
    racesClass0: "Dark Wizard",
    racesClass1: "Dark Knight",
    racesClass2: "Fairy Elf",
    racesClass3: "Magic Gladiator",
    racesClass4: "Dark Lord",
    racesClass5: "Summoner",
    racesClass6: "Rage Fighter",
    giftNav: "Gift codes",
    giftTitle: "Gift codes",
    giftHint: "Gift codes give coins, zen and items. Enter one in the game with /code CODE in the chat or in the gift code window (community window K, or the touch \"more\" menu), or on the account page with your account name and password. Each code has its own rules: dates (Sydney time), uses, once per account or per character, levels or classes.",
    giftAccountLink: "Enter a code on the account page",
    giftNone: "No public gift code right now: watch the community posts.",
    giftOffline: "The gift codes are unavailable: the server didn't answer.",
    giftOff: "Gift codes are switched off on this server.",
    giftUntil: "until %s Sydney time (%s your time)",
    giftFrom: "from %s Sydney time (%s your time)",
    giftLeft: "%s uses left",
    giftPerAccount: "once per account",
    giftPerCharacter: "once per character",
    giftLevels: "level %s",
    giftNoWebsite: "On this server, codes are entered in the game only.",
    resetsNav: "Resets",
    resetsTitle: "Reset ranking and relife",
    resetsHint: "Resets done this month (Sydney time) and the relifes (grand resets). Reset with the reset NPC or /reset, see what the next one needs with /reset info, do a relife with /relife, or let the server reset your character while it's offline leveling with /autoreset on. You can also reset, add or clear stat points and switch the delegated reset on the account page.",
    resetsAccountLink: "Reset on the account page",
    resetsMonth: "This month (%s)",
    resetsPrevious: "Last month (%s)",
    resetsRelifes: "Relifes",
    resetsRules: "Reset tiers",
    resetsNone: "No resets yet.",
    resetsNoRelifes: "No relifes yet.",
    resetsOffline: "The reset ranking is unavailable: the server didn't answer.",
    resetsOff: "The reset system is switched off on this server.",
    resetsCount: "Resets",
    resetsRelifeCount: "Relife",
    resetsTierRange: "Resets %s",
    resetsTierLine: "level %s · %s · %s points (%s) · %s a day",
    resetsModeTotal: "total",
    resetsModeAdd: "added",
    resetsModeKeep: "kept",
    resetsNoLimit: "no limit",
    resetsFree: "free",
    resetsVipLine: "VIP: %s levels lower, %s% off zen, %s% off coins, %s more resets a day.",
    resetsRelifeLine: "Relife after %s resets at level %s: %s; keeps %s% of the resets, +%s permanent points per relife, at most %s.",
    bossesNoEntries: "No entries yet.",
    bossesRankBossHunt: "Boss hunt",
    bossesRankBloodCastle: "Blood Castle",
    bossesRankDevilSquare: "Devil Square",
    bossesRankChaosCastle: "Chaos Castle",
    bossesCastleSiege: "Castle siege winners",
    bossesCastleSiegeText: "Every member of the guild that wins the castle siege gets %d coins at the end of the week.",
    bossesPrizes: "Weekly prizes (top 10)",
    bossesRank: "Rank",
    bossesName: "Character",
    bossesDamage: "Damage",
    bossesPoints: "Points",
    bossesCoins: "Coins",
    phoneNav: "Phone",
    phoneTitle: "Play on your phone",
    phoneIntro: "The game runs in the phone's browser and installs to the home screen like an app; nothing to download.",
    phoneOpen: "Open the game",
    phoneOpenText: "On the phone, open this site's address in Chrome (Android) or Safari (iPhone) and tap Play.",
    phoneInstall: "Add it to the home screen",
    phoneInstallText: "Android, Chrome: menu ⋮, then \"Add to Home screen\" or \"Install app\". iPhone, Safari: the share button, then \"Add to Home Screen\". It then opens full screen from its icon.",
    phoneControls: "Play sideways",
    phoneControlsText: "Turn the phone sideways. Tap to walk, the ⚔ button fights the nearest monster, the 1–5 and Q W E R buttons use skills and potions, ⋯ opens the other windows (guild, friends, quests, missions, cash shop) and ⛶ goes full screen.",
    scheduleNav: "Schedule",
    levels: "levels",
    levelRange: "Character level",
    minutes: "Minutes",
    feeShort: "Fee",
    eventPlayers: "Players",
    showAll: "Show all",
    showLess: "Show fewer",
    scheduleTitle: "Starting in the next 24 hours.",
    ttAll: "All",
    ttEvents: "Events",
    ttInvasions: "Invasions",
    ttBosses: "Bosses",
    ttColName: "Event / Boss",
    ttColSydney: "Next (Sydney)",
    ttColLocal: "Your time",
    ttColCountdown: "Status",
    ttColMap: "Map",
    ttColDrops: "Drops",
    ttColSchedule: "Daily (Sydney)",
    ttActive: "Active now",
    ttNone: "No scheduled entries in this category.",
    ttOffline: "Full timetable is unavailable while this server is offline or the feature is disabled.",
    ttGmWinners: "Recent Find the GM winners",
    luckyNav: "Lucky number",
    luckyTitle: "Daily lucky number.",
    luckyOffline: "Daily lucky number is currently turned off or unavailable.",
    luckyDate: "Date",
    luckyWinning: "Winning #",
    luckyWinners: "Winners",
    luckyNone: "No draws recorded yet.",
    marriageNav: "Marriage",
    marriageTitle: "Married couples in the realm.",
    marriageOffline: "Marriage is currently turned off or unavailable.",
    marriagePair: "Couple",
    marriageCoupleTitle: "Title",
    marriageDate: "Married (Sydney)",
    marriageDays: "Days",
    marriageNone: "No married couples yet.",
    rankingsNav: "Rankings",
    rankingsTitle: "The realm's best.",
    topCharacters: "Top characters",
    eventRankings: "Event rankings",
    dropsNav: "Drops",
    dropsTitle: "What monsters drop.",
    dropsHint: "Configured drop groups: chance per kill",
    rank: "#",
    character: "Character",
    className: "Class",
    resets: "Resets",
    masterShort: "Master",
    score: "Score",
    games: "Games",
    dropGroup: "Drop group",
    chance: "Chance",
    monsterLevel: "Monster level",
    monster: "Monster",
    items: "Items",
    anyMonster: "Any",
    noUpcoming: "No event starts in the next 24 hours.",
    noRanking: "No entries yet.",
    partUnavailable: "Unavailable right now.",
    detailsOffline: "Rankings, schedule and drops are unavailable: the server didn't answer. Nothing below is shown from memory.",
    yourTime: "Your local time",
    accountNav: "Account",
    accountTitle: "Your game account.",
    registerTitle: "Create an account",
    loginName: "Account name (3–10 letters or digits)",
    password: "Password (3–20 characters)",
    email: "E-mail (optional)",
    registerButton: "Create account",
    googleTitle: "Sign in with Google",
    googleOff: "Google sign-in isn't set up on this server.",
    googleNew: "No game account uses this Google address yet. Choose an account name and a game password:",
    googleLinked: "Your game account:",
    newPassword: "New game password (optional)",
    setPassword: "Set new password",
    registered: "Account created. You can enter the game now.",
    passwordChanged: "Your new game password is set.",
    networkError: "The server didn't answer. Try again in a moment.",
    contact: "Community:",
    realm: "The realm",
    events: "Events",
    world: "World",
    start: "Get started",
    account: "Create account",
    eyebrow: "A world worth returning to",
    hero: "Your legend.\nYour next chapter.",
    intro:
      "Gather your party, explore the world, and begin a new adventure. Your journey starts here.",
    play: "Enter the game →",
    explore: "Explore the realm",
    browser: "Play directly in your browser with the game launcher.",
    live: "LIVE REALM",
    refresh: "Refresh ↻",
    source: "Direct from the game server",
    realmTitle: "Know your realm.",
    server: "Server",
    truth:
      "Rates and rules reflect the running server. No estimated statistics.",
    eventsTitle: "Events",
    eventTitle: "Answer the call.",
    eventHint: "Configured event rules · times are not estimated",
    worldTitle: "Find your place in the world.",
    classes: "Character classes",
    maps: "Explore the maps",
    search: "Search maps…",
    adventure: "The journey begins with you",
    startTitle: "Step into your next adventure.",
    create: "Create your account",
    createText: "Register through the game launcher’s account portal.",
    choose: "Choose your path",
    chooseText:
      "Sign in on the website and click Play now to go straight to server choice, or open the browser client directly.",
    download: "Take the realm with you",
    downloadText:
      "One download for Windows, Ubuntu and macOS. Unzip it and run Start HNMO.bat (Windows) or start-hnmo.sh (Ubuntu, macOS).",
    downloadLink: "Download HNMO →",
    marketNav: "Item Market",
    marketTitle: "Escrow Item Market",
    marketHint: "Safe player-to-player escrow market in-game (F6 or NPC 547 Julia) and on the website. Bought items arrive in your escrow mailbox in-game.",
    marketOffline: "Item Market data is unavailable: the server didn't answer.",
    marketOff: "Item Market is turned off on this server.",
    marketNone: "No items listed right now.",
    marketItem: "Item",
    marketSeller: "Seller",
    marketPrice: "Price",
    marketExpires: "Expires (Sydney)",
    footer: "Live game data. English & Tiếng Việt.",
    top: "Back to top ↑",
    unavailable: "Game data is unavailable. Start the game server and refresh.",
    loading: "Connecting to the realm…",
    online: "Connected",
    offline: "Unavailable",
    players: "Players online / capacity",
    experience: "Experience rate",
    maxLevel: "Maximum level",
    maxMaster: "Maximum master level",
    characters: "Characters per account",
    party: "Maximum party size",
    master: "Master experience",
    level: "Level",
    duration: "Duration (minutes)",
    capacity: "Player limit",
    fee: "Entrance fee",
    required: "Required level",
    empty: "No entries supplied by this server.",
    updated: "Updated",
    all: "Server",
    mapsRate: "Map experience",
    nameFallback: "Unnamed",
    noSchedule:
      "Event schedules and global rankings are not supplied by this feed.",
  },
  vi: {
    missionsNav: "Nhiệm vụ",
    missionsTitle: "Nhiệm vụ hôm nay",
    missionsHint: "Hoàn thành trong trò chơi và nhận thưởng trong cửa sổ nhiệm vụ (J). Nhiệm vụ hằng ngày bắt đầu lại mỗi ngày, nhiệm vụ hằng tuần vào thứ Hai.",
    missionsDaily: "Hằng ngày",
    missionsWeekly: "Hằng tuần",
    missionsReset: "Nhiệm vụ ngày mới sau",
    missionsOffline: "Không xem được nhiệm vụ: máy chủ không trả lời.",
    missionsNone: "Hiện chưa có nhiệm vụ nào.",
    missionsReward: "Phần thưởng",
    missionsStreak: "Hoàn thành nhiệm vụ ngày nhiều ngày liên tục: mỗi %d ngày được thưởng thêm (%s).",
    missionsCap: "Xu kiếm được (thời gian trực tuyến, mốc cấp độ, mời bạn, lịch đăng nhập): tối đa %d mỗi ngày cho mỗi tài khoản.",
    newNav: "Bắt đầu chơi",
    newTitle: "Nửa giờ đầu tiên",
    newHint: "Nhân vật mới nhận quà tân thủ khi đăng nhập lần đầu và có hướng dẫn trong trò chơi (Y): mỗi bước có phần thưởng, xong hết được thêm phần thưởng cuối. Bạn có thể bỏ qua bất cứ lúc nào.",
    newKit: "Quà tân thủ",
    newKitPerAccount: "Một lần mỗi tài khoản, cho nhân vật mới đầu tiên.",
    newKitPerCharacter: "Cho mỗi nhân vật mới.",
    newKitAll: "Mọi lớp",
    newKitPoints: "%d điểm cộng thêm",
    newKitBuff: "%s trong %d phút",
    newKitBuffPlain: "Bùa tăng kinh nghiệm trong %d phút",
    newSkill: "kỹ năng",
    newLuck: "may mắn",
    newStack: "chồng %d",
    newSteps: "Các bước hướng dẫn",
    newOptional: "không bắt buộc",
    newFinal: "Khi xong mọi bước",
    newOffline: "Không xem được hướng dẫn tân thủ: máy chủ không trả lời.",
    newOff: "Máy chủ này đang tắt hướng dẫn tân thủ và quà tân thủ.",
    newLevel: "Nhân vật tới cấp %d cũng được tính là mới.",
    bossesNav: "Boss thế giới",
    bossesTitle: "Boss thế giới",
    bossesHint: "Các boss mạnh xuất hiện theo lịch (giờ Sydney; giờ của bạn trong ngoặc). Mọi người gây sát thương đều được chia thưởng theo hạng: top 3, top 10, người đủ sát thương và đòn cuối. Không gây sát thương thì không có thưởng. Hạ boss được điểm săn boss cho bảng xếp hạng tuần.",
    bossesOffline: "Không xem được boss thế giới: máy chủ không trả lời.",
    bossesNone: "Hiện chưa có boss thế giới nào.",
    bossesOff: "Boss thế giới đang tắt trên máy chủ này.",
    bossesNext: "Lần xuất hiện tới",
    bossesIn: "sau",
    bossesYourTime: "Giờ của bạn",
    bossesAt: "Lúc",
    bossesAlive: "Đang ở trên bản đồ — rời đi sau",
    bossesMap: "Bản đồ",
    bossesTimes: "Mỗi ngày lúc",
    bossesEvery: "Mỗi %d giờ từ",
    bossesDays: "Vào",
    bossesRewardTop1: "Hạng 1",
    bossesRewardTop2: "Hạng 2",
    bossesRewardTop3: "Hạng 3",
    bossesRewardTop10: "Hạng 4–10",
    bossesRewardParticipation: "Mọi người có ít nhất %s% sát thương",
    bossesRewardLastHit: "Đòn cuối",
    bossesLastKill: "Lần hạ gần nhất",
    bossesLeft: "đã rời đi mà không bị hạ",
    bossesLastHit: "đòn cuối",
    bossesNews: "Thông báo",
    bossesWeek: "Xếp hạng tuần này",
    bossesWeekEnds: "Tuần kết thúc sau",
    bossesLastWeek: "Người thắng tuần trước",
    guildsNav: "Bang hội",
    guildsTitle: "Cấp bang hội và bảng xếp hạng tuần",
    guildsHint: "Thành viên nâng cấp bang hội bằng cách chơi: quái vật, sự kiện, boss thế giới, nhiệm vụ, thời gian trong trò chơi và công thành. Mỗi cấp cho thành viên đang chơi một hiệu ứng nhỏ. Mỗi thứ Hai lúc 00:00 (giờ Sydney) các bang đứng đầu tuần nhận thưởng cho mọi thành viên.",
    guildsOffline: "Không xem được bang hội: máy chủ không trả lời.",
    guildsOff: "Cấp bang hội đang tắt trên máy chủ này.",
    guildsLadder: "Bảng xếp hạng tuần này",
    guildsLastWeek: "Bang thắng tuần trước",
    guildsWeekOf: "Tuần từ",
    guildsAll: "Bang hội theo cấp",
    guildsSieges: "Kết quả công thành",
    guildsNoSieges: "Chưa có bang nào thắng công thành.",
    guildsLevels: "Cấp và hiệu ứng",
    guildsGuild: "Bang hội",
    guildsLevel: "Cấp",
    guildsPoints: "Điểm",
    guildsXp: "KN bang hội",
    guildsMembers: "Thành viên",
    guildsCoins: "Xu mỗi người",
    guildsBuff: "Hiệu ứng",
    guildsNone: "Chưa có bang nào có điểm.",
    guildsNoBuff: "không có",
    guildsExp: "KN",
    guildsZen: "zen từ quái vật",
    guildsDefense: "phòng thủ",
    guildsRewards: "Thưởng thứ Hai cho mỗi thành viên",
    guildsMaster: "bang chủ được thêm %d",
    guildsSources: "KN bang hội",
    guildsCap: "tối đa %d mỗi ngày cho mỗi thành viên",
    guildsWeekEnds: "Tuần kết thúc sau",
    guildsSourceMonsterKill: "một quái vật",
    guildsSourceEventFinish: "hoàn thành một sự kiện",
    guildsSourceEventWin: "thắng một sự kiện",
    guildsSourceWorldBossKill: "hạ một boss thế giới",
    guildsSourceWorldBossDamage: "mỗi % sát thương lên boss thế giới",
    guildsSourceMission: "một nhiệm vụ",
    guildsSourceOnlineTime: "mỗi %d phút trong trò chơi",
    guildsSourceCastleSiege: "thắng công thành",
    communityNav: "Cộng đồng",
    communityTitle: "Tham gia cộng đồng",
    communityHint: "Trò chuyện với người chơi khác, biết sự kiện sớm nhất và nhận xu miễn phí: một lần khi liên kết tài khoản với Discord, một lần khi tham gia nhóm Zalo. Sự kiện cộng đồng tặng thưởng cho người nhập mã hoặc người thắng.",
    communityJoin: "Tham gia:",
    communityCoins: "Cách nhận xu",
    communityEvents: "Sự kiện cộng đồng",
    communityHighlights: "Nổi bật gần đây",
    communityNoEvents: "Hiện không có sự kiện cộng đồng nào.",
    communityNoHighlights: "Chưa có gì: đồ hiếm, boss thế giới và tin bang hội sẽ hiện ở đây.",
    communityOffline: "Mục cộng đồng tạm thời không có: máy chủ không trả lời.",
    communityOff: "Phần thưởng cộng đồng đang tắt trên máy chủ này.",
    communityDiscordHow: "Discord: tham gia máy chủ, mở cửa sổ cộng đồng (K) trong trò chơi, lấy mã liên kết rồi gõ /link MÃ trong Discord. Bạn nhận %s một lần.",
    communityZaloHow: "Zalo: tham gia nhóm, yêu cầu trong cửa sổ cộng đồng (K) và đăng mã nhận được vào nhóm. Quản trị viên duyệt và bạn nhận %s một lần.",
    communityCodeHow: "Nhập mã sự kiện trong cửa sổ cộng đồng (K), một lần mỗi tài khoản.",
    communityWinnersHow: "Người thắng",
    communityReward: "Phần thưởng",
    communityOpen: "Đang diễn ra",
    communityUpcoming: "Bắt đầu",
    communityEnded: "Đã kết thúc",
    communityUntil: "đến",
    racesNav: "Đua top",
    racesTitle: "Đua top",
    racesHint: "Đua với người chơi khác theo ngày, tuần hoặc tháng: reset, cấp, săn boss, sự kiện và nhiều hơn nữa, có cuộc đua theo từng lớp. Các hạng cao nhất nhận xu và vật phẩm, trả một lần khi cuộc đua kết thúc. Trong trò chơi: cửa sổ đua top (F9).",
    racesRunning: "Đang diễn ra",
    racesNone: "Hiện không có cuộc đua top nào.",
    racesUpcoming: "Cuộc đua sắp tới",
    racesHistory: "Người thắng trước đây",
    racesNoHistory: "Chưa có cuộc đua nào kết thúc.",
    racesOffline: "Mục đua top tạm thời không có: máy chủ không trả lời.",
    racesOff: "Đua top đang tắt trên máy chủ này.",
    racesEnds: "Kết thúc sau %s",
    racesEndsAt: "%s giờ Sydney (%s giờ của bạn)",
    racesStarts: "Bắt đầu %s giờ Sydney (%s giờ của bạn)",
    racesPrizes: "Phần thưởng",
    racesMinimum: "Cần ít nhất %s để nhận thưởng.",
    racesNobody: "Chưa có ai.",
    racesNobodyTook: "không có ai tham gia",
    racesRank: "Hạng %d",
    racesRanks: "Hạng %d-%d",
    racesPlayer: "Người chơi",
    racesValue: "Giá trị",
    racesAll: "mọi lớp nhân vật",
    racesOnly: "chỉ %s",
    racesPerClass: "mỗi lớp một bảng",
    racesGuilds: "bang hội (mọi thành viên đều nhận thưởng)",
    racesDaily: "hằng ngày",
    racesWeekly: "hằng tuần",
    racesMonthly: "hằng tháng",
    racesCustom: "ngày đặc biệt",
    racesKindResetsGained: "Số lần reset",
    racesKindLevelsGained: "Số cấp tăng",
    racesKindLevelReached: "Cấp cao nhất đạt được",
    racesKindBossHuntPoints: "Điểm săn boss",
    racesKindPkKills: "Số lần hạ người chơi (PK)",
    racesKindEventWins: "Số lần thắng sự kiện",
    racesKindGuildPoints: "Điểm bang hội",
    racesKindMonsterKills: "Số quái bị hạ",
    racesKindOnlineMinutes: "Số phút trong trò chơi",
    racesClass0: "Phù Thủy",
    racesClass1: "Chiến Binh",
    racesClass2: "Tiên Nữ",
    racesClass3: "Đấu Sĩ",
    racesClass4: "Chúa Tể",
    racesClass5: "Thuật Sĩ",
    racesClass6: "Thiết Binh",
    giftNav: "Gift code",
    giftTitle: "Gift code",
    giftHint: "Gift code tặng coin, zen và vật phẩm. Nhập trong game bằng /code MÃ trong khung chat hoặc trong cửa sổ Gift code (cửa sổ cộng đồng K, hoặc menu \"thêm\" trên màn hình cảm ứng), hoặc trên trang tài khoản với tên tài khoản và mật khẩu. Mỗi mã có điều kiện riêng: thời gian (giờ Sydney), số lượt, một lần mỗi tài khoản hoặc mỗi nhân vật, cấp hoặc lớp nhân vật.",
    giftAccountLink: "Nhập mã trên trang tài khoản",
    giftNone: "Hiện chưa có gift code công khai: hãy theo dõi bài đăng cộng đồng.",
    giftOffline: "Mục gift code tạm thời không có: máy chủ không trả lời.",
    giftOff: "Gift code đang tắt trên máy chủ này.",
    giftUntil: "đến %s giờ Sydney (%s giờ của bạn)",
    giftFrom: "từ %s giờ Sydney (%s giờ của bạn)",
    giftLeft: "còn %s lượt",
    giftPerAccount: "một lần mỗi tài khoản",
    giftPerCharacter: "một lần mỗi nhân vật",
    giftLevels: "cấp %s",
    giftNoWebsite: "Trên máy chủ này, mã chỉ nhập được trong game.",
    resetsNav: "Reset",
    resetsTitle: "Xếp hạng reset và relife",
    resetsHint: "Số lần reset trong tháng (giờ Sydney) và relife (đại reset). Reset ở NPC reset hoặc bằng /reset, xem điều kiện lần tiếp theo bằng /reset info, relife bằng /relife, hoặc để máy chủ tự reset khi treo máy offline bằng /uythac on. Bạn cũng có thể reset, cộng hoặc tẩy điểm và bật/tắt ủy thác reset trên trang tài khoản.",
    resetsAccountLink: "Reset trên trang tài khoản",
    resetsMonth: "Tháng này (%s)",
    resetsPrevious: "Tháng trước (%s)",
    resetsRelifes: "Relife",
    resetsRules: "Các mức reset",
    resetsNone: "Chưa có lần reset nào.",
    resetsNoRelifes: "Chưa có relife nào.",
    resetsOffline: "Bảng xếp hạng reset tạm thời không có: máy chủ không trả lời.",
    resetsOff: "Hệ thống reset đang tắt trên máy chủ này.",
    resetsCount: "Reset",
    resetsRelifeCount: "Relife",
    resetsTierRange: "Reset %s",
    resetsTierLine: "cấp %s · %s · %s điểm (%s) · %s mỗi ngày",
    resetsModeTotal: "tổng",
    resetsModeAdd: "cộng thêm",
    resetsModeKeep: "giữ nguyên",
    resetsNoLimit: "không giới hạn",
    resetsFree: "miễn phí",
    resetsVipLine: "VIP: giảm %s cấp, giảm %s% zen, giảm %s% coin, thêm %s lần reset mỗi ngày.",
    resetsRelifeLine: "Relife sau %s lần reset ở cấp %s: %s; giữ %s% số reset, +%s điểm vĩnh viễn mỗi relife, tối đa %s.",
    bossesNoEntries: "Chưa có ai.",
    bossesRankBossHunt: "Săn boss",
    bossesRankBloodCastle: "Huyết Lâu",
    bossesRankDevilSquare: "Quảng Trường Quỷ",
    bossesRankChaosCastle: "Hỗn Nguyên Lâu",
    bossesCastleSiege: "Đội thắng công thành",
    bossesCastleSiegeText: "Mỗi thành viên của bang thắng công thành nhận %d xu vào cuối tuần.",
    bossesPrizes: "Giải thưởng tuần (top 10)",
    bossesRank: "Hạng",
    bossesName: "Nhân vật",
    bossesDamage: "Sát thương",
    bossesPoints: "Điểm",
    bossesCoins: "Xu",
    phoneNav: "Điện thoại",
    phoneTitle: "Chơi trên điện thoại",
    phoneIntro: "Trò chơi chạy trong trình duyệt của điện thoại và cài lên màn hình chính như một ứng dụng; không cần tải gì.",
    phoneOpen: "Mở trò chơi",
    phoneOpenText: "Trên điện thoại, mở địa chỉ trang này bằng Chrome (Android) hoặc Safari (iPhone) rồi bấm Chơi.",
    phoneInstall: "Thêm vào màn hình chính",
    phoneInstallText: "Android, Chrome: menu ⋮, rồi \"Thêm vào màn hình chính\" hoặc \"Cài đặt ứng dụng\". iPhone, Safari: nút chia sẻ, rồi \"Thêm vào MH chính\". Sau đó mở toàn màn hình từ biểu tượng.",
    phoneControls: "Chơi màn hình ngang",
    phoneControlsText: "Xoay ngang điện thoại. Chạm để đi, nút ⚔ đánh quái vật gần nhất, các nút 1–5 và Q W E R dùng kỹ năng và bình thuốc, ⋯ mở các cửa sổ khác (bang hội, bạn bè, nhiệm vụ, cửa hàng) và ⛶ để toàn màn hình.",
    scheduleNav: "Lịch sự kiện",
    levels: "cấp",
    levelRange: "Cấp nhân vật",
    minutes: "Phút",
    feeShort: "Phí",
    eventPlayers: "Người",
    showAll: "Xem tất cả",
    showLess: "Thu gọn",
    scheduleTitle: "Bắt đầu trong 24 giờ tới.",
    ttAll: "Tất cả",
    ttEvents: "Sự kiện",
    ttInvasions: "Xâm lăng",
    ttBosses: "Boss",
    ttColName: "Sự kiện / Boss",
    ttColSydney: "Giờ tới (Sydney)",
    ttColLocal: "Giờ máy bạn",
    ttColCountdown: "Trạng thái",
    ttColMap: "Bản đồ",
    ttColDrops: "Vật phẩm rơi",
    ttColSchedule: "Hàng ngày (Sydney)",
    ttActive: "Đang diễn ra",
    ttNone: "Không có mục nào trong danh mục này.",
    ttOffline: "Chưa tải được bảng lịch đầy đủ hoặc tính năng đang tắt.",
    ttGmWinners: "Người thắng Truy tìm GM gần đây",
    luckyNav: "Con số may mắn",
    luckyTitle: "Quay số may mắn hàng ngày.",
    luckyOffline: "Tính năng Con số may mắn hiện đang tắt hoặc máy chủ chưa trả lời.",
    luckyDate: "Ngày",
    luckyWinning: "Số trúng",
    luckyWinners: "Người trúng",
    luckyNone: "Chưa có kỳ quay nào.",
    marriageNav: "Kết hôn",
    marriageTitle: "Các cặp đôi trong lục địa.",
    marriageOffline: "Tính năng Kết hôn hiện đang tắt hoặc máy chủ chưa trả lời.",
    marriagePair: "Cặp đôi",
    marriageCoupleTitle: "Danh hiệu",
    marriageDate: "Ngày cưới (Sydney)",
    marriageDays: "Số ngày",
    marriageNone: "Chưa có cặp đôi nào kết hôn.",
    rankingsNav: "Xếp hạng",
    rankingsTitle: "Những người mạnh nhất.",
    topCharacters: "Nhân vật hàng đầu",
    eventRankings: "Xếp hạng sự kiện",
    dropsNav: "Rơi đồ",
    dropsTitle: "Quái vật rơi gì.",
    dropsHint: "Nhóm rơi đồ đã cấu hình: tỷ lệ mỗi lần hạ quái",
    rank: "#",
    character: "Nhân vật",
    className: "Lớp",
    resets: "Reset",
    masterShort: "Master",
    score: "Điểm",
    games: "Số trận",
    dropGroup: "Nhóm rơi đồ",
    chance: "Tỷ lệ",
    monsterLevel: "Cấp quái",
    monster: "Quái vật",
    items: "Vật phẩm",
    anyMonster: "Mọi quái",
    noUpcoming: "Không có sự kiện nào bắt đầu trong 24 giờ tới.",
    noRanking: "Chưa có dữ liệu.",
    partUnavailable: "Tạm thời chưa có.",
    detailsOffline: "Chưa lấy được xếp hạng, lịch và rơi đồ: máy chủ không trả lời. Không hiển thị số liệu cũ.",
    yourTime: "Giờ địa phương của bạn",
    accountNav: "Tài khoản",
    accountTitle: "Tài khoản trò chơi của bạn.",
    registerTitle: "Tạo tài khoản",
    loginName: "Tên tài khoản (3–10 chữ hoặc số)",
    password: "Mật khẩu (3–20 ký tự)",
    email: "E-mail (không bắt buộc)",
    registerButton: "Tạo tài khoản",
    googleTitle: "Đăng nhập bằng Google",
    googleOff: "Máy chủ này chưa bật đăng nhập bằng Google.",
    googleNew: "Chưa có tài khoản game nào dùng địa chỉ Google này. Hãy chọn tên tài khoản và mật khẩu game:",
    googleLinked: "Tài khoản game của bạn:",
    newPassword: "Mật khẩu game mới (không bắt buộc)",
    setPassword: "Đặt mật khẩu mới",
    registered: "Đã tạo tài khoản. Bạn có thể vào trò chơi ngay.",
    passwordChanged: "Đã đặt mật khẩu game mới.",
    networkError: "Máy chủ chưa trả lời. Hãy thử lại sau ít phút.",
    contact: "Cộng đồng:",
    realm: "Máy chủ",
    events: "Sự kiện",
    world: "Thế giới",
    start: "Bắt đầu",
    account: "Tạo tài khoản",
    eyebrow: "Thế giới luôn chờ bạn trở lại",
    hero: "Huyền thoại của bạn.\nHành trình mới.",
    intro:
      "Lập tổ đội, khám phá thế giới và bắt đầu cuộc phiêu lưu mới. Hành trình của bạn bắt đầu từ đây.",
    play: "Vào trò chơi →",
    explore: "Khám phá máy chủ",
    browser: "Chơi ngay trên trình duyệt thông qua trình khởi chạy.",
    live: "MÁY CHỦ TRỰC TIẾP",
    refresh: "Làm mới ↻",
    source: "Dữ liệu trực tiếp từ máy chủ trò chơi",
    realmTitle: "Tìm hiểu máy chủ.",
    server: "Máy chủ",
    truth:
      "Tỷ lệ và quy tắc lấy từ máy chủ đang chạy. Không dùng số liệu ước tính.",
    eventsTitle: "Sự kiện",
    eventTitle: "Sẵn sàng thử thách.",
    eventHint: "Quy tắc sự kiện từ máy chủ · không ước tính giờ mở",
    worldTitle: "Tìm vị trí của bạn trong thế giới.",
    classes: "Lớp nhân vật",
    maps: "Khám phá bản đồ",
    search: "Tìm bản đồ…",
    adventure: "Hành trình bắt đầu cùng bạn",
    startTitle: "Bước vào cuộc phiêu lưu mới.",
    create: "Tạo tài khoản",
    createText: "Đăng ký tại trang tài khoản của trình khởi chạy trò chơi.",
    choose: "Chọn con đường của bạn",
    chooseText: "Đăng nhập trên website và bấm Chơi ngay để vào thẳng phần chọn máy chủ, hoặc mở trò chơi trực tiếp trên trình duyệt.",
    download: "Mang theo thế giới của bạn",
    downloadText:
      "Một bản tải cho Windows, Ubuntu và macOS. Giải nén rồi chạy Start HNMO.bat (Windows) hoặc start-hnmo.sh (Ubuntu, macOS).",
    downloadLink: "Tải HNMO →",
    marketNav: "Chợ vật phẩm",
    marketTitle: "Chợ vật phẩm an toàn",
    marketHint: "Mua bán vật phẩm qua kho giữ đồ trong game (phím F6 hoặc NPC 547 Julia) và trên website. Đồ đã mua sẽ được chuyển vào Kho chợ trong game.",
    marketOffline: "Không xem được Chợ vật phẩm: máy chủ không trả lời.",
    marketOff: "Chợ vật phẩm đang tắt trên máy chủ này.",
    marketNone: "Hiện chưa có vật phẩm nào được rao bán.",
    marketItem: "Vật phẩm",
    marketSeller: "Người bán",
    marketPrice: "Giá",
    marketExpires: "Hết hạn (Sydney)",
    footer: "Dữ liệu trò chơi trực tiếp. English & Tiếng Việt.",
    top: "Về đầu trang ↑",
    unavailable:
      "Chưa lấy được dữ liệu. Hãy khởi động máy chủ trò chơi rồi làm mới.",
    loading: "Đang kết nối máy chủ…",
    online: "Đã kết nối",
    offline: "Chưa kết nối",
    players: "Người chơi trực tuyến / sức chứa",
    experience: "Tỷ lệ kinh nghiệm",
    maxLevel: "Cấp độ tối đa",
    maxMaster: "Cấp master tối đa",
    characters: "Số nhân vật mỗi tài khoản",
    party: "Số thành viên tổ đội tối đa",
    master: "Kinh nghiệm master",
    level: "Cấp độ",
    duration: "Thời lượng (phút)",
    capacity: "Số người tối đa",
    fee: "Phí tham gia",
    required: "Cấp độ yêu cầu",
    empty: "Máy chủ chưa cung cấp mục nào.",
    updated: "Cập nhật",
    all: "Máy chủ",
    mapsRate: "Kinh nghiệm bản đồ",
    nameFallback: "Chưa có tên",
    noSchedule:
      "Nguồn dữ liệu này chưa cung cấp lịch mở sự kiện và bảng xếp hạng toàn máy chủ.",
  },
};
type CopyKey = keyof typeof copy.en;
interface Named {
  name: string | null;
}
interface GameClass extends Named {
  id: number;
  requiredLevel: number;
}
interface GameMap extends Named {
  id: number;
  expMultiplier: number;
}
interface GameEvent extends Named {
  type?: string;
  typeName?: { en: string; vi: string };
  description: string | null;
  level: number;
  minimumLevel: number;
  maximumLevel: number;
  durationSeconds: number;
  capacity: number;
  entranceFee: number;
}
interface Realm {
  id: number;
  players: number;
  capacity: number;
  experienceRate: number;
  masterExperienceRate: number;
  maximumLevel: number;
  maximumMasterLevel: number;
  maximumCharactersPerAccount: number;
  maximumPartySize: number;
  classes: GameClass[];
  maps: GameMap[];
  events: GameEvent[];
}
interface Snapshot {
  updatedAt: string;
  servers: Realm[];
}
let language: Language = "en";
try {
  language = localStorage.getItem("website-language") === "vi" ? "vi" : "en";
} catch {
  /* Storage is optional. */
}
let snapshot: Snapshot | null = null;
let selected = "";
const REFRESH_MS = 30000;
function t(key: CopyKey): string {
  return copy[language][key];
}
function element(id: string): HTMLElement {
  return document.getElementById(id)!;
}
function node(tag: string, text: string, className = ""): HTMLElement {
  const item = document.createElement(tag);
  item.textContent = text;
  item.className = className;
  return item;
}
// The game's own Vietnamese texts (the web game's /data/lang/vi.json): class, event and other names the
// server sends only in English are shown with the game's Vietnamese name.
let gameVietnamese: Record<string, string> | null = null;
let gameVietnameseLoading = false;
function loadGameVietnamese(): void {
  if (gameVietnamese || gameVietnameseLoading) return;
  gameVietnameseLoading = true;
  fetch("/data/lang/vi.json", { cache: "force-cache" })
    .then((response) => (response.ok ? response.json() : null))
    .then((data: { texts?: Record<string, string> } | null) => {
      gameVietnamese = data?.texts ?? {};
      render();
      renderDetails();
    })
    .catch(() => { gameVietnamese = {}; });
}

function localized(value: string | null): string {
  const parts = (value ?? "").split("||");
  if (language === "vi" && parts.length === 1 && parts[0]) {
    loadGameVietnamese();
    const game = gameVietnamese?.[parts[0]];
    if (game) return game;
  }
  const translated = parts
    .slice(1)
    .find(
      (part) =>
        part.startsWith(`${language}=`) ||
        part.startsWith(`${language}-${language === "vi" ? "VN" : "US"}=`),
    );
  return (
    translated?.slice(translated.indexOf("=") + 1) ||
    parts[0] ||
    t("nameFallback")
  );
}
function clear(id: string): HTMLElement {
  const target = element(id);
  target.replaceChildren();
  return target;
}
function current(): Realm | undefined {
  return snapshot?.servers.find((server) => String(server.id) === selected);
}
function empty(id: string): void {
  clear(id).append(node("p", t("empty"), "muted"));
}
function renderStats(realm: Realm): void {
  const stats = clear("stats");
  const fields: [string, string][] = [
    [`${realm.players} / ${realm.capacity}`, t("players")],
    [`${realm.experienceRate}×`, t("experience")],
    [String(realm.maximumLevel), t("maxLevel")],
    [String(realm.maximumPartySize), t("party")],
    [`${realm.masterExperienceRate}×`, t("master")],
    [String(realm.maximumMasterLevel), t("maxMaster")],
    [String(realm.maximumCharactersPerAccount), t("characters")],
    [String(realm.maps.length), t("maps")],
  ];
  for (const [value, label] of fields) {
    const item = node("div", "", "stat");
    item.append(node("strong", value), node("span", label));
    stats.append(item);
  }
}
function renderEvents(realm: Realm): void {
  const target = clear("event-list");
  // One card per event (its levels in a table), named from the server's translations.
  const groups = new Map<string, GameEvent[]>();
  for (const event of realm.events) {
    const key = event.type ?? localized(event.name).replace(/\s*\d+$/, "");
    groups.set(key, [...(groups.get(key) ?? []), event]);
  }
  for (const [key, levels] of groups) {
    const first = levels[0]!;
    // Without a server translation the type reads "ImperialGuardian": spaced out.
    const title = (first.typeName ? first.typeName[language] : key).replace(/([a-z])([A-Z])/g, "$1 $2");
    const card = node("article", "", "event");
    card.append(node("span", `${levels.length} ${t("levels")}`, "number"), node("h3", title));
    const table = document.createElement("table");
    table.className = "table";
    const head = document.createElement("tr");
    const withFee = levels.some((event) => event.entranceFee > 0);
    const labels = [t("level"), t("levelRange"), t("minutes"), t("eventPlayers"), ...(withFee ? [t("feeShort")] : [])];
    labels.forEach((label, index) => head.append(node("th", label, index === 0 ? "" : "num")));
    table.append(head);
    for (const event of [...levels].sort((a, b) => a.level - b.level)) {
      const row = document.createElement("tr");
      row.append(
        node("td", String(event.level), "rank"),
        node("td", `${event.minimumLevel}–${event.maximumLevel}`, "num"),
        node("td", String(Math.round(event.durationSeconds / 60)), "num"),
        node("td", String(event.capacity), "num"),
      );
      if (withFee) row.append(node("td", event.entranceFee.toLocaleString(language), "num"));
      table.append(row);
    }
    const wrap = node("div", "", "table-wrap");
    wrap.append(table);
    card.append(wrap);
    target.append(card);
  }
  if (!realm.events.length) empty("event-list");
}
function renderMaps(): void {
  const realm = current();
  if (!realm) return;
  const query = (element("search") as HTMLInputElement).value.toLocaleLowerCase(
    language,
  );
  const maps = realm.maps.filter((map) =>
    localized(map.name).toLocaleLowerCase(language).includes(query),
  );
  const target = clear("maps");
  for (const map of maps) {
    const row = node("div", "", "map-row");
    row.append(
      node("span", localized(map.name)),
      node("small", `${map.expMultiplier}×`),
    );
    target.append(row);
  }
  if (!maps.length) empty("maps");
}
function render(): void {
  document.documentElement.lang = language;
  element("community").dataset.communityLinks = t("contact");
  element("social-links").dataset.communityLinks = t("communityJoin");
  showCommunityLinks();
  for (const item of document.querySelectorAll<HTMLElement>("[data-t]"))
    item.textContent = t(item.dataset.t as CopyKey);
  (element("search") as HTMLInputElement).placeholder = t("search");
  for (const lang of ["en", "vi"])
    element(lang).setAttribute("aria-pressed", String(language === lang));
  element("connection").textContent = t(snapshot ? "online" : "offline");
  element("notice").textContent = snapshot ? "" : t("unavailable");
  const select = element("server") as HTMLSelectElement;
  select.replaceChildren();
  for (const realm of snapshot?.servers ?? []) {
    const option = new Option(`${t("all")} ${realm.id}`, String(realm.id));
    select.add(option);
  }
  select.value = selected;
  const realm = current();
  if (!realm) {
    for (const id of ["stats", "event-list", "classes", "maps"]) empty(id);
    element("updated").textContent = "";
    return;
  }
  renderStats(realm);
  renderEvents(realm);
  renderMaps();
  const classes = clear("classes");
  for (const entry of realm.classes) {
    const row = node("div", "");
    row.append(
      node("span", localized(entry.name)),
      node("small", `${t("required")} ${entry.requiredLevel}`),
    );
    classes.append(row);
  }
  if (!realm.classes.length) empty("classes");
  element("updated").textContent =
    `${t("updated")}: ${new Date(snapshot!.updatedAt).toLocaleString(language, { timeZone: "Australia/Sydney" })} · Sydney`;
}
async function refresh(): Promise<void> {
  (element("refresh") as HTMLButtonElement).disabled = true;
  element("connection").textContent = t("loading");
  try {
    const response = await fetch("/website/data", {
      cache: "no-store",
      signal: AbortSignal.timeout(8000),
    });
    if (!response.ok) throw new Error("Server unavailable");
    const data: unknown = await response.json();
    if (
      typeof data !== "object" ||
      data === null ||
      !("servers" in data) ||
      !Array.isArray(data.servers) ||
      !("updatedAt" in data) ||
      typeof data.updatedAt !== "string"
    )
      throw new Error("Invalid snapshot");
    snapshot = data as Snapshot;
    if (!snapshot.servers.some((server) => String(server.id) === selected))
      selected = String(snapshot.servers[0]?.id ?? "");
  } catch {
    snapshot = null;
  } // Clear old numbers rather than present them as live.
  render();
  (element("refresh") as HTMLButtonElement).disabled = false;
}
for (const lang of ["en", "vi"] as const)
  element(lang).addEventListener("click", () => {
    language = lang;
    try {
      localStorage.setItem("website-language", lang);
    } catch {
      /* Optional storage. */
    }
    render();
  });
element("server").addEventListener("change", (event) => {
  selected = (event.target as HTMLSelectElement).value;
  render();
});
element("search").addEventListener("input", renderMaps);
element("refresh").addEventListener("click", () => void refresh());
render();
void loadGameName();
void refresh();
setInterval(() => {
  if (!document.hidden) void refresh();
}, REFRESH_MS);

// ---------- account: registration and Google sign-in ----------

interface PortalAnswer {
  error?: string;
  created?: boolean;
  needsAccount?: boolean;
  loginName?: string;
  passwordChanged?: boolean;
}

function message(id: string, text: string, kind: "ok" | "error" | "" = ""): void {
  const target = element(id);
  target.textContent = text;
  target.className = `form-message ${kind}`;
}

async function post(url: string, body: unknown): Promise<PortalAnswer> {
  try {
    const response = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
      signal: AbortSignal.timeout(15000),
    });
    const answer = (await response.json().catch(() => ({}))) as PortalAnswer;
    if (!response.ok && !answer.error) answer.error = t("networkError");
    return answer;
  } catch {
    return { error: t("networkError") };
  }
}

function field(form: HTMLFormElement, name: string): string {
  return (form.elements.namedItem(name) as HTMLInputElement | null)?.value.trim() ?? "";
}

element("register").addEventListener("submit", (event) => {
  event.preventDefault();
  const form = event.currentTarget as HTMLFormElement;
  void post("/portal/register", {
    loginName: field(form, "loginName"),
    password: (form.elements.namedItem("password") as HTMLInputElement).value,
    eMail: field(form, "email") || null,
  }).then((answer) => {
    if (answer.error) message("register-message", answer.error, "error");
    else {
      message("register-message", t("registered"), "ok");
      form.reset();
    }
  });
});

// Google Identity Services: loaded only when the host set a client id in the admin panel.
interface GoogleIdentity {
  accounts: {
    id: {
      initialize(options: { client_id: string; callback: (response: { credential: string }) => void }): void;
      renderButton(parent: HTMLElement, options: Record<string, string | number>): void;
    };
  };
}
let googleCredential = "";

function showGoogleAnswer(answer: PortalAnswer): void {
  element("google-account").hidden = !answer.needsAccount;
  element("google-linked").hidden = !answer.loginName;
  if (answer.loginName) element("google-login").textContent = answer.loginName;
  if (answer.error) message("google-message", answer.error, "error");
  else if (answer.created) message("google-message", t("registered"), "ok");
  else if (answer.passwordChanged) message("google-message", t("passwordChanged"), "ok");
  else message("google-message", "");
}

async function setUpGoogle(): Promise<void> {
  let clientId = "";
  try {
    const response = await fetch("/website/settings", { cache: "no-cache" });
    if (response.ok) clientId = ((await response.json()) as { googleClientId?: string }).googleClientId ?? "";
  } catch {
    /* Without settings there's no Google sign-in. */
  }
  if (!clientId) return;
  const script = document.createElement("script");
  script.src = "https://accounts.google.com/gsi/client";
  script.async = true;
  script.onload = () => {
    const google = (window as unknown as { google?: GoogleIdentity }).google;
    if (!google) return;
    google.accounts.id.initialize({
      client_id: clientId,
      callback: (response) => {
        googleCredential = response.credential;
        void post("/portal/google", { credential: googleCredential }).then(showGoogleAnswer);
      },
    });
    const target = element("google-button");
    target.hidden = false;
    element("google-off").hidden = true;
    google.accounts.id.renderButton(target, { theme: "filled_black", size: "large", text: "signin_with", locale: language });
  };
  document.head.append(script);
}

element("google-account").addEventListener("submit", (event) => {
  event.preventDefault();
  const form = event.currentTarget as HTMLFormElement;
  void post("/portal/google", {
    credential: googleCredential,
    loginName: field(form, "loginName"),
    password: (form.elements.namedItem("password") as HTMLInputElement).value,
  }).then((answer) => {
    if (answer.created) {
      showGoogleAnswer({ loginName: field(form, "loginName"), created: true });
    } else showGoogleAnswer(answer);
  });
});

element("google-linked").addEventListener("submit", (event) => {
  event.preventDefault();
  const form = event.currentTarget as HTMLFormElement;
  const newPassword = (form.elements.namedItem("newPassword") as HTMLInputElement).value;
  if (!newPassword) return;
  void post("/portal/google", { credential: googleCredential, newPassword }).then(showGoogleAnswer);
});

document.getElementById("google-play")?.addEventListener("click", (event) => {
  if (!googleCredential) return;
  event.preventDefault();
  void post("/portal/google-ticket", { credential: googleCredential }).then((answer) => {
    const ticket = (answer as { ticket?: string }).ticket;
    const loginName = answer.loginName;
    if (ticket && loginName) {
      location.href = `/index.html#ticket=${encodeURIComponent(ticket)}&account=${encodeURIComponent(loginName)}`;
    } else if (answer.error) {
      message("google-message", answer.error, "error");
    }
  });
});

// After the page has loaded: a slow or blocked Google script must not hold up the page.
if (document.readyState === "complete") void setUpGoogle();
else window.addEventListener("load", () => void setUpGoogle(), { once: true });

// ---------- the game loads in the background ----------
// While the website is open, the browser fetches the game's code and its first, largest data files
// into its cache, so that "Enter the game" starts quickly. Low priority, after the page is idle.
function preloadGame(): void {
  const prefetch = (href: string, as: string) => {
    const link = document.createElement("link");
    link.rel = as === "script" ? "modulepreload" : "prefetch";
    link.href = href;
    if (as !== "script") link.as = as;
    document.head.append(link);
  };
  void fetch("/index.html", { cache: "force-cache" })
    .then((response) => (response.ok ? response.text() : ""))
    .then((page) => {
      const entry = /<script[^>]+type="module"[^>]+src="([^"]+)"/.exec(page)?.[1] ?? /src="(\/js\/main[^"]*\.js)"/.exec(page)?.[1];
      if (entry) prefetch(entry, "script");
      for (const file of ["/data/Player/player.bmd", "/data/Monsters/Monsters.json", "/data/Monsters/Models.json"]) prefetch(file, "fetch");
    })
    .catch(() => undefined);
}
const idle = (window as unknown as { requestIdleCallback?: (callback: () => void) => void }).requestIdleCallback;
if (idle) idle(preloadGame);
else setTimeout(preloadGame, 1500);

// ---------- schedule, rankings and drops (read from the server, /website/details) ----------

interface RankedCharacter { rank: number; name: string; className: string; level: number; masterLevel: number; resets: number }
interface EventRanking { type: string; entries: { rank: number; name: string; className: string; score: number; games: number }[] }
interface UpcomingEvent { name: string; nameVi: string; startsAt: string }
interface DropGroup { name: string | null; chancePercent: number; minimumMonsterLevel: number | null; maximumMonsterLevel: number | null; monster: string | null; itemCount: number }
interface Details {
  characters: RankedCharacter[] | null;
  eventRankings: EventRanking[] | null;
  upcoming: { serverId: number; events: UpcomingEvent[] }[] | null;
  drops: DropGroup[] | null;
}
let details: Details | null = null;
let showAllDrops = false;
let eventType = "";
const EVENT_NAMES: Record<string, [string, string]> = {
  BloodCastle: ["Blood Castle", "Huyết Lâu"],
  DevilSquare: ["Devil Square", "Quảng Trường Quỷ"],
  ChaosCastle: ["Chaos Castle", "Hỗn Nguyên Lâu"],
  IllusionTemple: ["Illusion Temple", "Đền Ảo Ảnh"],
};

function table(id: string, headers: [string, boolean][], rows: (string | number)[][], emptyText: string): void {
  const target = clear(id);
  if (!rows.length) {
    const row = document.createElement("tr");
    const cell = node("td", emptyText, "muted") as HTMLTableCellElement;
    cell.colSpan = Math.max(1, headers.length);
    row.append(cell);
    target.append(row);
    return;
  }
  const head = document.createElement("tr");
  for (const [label, numeric] of headers) head.append(node("th", label, numeric ? "num" : ""));
  target.append(head);
  for (const values of rows) {
    const row = document.createElement("tr");
    values.forEach((value, index) => row.append(node("td", String(value), index === 0 ? "rank" : headers[index]?.[1] ? "num" : "")));
    target.append(row);
  }
}

function renderDetails(): void {
  element("details-notice").textContent = details ? "" : t("detailsOffline");
  const zone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  element("timezone").textContent = `${t("yourTime")}: ${zone}`;
  // Schedule of the chosen server.
  const upcomingTarget = clear("upcoming");
  const events = details?.upcoming?.find((entry) => String(entry.serverId) === selected)?.events ?? (details?.upcoming ? [] : null);
  if (events === null) upcomingTarget.append(node("p", details ? t("partUnavailable") : "", "muted"));
  else if (!events.length) upcomingTarget.append(node("p", t("noUpcoming"), "muted"));
  for (const event of events ?? []) {
    const slot = node("div", "", "slot");
    const time = node("time", new Date(event.startsAt).toLocaleTimeString(language, { hour: "2-digit", minute: "2-digit" }));
    time.setAttribute("datetime", event.startsAt);
    slot.append(time, node("span", language === "vi" ? event.nameVi : event.name), node("small", new Date(event.startsAt).toLocaleDateString(language, { weekday: "short", day: "numeric", month: "short" })));
    upcomingTarget.append(slot);
  }
  // Character ranking.
  const characters = details?.characters;
  if (characters === null || characters === undefined) table("ranking", [], [], details ? t("partUnavailable") : "");
  else table("ranking", [[t("rank"), false], [t("character"), false], [t("className"), false], [t("level"), true], [t("masterShort"), true], [t("resets"), true]],
    characters.map((c) => [c.rank, c.name, localized(c.className), c.level, c.masterLevel, c.resets]), t("noRanking"));
  // Event rankings.
  const select = element("event-ranking") as HTMLSelectElement;
  const boards = details?.eventRankings ?? [];
  if (!boards.some((board) => board.type === eventType)) eventType = boards[0]?.type ?? "";
  select.replaceChildren(...boards.map((board) => new Option(EVENT_NAMES[board.type]?.[language === "vi" ? 1 : 0] ?? board.type, board.type)));
  select.value = eventType;
  const board = boards.find((entry) => entry.type === eventType);
  if (!details?.eventRankings) table("event-table", [], [], details ? t("partUnavailable") : "");
  else table("event-table", [[t("rank"), false], [t("character"), false], [t("className"), false], [t("score"), true], [t("games"), true]],
    (board?.entries ?? []).map((e) => [e.rank, e.name, localized(e.className), e.score, e.games]), t("noRanking"));
  // Drops.
  const drops = details?.drops;
  if (drops === null || drops === undefined) table("drop-table", [], [], details ? t("partUnavailable") : "");
  else table("drop-table", [[t("dropGroup"), false], [t("chance"), true], [t("monsterLevel"), false], [t("monster"), false], [t("items"), true]],
    (showAllDrops ? drops : drops.slice(0, 15)).map((d) => [localized(d.name), `${d.chancePercent}%`,
      d.minimumMonsterLevel === null && d.maximumMonsterLevel === null ? "—" : `${d.minimumMonsterLevel ?? 0}–${d.maximumMonsterLevel ?? "∞"}`,
      d.monster ? localized(d.monster) : t("anyMonster"), d.itemCount]), t("noRanking"));
  const more = element("drops-more");
  more.hidden = !drops || drops.length <= 15;
  more.textContent = t(showAllDrops ? "showLess" : "showAll");
}

async function refreshDetails(): Promise<void> {
  try {
    const response = await fetch("/website/details", { cache: "no-store", signal: AbortSignal.timeout(12000) });
    if (!response.ok) throw new Error("Server unavailable");
    const data = (await response.json()) as Details;
    if (typeof data !== "object" || data === null) throw new Error("Invalid details");
    details = data;
  } catch {
    details = null; // never show old rankings as current
  }
  renderDetails();
}

element("drops-more").addEventListener("click", () => {
  showAllDrops = !showAllDrops;
  renderDetails();
});
element("event-ranking").addEventListener("change", (event) => {
  eventType = (event.target as HTMLSelectElement).value;
  renderDetails();
});
for (const id of ["en", "vi"]) element(id).addEventListener("click", () => renderDetails());
element("server").addEventListener("change", () => renderDetails());
element("refresh").addEventListener("click", () => void refreshDetails());
void refreshDetails();
setInterval(() => {
  if (!document.hidden) void refreshDetails();
}, 60000);

// ---------- missions (read from the server, /website/missions) ----------

interface PublicReward { zen: number; coins: number; coinType: string; items: { group: number; number: number; level: number; count: number; name?: string | null }[] }
interface PublicMission { id: string; period: "daily" | "weekly"; kind: string; titleEn: string; titleVi: string; target: number; reward: PublicReward }
interface PublicMissions { missions: PublicMission[]; nextDaily?: number; streak?: { bonusDay: number; bonus: PublicReward }; earnedDailyCap?: number }
let missions: PublicMissions | null = null;

function coinLabel(type: string): string {
  return type === "GoblinPoints" ? "Goblin Points" : type === "WCoinP" ? "W Coin (P)" : "W Coin (C)";
}

function rewardLine(reward: PublicReward): string {
  const parts: string[] = [];
  const format = (value: number) => value.toLocaleString(language === "vi" ? "vi-VN" : "en-AU");
  if (reward.zen > 0) parts.push(`${format(reward.zen)} Zen`);
  if (reward.coins > 0) parts.push(`${format(reward.coins)} ${coinLabel(reward.coinType)}`);
  for (const item of reward.items) parts.push(`${item.name ? localized(item.name) : `${item.group}/${item.number}`}${item.level > 0 ? ` +${item.level}` : ""} x${item.count}`);
  return parts.join(" · ");
}

function renderMissions(): void {
  element("missions-notice").textContent = missions ? "" : t("missionsOffline");
  for (const period of ["daily", "weekly"] as const) {
    const target = clear(`missions-${period}`);
    const list = missions?.missions.filter((mission) => mission.period === period) ?? [];
    if (missions && !list.length) target.append(node("p", t("missionsNone"), "muted"));
    for (const mission of list) {
      const card = node("article", "", "mission");
      card.dataset.mission = mission.id;
      const reward = rewardLine(mission.reward);
      card.append(node("h4", (language === "vi" ? mission.titleVi : mission.titleEn) || mission.titleEn));
      if (reward) card.append(node("p", `${t("missionsReward")}: ${reward}`));
      target.append(card);
    }
  }
  const minutes = missions?.nextDaily ? Math.max(0, Math.ceil((missions.nextDaily - Date.now()) / 60000)) : null;
  element("missions-reset").textContent = minutes === null ? "" : `${t("missionsReset")} ${Math.floor(minutes / 60)} h ${minutes % 60} min`;
  const extra: string[] = [];
  if (missions?.streak) extra.push(t("missionsStreak").replace("%d", String(missions.streak.bonusDay)).replace("%s", rewardLine(missions.streak.bonus)));
  if (missions?.earnedDailyCap) extra.push(t("missionsCap").replace("%d", String(missions.earnedDailyCap)));
  element("missions-extra").textContent = extra.join(" ");
}

async function refreshMissions(): Promise<void> {
  try {
    const response = await fetch("/website/missions", { cache: "no-store", signal: AbortSignal.timeout(10000) });
    if (!response.ok) throw new Error("Server unavailable");
    const data = (await response.json()) as PublicMissions;
    if (typeof data !== "object" || data === null || !Array.isArray(data.missions)) throw new Error("Invalid missions");
    missions = data;
  } catch {
    missions = null;
  }
  renderMissions();
}

for (const id of ["en", "vi"]) element(id).addEventListener("click", () => renderMissions());
element("refresh").addEventListener("click", () => void refreshMissions());
void refreshMissions();
setInterval(() => {
  if (!document.hidden) void refreshMissions();
}, 5 * 60000);

// ---------- new players (read from the server, /website/onboarding) ----------

interface PublicKitItem { group: number; number: number; level: number; count: number; durability: number; skill: boolean; luck: boolean; optionLevel: number; excellent: number; classNumber: number; className?: string | null; name?: string | null }
interface PublicStep { id: string; kind: string; titleEn: string; titleVi: string; hintEn: string; hintVi: string; target: number; optional: boolean; reward: PublicReward }
interface PublicOnboarding {
  enabled: boolean; guideEnabled?: boolean; newCharacterMaxLevel?: number; steps?: PublicStep[] | null; final?: PublicReward | null;
  kit?: { perAccount: boolean; zen: number; coins: number; coinType: string; points: number; buffMinutes: number; buffName?: string | null; items: PublicKitItem[] } | null;
}
let onboarding: PublicOnboarding | null = null;

function kitItemLine(item: PublicKitItem): string {
  const extras: string[] = [];
  if (item.skill) extras.push(t("newSkill"));
  if (item.luck) extras.push(t("newLuck"));
  if (item.optionLevel > 0) extras.push(`+${item.optionLevel * 4}`);
  if (item.group === 14 && item.durability > 1) extras.push(t("newStack").replace("%d", String(item.durability)));
  const name = item.name ? localized(item.name) : `${item.group}/${item.number}`;
  return `${name}${item.level > 0 ? ` +${item.level}` : ""}${item.count > 1 ? ` x${item.count}` : ""}${extras.length ? ` (${extras.join(", ")})` : ""}`;
}

function renderOnboarding(): void {
  const notice = element("newplayers-notice");
  const kitTarget = clear("newplayers-kit");
  const stepsTarget = clear("newplayers-steps");
  element("newplayers-extra").textContent = "";
  if (!onboarding) { notice.textContent = t("newOffline"); return; }
  if (!onboarding.enabled || (!onboarding.kit && !onboarding.steps)) { notice.textContent = t("newOff"); return; }
  notice.textContent = "";
  const kit = onboarding.kit;
  if (kit) {
    const card = node("article", "", "mission newplayers-kit");
    card.append(node("h4", t("newKit")));
    card.append(node("p", kit.perAccount ? t("newKitPerAccount") : t("newKitPerCharacter")));
    const groups = new Map<string, PublicKitItem[]>();
    for (const item of kit.items) {
      const key = item.classNumber < 0 ? t("newKitAll") : localized(item.className ?? `#${item.classNumber}`);
      groups.set(key, [...(groups.get(key) ?? []), item]);
    }
    for (const [label, items] of groups) {
      const line = node("p", `${label}: ${items.map(kitItemLine).join(" · ")}`);
      line.dataset.kitClass = label;
      card.append(line);
    }
    const extra = rewardLine({ zen: kit.zen, coins: kit.coins, coinType: kit.coinType, items: [] });
    if (extra) card.append(node("p", extra));
    if (kit.points > 0) card.append(node("p", t("newKitPoints").replace("%d", String(kit.points))));
    if (kit.buffMinutes > 0) {
      card.append(node("p", kit.buffName
        ? t("newKitBuff").replace("%s", localized(kit.buffName)).replace("%d", String(kit.buffMinutes))
        : t("newKitBuffPlain").replace("%d", String(kit.buffMinutes))));
    }
    kitTarget.append(card);
  }
  const steps = onboarding.steps ?? [];
  element("newplayers-steps-head").hidden = steps.length === 0;
  steps.forEach((step, index) => {
    const card = node("article", "", "mission");
    card.dataset.step = step.id;
    card.append(node("h4", `${index + 1}. ${(language === "vi" ? step.titleVi : step.titleEn) || step.titleEn}${step.optional ? ` (${t("newOptional")})` : ""}`));
    const hint = (language === "vi" ? step.hintVi : step.hintEn) || step.hintEn;
    if (hint) card.append(node("p", hint));
    const reward = rewardLine(step.reward);
    if (reward) card.append(node("p", `${t("missionsReward")}: ${reward}`));
    stepsTarget.append(card);
  });
  const extra: string[] = [];
  const final = onboarding.final ? rewardLine(onboarding.final) : "";
  if (steps.length && final) extra.push(`${t("newFinal")}: ${final}.`);
  if (onboarding.newCharacterMaxLevel) extra.push(t("newLevel").replace("%d", String(onboarding.newCharacterMaxLevel)));
  element("newplayers-extra").textContent = extra.join(" ");
}

async function refreshOnboarding(): Promise<void> {
  try {
    const response = await fetch("/website/onboarding", { cache: "no-store", signal: AbortSignal.timeout(10000) });
    if (!response.ok) throw new Error("Server unavailable");
    const data = (await response.json()) as PublicOnboarding;
    if (typeof data !== "object" || data === null || typeof data.enabled !== "boolean") throw new Error("Invalid onboarding");
    onboarding = data;
  } catch {
    onboarding = null;
  }
  renderOnboarding();
}

for (const id of ["en", "vi"]) element(id).addEventListener("click", () => renderOnboarding());
element("refresh").addEventListener("click", () => void refreshOnboarding());
void refreshOnboarding();
setInterval(() => {
  if (!document.hidden) void refreshOnboarding();
}, 5 * 60000);

// ---------- world bosses (read from the server, /website/world-bosses) ----------

interface BossTop { rank: number; name: string; damage: number; share: number }
interface BossKill { bossId: string; nameEn: string; nameVi: string; endedAt: number; killed: boolean; lastHit?: string | null; fighters: number; top: BossTop[] }
interface PublicBoss {
  id: string; nameEn: string; nameVi: string; map: number; mapName?: string | null; schedule: "every" | "fixed"; everyHours: number;
  times: string; days: string[]; nextSpawn?: number | null; despawnMinutes: number;
  alive?: { x: number; y: number; spawnedAt: number; despawnAt: number; fighters: number } | null;
  rewards: { top1: PublicReward; top2: PublicReward; top3: PublicReward; top10: PublicReward; participation: PublicReward; lastHit: PublicReward; participationMinimumPercent: number };
  lastKill?: BossKill | null;
}
interface RankLine { rank: number; name: string; points: number; coins?: number; coinType?: string }
interface PublicBosses {
  off?: boolean; now: number; bosses: PublicBoss[];
  announcements?: { at: number; phase: string; textEn: string; textVi: string }[];
  week?: { ends: number; rankings: Record<string, RankLine[]>; castleSiegeGuild?: string | null };
  prizes?: Record<string, { enabled: boolean; coinType: string; ranks: { rank: number; coins: number }[] }>;
  castleSiege?: { enabled: boolean; coinType: string; coinsPerMember: number };
  lastWeek?: { week: string; winners: Record<string, RankLine[]>; castleSiegeGuild?: string | null } | null;
}
let bosses: PublicBosses | null = null;
let bossesSkew = 0;
const RANKING_KEYS: [string, CopyKey][] = [["BossHunt", "bossesRankBossHunt"], ["BloodCastle", "bossesRankBloodCastle"], ["DevilSquare", "bossesRankDevilSquare"], ["ChaosCastle", "bossesRankChaosCastle"]];

function bossName(boss: { nameEn: string; nameVi: string }): string {
  return (language === "vi" ? boss.nameVi : boss.nameEn) || boss.nameEn;
}

/** "Sat 20:00" in Sydney or the visitor's own time zone. */
function bossClock(at: number, timeZone?: string): string {
  return new Intl.DateTimeFormat(language === "vi" ? "vi-VN" : "en-AU", { weekday: "short", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit", hour12: false, ...(timeZone ? { timeZone } : {}) }).format(at);
}

function bossCountdown(at: number): string {
  const seconds = Math.max(0, Math.round((at - (Date.now() + bossesSkew)) / 1000));
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  return hours > 0 ? `${hours} h ${String(minutes).padStart(2, "0")} min` : `${minutes}:${String(seconds % 60).padStart(2, "0")}`;
}

function dayName(day: string): string {
  const index = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"].indexOf(day);
  if (index < 0) return day;
  // 2026-11-15 was a Sunday.
  return new Intl.DateTimeFormat(language === "vi" ? "vi-VN" : "en-AU", { weekday: "short", timeZone: "UTC" }).format(Date.UTC(2026, 10, 15 + index));
}

function rankList(lines: RankLine[], withCoins: boolean): HTMLElement {
  const list = document.createElement("ol");
  if (!lines.length) return node("p", t("bossesNoEntries"), "muted");
  const format = (value: number) => value.toLocaleString(language === "vi" ? "vi-VN" : "en-AU");
  for (const line of lines) {
    const coins = withCoins && line.coins ? ` · ${format(line.coins)} ${coinLabel(line.coinType ?? "GoblinPoints")}` : "";
    list.append(node("li", `${line.name} — ${format(line.points)} ${t("bossesPoints").toLowerCase()}${coins}`));
  }
  return list;
}

function renderBosses(): void {
  const data = bosses;
  element("bosses-notice").textContent = data ? (data.off ? t("bossesOff") : "") : t("bossesOffline");
  const list = clear("bosses-list");
  if (data && !data.off && !data.bosses.length) list.append(node("p", t("bossesNone"), "muted"));
  const format = (value: number) => value.toLocaleString(language === "vi" ? "vi-VN" : "en-AU");
  for (const boss of data?.bosses ?? []) {
    const card = node("article", "", boss.alive ? "boss alive" : "boss");
    card.dataset.boss = boss.id;
    card.append(node("h4", bossName(boss)));
    card.append(node("p", `${t("bossesMap")}: ${boss.mapName ? localized(boss.mapName) : boss.map}`));
    if (boss.alive) {
      card.append(node("p", `${t("bossesAlive")} ${bossCountdown(boss.alive.despawnAt)} (${boss.alive.x}, ${boss.alive.y})`, "boss-when"));
    } else if (boss.nextSpawn) {
      const when = node("p", `${t("bossesNext")}: ${bossClock(boss.nextSpawn, "Australia/Sydney")} (Sydney) · ${t("bossesIn")} ${bossCountdown(boss.nextSpawn)}`, "boss-when");
      when.dataset.next = String(boss.nextSpawn);
      card.append(when);
      card.append(node("p", `${t("bossesYourTime")}: ${bossClock(boss.nextSpawn)}`));
    }
    const schedule = boss.schedule === "every" ? `${t("bossesEvery").replace("%d", String(boss.everyHours))} ${boss.times.split(",")[0]}` : `${t(boss.days.length ? "bossesAt" : "bossesTimes")} ${boss.times}`;
    card.append(node("p", boss.days.length ? `${schedule} · ${t("bossesDays")} ${boss.days.map(dayName).join(", ")}` : schedule));
    const rewards = document.createElement("ul");
    const tiers: [CopyKey, PublicReward][] = [["bossesRewardTop1", boss.rewards.top1], ["bossesRewardTop2", boss.rewards.top2], ["bossesRewardTop3", boss.rewards.top3], ["bossesRewardTop10", boss.rewards.top10], ["bossesRewardParticipation", boss.rewards.participation], ["bossesRewardLastHit", boss.rewards.lastHit]];
    for (const [key, reward] of tiers) {
      const line = rewardLine(reward);
      if (line) rewards.append(node("li", `${t(key).replace("%s", String(boss.rewards.participationMinimumPercent))}: ${line}`));
    }
    card.append(rewards);
    const kill = boss.lastKill;
    if (kill) {
      const head = kill.killed
        ? `${t("bossesLastKill")}: ${bossClock(kill.endedAt, "Australia/Sydney")} (Sydney)${kill.lastHit ? ` · ${t("bossesLastHit")}: ${kill.lastHit}` : ""}`
        : `${t("bossesLastKill")}: ${bossClock(kill.endedAt, "Australia/Sydney")} (Sydney) · ${t("bossesLeft")}`;
      card.append(node("p", head));
      const top = document.createElement("ol");
      top.className = "boss-top";
      for (const line of kill.top) top.append(node("li", `${line.name} — ${format(line.damage)} (${line.share}%)`));
      if (kill.top.length) card.append(top);
    }
    list.append(card);
  }

  const news = clear("bosses-news");
  for (const item of data?.announcements ?? []) news.append(node("li", `${bossClock(item.at, "Australia/Sydney")} — ${(language === "vi" ? item.textVi : item.textEn) || item.textEn}`));

  element("bosses-week-ends").textContent = data?.week ? `${t("bossesWeekEnds")} ${bossCountdown(data.week.ends)}` : "";
  for (const [target, rankings, withCoins] of [["bosses-week", data?.week?.rankings, false], ["bosses-last-week", data?.lastWeek?.winners, true]] as const) {
    const box = clear(target);
    if (!data || data.off) continue;
    for (const [key, label] of RANKING_KEYS) {
      const prize = data.prizes?.[key];
      if (prize && !prize.enabled && !withCoins) continue;
      const column = node("div", "", "boss-ranking");
      column.dataset.ranking = key;
      column.append(node("h4", t(label)));
      column.append(rankList(rankings?.[key] ?? [], withCoins));
      if (!withCoins && prize?.enabled && prize.ranks.length) {
        column.append(node("p", `${t("bossesPrizes")}: ${prize.ranks.map((r) => `${r.rank}. ${format(r.coins)}`).join(", ")} ${coinLabel(prize.coinType)}`, "muted"));
      }
      box.append(column);
    }
  }
  const siege: string[] = [];
  if (data?.castleSiege?.enabled) siege.push(`${t("bossesCastleSiege")}: ${t("bossesCastleSiegeText").replace("%d", format(data.castleSiege.coinsPerMember))}`);
  if (data?.week?.castleSiegeGuild) siege.push(`${t("bossesWeek")}: ${data.week.castleSiegeGuild}`);
  if (data?.lastWeek?.castleSiegeGuild) siege.push(`${t("bossesLastWeek")}: ${data.lastWeek.castleSiegeGuild}`);
  element("bosses-siege").textContent = siege.join(" · ");
}

async function refreshBosses(): Promise<void> {
  try {
    const response = await fetch("/website/world-bosses", { cache: "no-store", signal: AbortSignal.timeout(10000) });
    if (!response.ok) throw new Error("Server unavailable");
    const data = (await response.json()) as PublicBosses;
    if (typeof data !== "object" || data === null || !Array.isArray(data.bosses)) throw new Error("Invalid world bosses");
    bosses = data;
    bossesSkew = typeof data.now === "number" ? data.now - Date.now() : 0;
  } catch {
    bosses = null;
  }
  renderBosses();
}

for (const id of ["en", "vi"]) element(id).addEventListener("click", () => renderBosses());
element("refresh").addEventListener("click", () => void refreshBosses());
void refreshBosses();
setInterval(() => {
  if (!document.hidden) void refreshBosses();
}, 60000);
// The countdowns.
setInterval(() => {
  if (!document.hidden && bosses) renderBosses();
}, 1000);

// ---------- guilds (read from the server, /website/guilds) ----------

interface GuildBonus { level: number; xp: number; exp: number; zen: number; defense: number }
interface PublicGuilds {
  enabled: boolean; now?: number; weekEnds?: number; dailyCap?: number; buffsEnabled?: boolean; buffNameEn?: string; buffNameVi?: string;
  levels?: GuildBonus[]; sources?: { source: string; xp: number }[]; onlineMinutes?: number; siegeLadderBonus?: number;
  ladderOn?: boolean; ladderMinimumXp?: number;
  rewards?: { from: number; to: number; coins: number; masterBonus: number; coinType: string; zen: number; items: PublicReward["items"] }[];
  ladder?: { rank: number; name: string; xp: number; level: number; members?: number | null }[];
  lastWeek?: { week: string; lines: { rank: number; name: string; xp: number; coins: number; coinType: string; paid: number }[] } | null;
  guilds?: { name: string; level: number; xp: number; members?: number | null }[];
  sieges?: { at: number; name: string }[];
}
let guilds: PublicGuilds | null = null;

const GUILD_SOURCES: Record<string, Parameters<typeof t>[0]> = {
  MonsterKill: "guildsSourceMonsterKill", EventFinish: "guildsSourceEventFinish", EventWin: "guildsSourceEventWin",
  WorldBossKill: "guildsSourceWorldBossKill", WorldBossDamage: "guildsSourceWorldBossDamage", Mission: "guildsSourceMission",
  OnlineTime: "guildsSourceOnlineTime", CastleSiege: "guildsSourceCastleSiege",
};

function guildBonus(level: GuildBonus): string {
  const parts: string[] = [];
  if (level.exp > 0) parts.push(`+${level.exp}% ${t("guildsExp")}`);
  if (level.zen > 0) parts.push(`+${level.zen}% ${t("guildsZen")}`);
  if (level.defense > 0) parts.push(`+${level.defense} ${t("guildsDefense")}`);
  return parts.length ? parts.join(" · ") : t("guildsNoBuff");
}

function renderGuilds(): void {
  const format = (value: number) => value.toLocaleString(language === "vi" ? "vi-VN" : "en-AU");
  const data = guilds;
  const notice = element("guilds-notice");
  notice.textContent = !data ? t("guildsOffline") : !data.enabled ? t("guildsOff") : "";
  const on = !!data?.enabled;
  const members = (count?: number | null) => (count === null || count === undefined ? "—" : format(count));
  element("guilds-week-ends").textContent = on && data?.weekEnds ? `${t("guildsWeekEnds")} ${bossCountdown(data.weekEnds)}` : "";
  table("guilds-ladder", [["#", false], [t("guildsGuild"), false], [t("guildsLevel"), true], [t("guildsMembers"), true], [t("guildsPoints"), true]],
    on ? (data?.ladder ?? []).map((l) => [l.rank, l.name, l.level, members(l.members), format(l.xp)]) : [], on ? t("guildsNone") : "");
  const rewards = on && data?.ladderOn ? data.rewards ?? [] : [];
  element("guilds-rewards").textContent = rewards.length
    ? `${t("guildsRewards")}: ${rewards.map((r) => {
      const ranks = r.from === r.to ? `#${r.from}` : `#${r.from}–${r.to}`;
      const prize = rewardLine({ zen: r.zen, coins: r.coins, coinType: r.coinType, items: r.items });
      return `${ranks} ${prize}${r.masterBonus > 0 ? ` (${t("guildsMaster").replace("%d", format(r.masterBonus))})` : ""}`;
    }).join(" · ")}`
    : "";
  const last = on ? data?.lastWeek : null;
  element("guilds-last-week-of").textContent = last ? `${t("guildsWeekOf")} ${last.week}` : "";
  table("guilds-last-week", [["#", false], [t("guildsGuild"), false], [t("guildsPoints"), true], [t("guildsCoins"), true]],
    (last?.lines ?? []).map((l) => [l.rank, l.name, format(l.xp), l.coins > 0 ? `${format(l.coins)} ${coinLabel(l.coinType)}` : "—"]), on ? t("noRanking") : "");
  table("guilds-all", [["#", false], [t("guildsGuild"), false], [t("guildsLevel"), true], [t("guildsMembers"), true], [t("guildsXp"), true]],
    on ? (data?.guilds ?? []).map((g, index) => [index + 1, g.name, g.level, members(g.members), format(g.xp)]) : [], on ? t("guildsNone") : "");
  const sieges = clear("guilds-sieges");
  if (on) {
    for (const siege of data?.sieges ?? []) sieges.append(node("li", `${bossClock(siege.at, "Australia/Sydney")} — ${siege.name}`));
    if (!data?.sieges?.length) sieges.append(node("li", t("guildsNoSieges"), "muted"));
  }
  table("guilds-levels", [[t("guildsLevel"), false], [t("guildsXp"), true], [t("guildsBuff"), false]],
    on ? (data?.levels ?? []).map((l) => [l.level, format(l.xp), data?.buffsEnabled === false ? t("guildsNoBuff") : guildBonus(l)]) : [], "");
  const sources = on ? (data?.sources ?? []).map((s) => {
    const label = GUILD_SOURCES[s.source] ? t(GUILD_SOURCES[s.source]).replace("%d", String(data?.onlineMinutes ?? 10)) : s.source;
    return `${label} +${format(s.xp)}`;
  }) : [];
  element("guilds-sources").textContent = sources.length
    ? `${t("guildsSources")}: ${sources.join(" · ")}${data?.dailyCap ? ` (${t("guildsCap").replace("%d", format(data.dailyCap))})` : ""}`
    : "";
}

async function refreshGuilds(): Promise<void> {
  try {
    const response = await fetch("/website/guilds", { cache: "no-store", signal: AbortSignal.timeout(10000) });
    if (!response.ok) throw new Error("Server unavailable");
    const data = (await response.json()) as PublicGuilds;
    if (typeof data !== "object" || data === null || typeof data.enabled !== "boolean") throw new Error("Invalid guilds");
    guilds = data;
  } catch {
    guilds = null;
  }
  renderGuilds();
}

for (const id of ["en", "vi"]) element(id).addEventListener("click", () => renderGuilds());
element("refresh").addEventListener("click", () => void refreshGuilds());
void refreshGuilds();
setInterval(() => {
  if (!document.hidden) void refreshGuilds();
}, 5 * 60000);

// ---------- community (read from the server, /website/community) ----------

interface PublicCommunity {
  enabled: boolean; now?: number;
  discord?: { on: boolean; coins: number; coinType: string };
  zalo?: { on: boolean; coins: number; coinType: string };
  events?: { id: string; titleEn: string; titleVi: string; descriptionEn: string; descriptionVi: string; start?: number | null; end?: number | null; phase: string; hasCode: boolean; winners: string[]; reward: PublicReward }[];
  highlights?: { at: number; kind: string; textEn: string; textVi: string }[];
}
let community: PublicCommunity | null = null;

function renderCommunity(): void {
  const format = (value: number) => value.toLocaleString(language === "vi" ? "vi-VN" : "en-AU");
  const data = community;
  element("social-notice").textContent = !data ? t("communityOffline") : !data.enabled ? t("communityOff") : "";
  const on = !!data?.enabled;
  const coins = clear("social-coins");
  if (on && data?.discord?.on) coins.append(node("li", t("communityDiscordHow").replace("%s", `${format(data.discord.coins)} ${coinLabel(data.discord.coinType)}`)));
  if (on && data?.zalo?.on) coins.append(node("li", t("communityZaloHow").replace("%s", `${format(data.zalo.coins)} ${coinLabel(data.zalo.coinType)}`)));
  if (on && (data?.events ?? []).some((e) => e.hasCode)) coins.append(node("li", t("communityCodeHow")));
  const events = clear("social-events");
  for (const event of on ? data?.events ?? [] : []) {
    const box = node("div", "", "community-event");
    box.append(node("strong", (language === "vi" ? event.titleVi : event.titleEn) || event.titleEn || event.titleVi));
    const description = (language === "vi" ? event.descriptionVi : event.descriptionEn) || event.descriptionEn;
    if (description) box.append(node("p", description, "muted"));
    const span = event.phase === "Upcoming" && event.start ? `${t("communityUpcoming")} ${bossClock(event.start, "Australia/Sydney")}`
      : event.phase === "Ended" ? t("communityEnded")
        : `${t("communityOpen")}${event.end ? ` ${t("communityUntil")} ${bossClock(event.end, "Australia/Sydney")}` : ""}`;
    const reward = rewardLine(event.reward);
    box.append(node("p", [span, reward ? `${t("communityReward")}: ${reward}` : ""].filter((p) => p).join(" · ")));
    if (event.winners.length) box.append(node("p", `${t("communityWinnersHow")}: ${event.winners.join(", ")}`, "muted"));
    events.append(box);
  }
  if (on && !data?.events?.length) events.append(node("p", t("communityNoEvents"), "muted"));
  const highlights = clear("social-highlights");
  for (const item of on ? data?.highlights ?? [] : []) {
    highlights.append(node("li", `${bossClock(item.at, "Australia/Sydney")} — ${(language === "vi" ? item.textVi : item.textEn) || item.textEn}`));
  }
  if (on && !data?.highlights?.length) highlights.append(node("li", t("communityNoHighlights"), "muted"));
}

async function refreshCommunity(): Promise<void> {
  try {
    const response = await fetch("/website/community", { cache: "no-store", signal: AbortSignal.timeout(10000) });
    if (!response.ok) throw new Error("Server unavailable");
    const data = (await response.json()) as PublicCommunity;
    if (typeof data !== "object" || data === null || typeof data.enabled !== "boolean") throw new Error("Invalid community");
    community = data;
  } catch {
    community = null;
  }
  renderCommunity();
}

for (const id of ["en", "vi"]) element(id).addEventListener("click", () => renderCommunity());
element("refresh").addEventListener("click", () => void refreshCommunity());
void refreshCommunity();
setInterval(() => {
  if (!document.hidden) void refreshCommunity();
}, 2 * 60000);

// ---------- top races (read from the server, /website/top-races) ----------

type RaceKind = "ResetsGained" | "LevelsGained" | "LevelReached" | "BossHuntPoints" | "PkKills" | "EventWins" | "GuildPoints" | "MonsterKills" | "OnlineMinutes";
interface PublicRacePrize { from: number; to: number; coins: number; coinType: string; zen: number; items: PublicReward["items"] }
interface PublicRace { id: string; titleEn: string; titleVi: string; kind: RaceKind; event: string; scope: string; cls: number; period: string; minimum: number; prizes: PublicRacePrize[] }
interface PublicRaceLine { rank: number; name: string; value: number; cls: number }
interface PublicTopRaces {
  enabled: boolean; now?: number;
  races?: { race: PublicRace; start: number; end: number; tables: { cls: number; lines: PublicRaceLine[] }[] }[];
  upcoming?: { race: PublicRace; start: number; end: number }[];
  history?: { raceId: string; titleEn: string; titleVi: string; kind: RaceKind; periodKey: string; start: number; end: number; tables: { cls: number; lines: PublicRaceLine[] }[] }[];
}
let topRaces: PublicTopRaces | null = null;
let topRacesSkew = 0;

const RACE_CLASSES = ["racesClass0", "racesClass1", "racesClass2", "racesClass3", "racesClass4", "racesClass5", "racesClass6"] as const;

function raceTitle(race: { titleEn: string; titleVi: string }): string {
  return (language === "vi" ? race.titleVi : race.titleEn) || race.titleEn;
}

function raceClass(cls: number): string {
  const key = RACE_CLASSES[cls];
  return key ? t(key) : "";
}

function raceValue(kind: RaceKind, value: number): string {
  const n = value.toLocaleString(language === "vi" ? "vi-VN" : "en-AU");
  if (kind === "LevelReached") return language === "vi" ? `cấp ${n}` : `level ${n}`;
  return n;
}

function raceAbout(race: PublicRace): string {
  const period = race.period === "Daily" ? t("racesDaily") : race.period === "Weekly" ? t("racesWeekly") : race.period === "Monthly" ? t("racesMonthly") : t("racesCustom");
  const scope = race.kind === "GuildPoints" ? t("racesGuilds") : race.scope === "OneClass" ? t("racesOnly").replace("%s", raceClass(race.cls)) : race.scope === "PerClass" ? t("racesPerClass") : t("racesAll");
  return `${t(`racesKind${race.kind}` as CopyKey)} · ${period} · ${scope}`;
}

function raceCountdown(at: number): string {
  const minutes = Math.max(0, Math.ceil((at - (Date.now() + topRacesSkew)) / 60000));
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  return days > 0 ? `${days} d ${hours} h` : `${hours} h ${String(minutes % 60).padStart(2, "0")} min`;
}

function raceTable(kind: RaceKind, lines: PublicRaceLine[]): HTMLElement {
  if (lines.length === 0) return node("p", t("racesNobody"), "muted");
  const table = document.createElement("table");
  table.className = "table";
  const head = document.createElement("tr");
  for (const label of ["#", t("racesPlayer"), t("racesValue")]) head.append(node("th", label));
  table.append(head);
  for (const line of lines) {
    const row = document.createElement("tr");
    row.append(node("td", String(line.rank)), node("td", line.name), node("td", raceValue(kind, line.value)));
    table.append(row);
  }
  const wrap = node("div", "", "table-wrap");
  wrap.append(table);
  return wrap;
}

function renderTopRaces(): void {
  const data = topRaces;
  element("races-notice").textContent = !data ? t("racesOffline") : !data.enabled ? t("racesOff") : "";
  const on = !!data?.enabled;
  const running = clear("races-running");
  for (const entry of on ? data?.races ?? [] : []) {
    const race = entry.race;
    const box = node("article", "", "panel race-card");
    box.dataset.race = race.id;
    box.append(node("h4", raceTitle(race)));
    box.append(node("p", raceAbout(race), "muted"));
    box.append(node("p", `${t("racesEnds").replace("%s", raceCountdown(entry.end))} · ${t("racesEndsAt").replace("%s", bossClock(entry.end, "Australia/Sydney")).replace("%s", bossClock(entry.end))}`, "race-time"));
    for (const table of entry.tables) {
      if (table.cls >= 0 && race.scope === "PerClass") box.append(node("h5", raceClass(table.cls)));
      box.append(raceTable(race.kind, table.lines));
    }
    if (entry.tables.length === 0) box.append(node("p", t("racesNobody"), "muted"));
    const prizes = node("ul", "", "boss-news race-prizes");
    for (const prize of race.prizes) {
      const ranks = prize.to > prize.from ? t("racesRanks").replace("%d", String(prize.from)).replace("%d", String(prize.to)) : t("racesRank").replace("%d", String(prize.from));
      prizes.append(node("li", `${ranks}: ${rewardLine({ zen: prize.zen, coins: prize.coins, coinType: prize.coinType, items: prize.items })}`));
    }
    if (race.minimum > 1) prizes.append(node("li", t("racesMinimum").replace("%s", raceValue(race.kind, race.minimum)), "muted"));
    if (race.prizes.length > 0) {
      box.append(node("h5", t("racesPrizes")));
      box.append(prizes);
    }
    running.append(box);
  }
  if (on && !data?.races?.length) running.append(node("p", t("racesNone"), "muted"));
  const upcoming = clear("races-upcoming");
  for (const entry of on ? data?.upcoming ?? [] : []) {
    upcoming.append(node("li", `${raceTitle(entry.race)} · ${raceAbout(entry.race)} · ${t("racesStarts").replace("%s", bossClock(entry.start, "Australia/Sydney")).replace("%s", bossClock(entry.start))}`));
  }
  element("races-upcoming-panel").hidden = !on || !data?.upcoming?.length;
  const history = clear("races-history");
  for (const result of on ? data?.history ?? [] : []) {
    const winners = result.tables.map((table) => {
      const first = table.lines[0];
      if (!first) return "";
      return `${table.cls >= 0 ? `${raceClass(table.cls)}: ` : ""}${first.name} (${raceValue(result.kind, first.value)})`;
    }).filter((w) => w.length > 0);
    history.append(node("li", `${bossClock(result.end, "Australia/Sydney")} — ${raceTitle(result)}: ${winners.length > 0 ? winners.join(", ") : t("racesNobodyTook")}`));
  }
  if (on && !data?.history?.length) history.append(node("li", t("racesNoHistory"), "muted"));
}

async function refreshTopRaces(): Promise<void> {
  try {
    const response = await fetch("/website/top-races", { cache: "no-store", signal: AbortSignal.timeout(10000) });
    if (!response.ok) throw new Error("Server unavailable");
    const data = (await response.json()) as PublicTopRaces;
    if (typeof data !== "object" || data === null || typeof data.enabled !== "boolean") throw new Error("Invalid top races");
    topRaces = data;
    if (typeof data.now === "number") topRacesSkew = data.now - Date.now();
  } catch {
    topRaces = null;
  }
  renderTopRaces();
}

for (const id of ["en", "vi"]) element(id).addEventListener("click", () => renderTopRaces());
element("refresh").addEventListener("click", () => void refreshTopRaces());
void refreshTopRaces();
setInterval(() => {
  if (!document.hidden) void refreshTopRaces();
}, 60000);

// ---------- gift codes (the public codes, /website/gift-codes) ----------

interface PublicGiftCode {
  code: string; note: string; reward: PublicReward; usesLeft?: number;
  rules: { limit: string; start?: number; end?: number; minLevel: number; maxLevel: number; minResets: number; maxResets: number; classes: number[] };
}
interface PublicGiftCodes { enabled: boolean; website?: boolean; codes?: PublicGiftCode[] }
let giftCodes: PublicGiftCodes | null = null;

function renderGiftCodes(): void {
  const data = giftCodes;
  element("gift-notice").textContent = !data ? t("giftOffline") : !data.enabled ? t("giftOff") : data.website === false ? t("giftNoWebsite") : "";
  element("gift-account").hidden = !data?.enabled || data.website === false;
  const list = clear("gift-codes");
  const format = (value: number) => value.toLocaleString(language === "vi" ? "vi-VN" : "en-AU");
  for (const code of data?.enabled ? data.codes ?? [] : []) {
    const box = node("article", "", "panel gift-card");
    box.dataset.code = code.code;
    box.append(node("h4", code.code, "gift-code"));
    if (code.note) box.append(node("p", code.note, "muted"));
    box.append(node("p", rewardLine(code.reward), "gift-reward"));
    const r = code.rules;
    const rules: string[] = [r.limit === "OncePerCharacter" ? t("giftPerCharacter") : t("giftPerAccount")];
    if (r.minLevel > 0 || r.maxLevel > 0) rules.push(t("giftLevels").replace("%s", `${r.minLevel > 0 ? r.minLevel : 1}-${r.maxLevel > 0 ? r.maxLevel : "∞"}`));
    if (r.classes.length > 0) rules.push(r.classes.map((c) => raceClass(c)).join(", "));
    if (typeof code.usesLeft === "number") rules.push(t("giftLeft").replace("%s", format(code.usesLeft)));
    if (r.start && r.start > Date.now()) rules.push(t("giftFrom").replace("%s", bossClock(r.start, "Australia/Sydney")).replace("%s", bossClock(r.start)));
    if (r.end) rules.push(t("giftUntil").replace("%s", bossClock(r.end, "Australia/Sydney")).replace("%s", bossClock(r.end)));
    box.append(node("p", rules.join(" · "), "muted gift-rules"));
    list.append(box);
  }
  if (data?.enabled && !data.codes?.length) list.append(node("p", t("giftNone"), "muted"));
}

async function refreshGiftCodes(): Promise<void> {
  try {
    const response = await fetch("/website/gift-codes", { cache: "no-store", signal: AbortSignal.timeout(10000) });
    if (!response.ok) throw new Error("Server unavailable");
    const data = (await response.json()) as PublicGiftCodes;
    if (typeof data !== "object" || data === null || typeof data.enabled !== "boolean") throw new Error("Invalid gift codes");
    giftCodes = data;
  } catch {
    giftCodes = null;
  }
  renderGiftCodes();
}

for (const id of ["en", "vi"]) element(id).addEventListener("click", () => renderGiftCodes());
element("refresh").addEventListener("click", () => void refreshGiftCodes());
void refreshGiftCodes();
setInterval(() => {
  if (!document.hidden) void refreshGiftCodes();
}, 120000);

// ---------- resets (the month's reset ranking, relifes and the tiers, /website/resets) ----------

interface PublicResetEntry { rank: number; name: string; cls: number; value: number; resets: number; relifes: number }
interface PublicResetTier { from: number; to: number; level: number; zen: number; zenTimesReset: boolean; coins: number; coinType: string; points: number; mode: "Total" | "Add" | "Keep"; dailyLimit: number }
interface PublicResets {
  enabled: boolean; month?: string; previousMonth?: string;
  monthly?: PublicResetEntry[]; previous?: PublicResetEntry[]; relifes?: PublicResetEntry[];
  rules?: {
    tiers: PublicResetTier[];
    vip?: { levelReduction: number; zenDiscount: number; coinsDiscount: number; extraDaily: number };
    relife?: { resetsNeeded: number; level: number; zen: number; coins: number; coinType: string; keepPercent: number; pointsPerRelife: number; maximum: number };
  };
}
let resetsData: PublicResets | null = null;

function fill(text: string, ...values: (string | number)[]): string {
  let index = 0;
  return text.replace(/%s/g, () => String(values[index++] ?? ""));
}

function resetCost(zen: number, coins: number, coinType: string, timesReset = false): string {
  const n = (value: number) => value.toLocaleString(language === "vi" ? "vi-VN" : "en-AU");
  const parts: string[] = [];
  if (zen > 0) parts.push(`${n(zen)} zen${timesReset ? " × n" : ""}`);
  if (coins > 0) parts.push(`${n(coins)} ${coinType === "WCoinC" ? "W Coin (C)" : coinType === "WCoinP" ? "W Coin (P)" : "Goblin Points"}`);
  return parts.length ? parts.join(" + ") : t("resetsFree");
}

function renderResets(): void {
  const data = resetsData;
  element("resets-notice").textContent = !data ? t("resetsOffline") : !data.enabled ? t("resetsOff") : "";
  element("resets-account").hidden = !data?.enabled;
  const on = !!data?.enabled;
  element("resets-month-title").textContent = fill(t("resetsMonth"), data?.month ?? "");
  element("resets-previous-title").textContent = fill(t("resetsPrevious"), data?.previousMonth ?? "");
  const rows = (entries: PublicResetEntry[] | undefined) => (on ? entries ?? [] : []).map((e) => [e.rank, e.name, raceClass(Math.floor(e.cls / 4)), e.value]);
  const headers: [string, boolean][] = [[t("rank"), false], [t("character"), false], [t("className"), false], [t("resetsCount"), true]];
  table("resets-monthly", headers, rows(data?.monthly), t("resetsNone"));
  table("resets-previous", headers, rows(data?.previous), t("resetsNone"));
  table("resets-relifes", [[t("rank"), false], [t("character"), false], [t("className"), false], [t("resetsRelifeCount"), true], [t("resets"), true]],
    (on ? data?.relifes ?? [] : []).map((e) => [e.rank, e.name, raceClass(Math.floor(e.cls / 4)), e.value, e.resets]), t("resetsNoRelifes"));
  const rules = clear("resets-rules");
  if (!on || !data?.rules) return;
  for (const tier of data.rules.tiers) {
    const mode = tier.mode === "Add" ? t("resetsModeAdd") : tier.mode === "Keep" ? t("resetsModeKeep") : t("resetsModeTotal");
    const range = tier.to >= 0 ? `${tier.from + 1}-${tier.to + 1}` : `${tier.from + 1}+`;
    const item = node("li", "");
    item.append(node("strong", fill(t("resetsTierRange"), range)), document.createTextNode(": " + fill(t("resetsTierLine"), tier.level, resetCost(tier.zen, tier.coins, tier.coinType, tier.zenTimesReset), tier.points, mode, tier.dailyLimit > 0 ? tier.dailyLimit : t("resetsNoLimit"))));
    rules.append(item);
  }
  const vip = data.rules.vip;
  if (vip) rules.append(node("li", fill(t("resetsVipLine"), vip.levelReduction, vip.zenDiscount, vip.coinsDiscount, vip.extraDaily)));
  const relife = data.rules.relife;
  if (relife) rules.append(node("li", fill(t("resetsRelifeLine"), relife.resetsNeeded, relife.level, resetCost(relife.zen, relife.coins, relife.coinType), relife.keepPercent, relife.pointsPerRelife, relife.maximum > 0 ? relife.maximum : t("resetsNoLimit"))));
}

async function refreshResets(): Promise<void> {
  try {
    const response = await fetch("/website/resets", { cache: "no-store", signal: AbortSignal.timeout(10000) });
    if (!response.ok) throw new Error("Server unavailable");
    const data = (await response.json()) as PublicResets;
    if (typeof data !== "object" || data === null || typeof data.enabled !== "boolean") throw new Error("Invalid resets");
    resetsData = data;
  } catch {
    resetsData = null;
  }
  renderResets();
}

for (const id of ["en", "vi"]) element(id).addEventListener("click", () => renderResets());
element("refresh").addEventListener("click", () => void refreshResets());
void refreshResets();
setInterval(() => {
  if (!document.hidden) void refreshResets();
}, 120000);

// ---------- top-up milestones (public tiers on home page, /website/topup-milestones) ----------

interface PublicMilestoneItem {
  group: number;
  number: number;
  level: number;
  quantity: number;
  name?: string;
}

interface PublicMilestoneStep {
  id: string;
  thresholdVnd: number;
  titleEn: string;
  titleVi: string;
  coinType: string;
  coins: number;
  zen: number;
  items: PublicMilestoneItem[];
}

interface PublicTopUpMilestones {
  enabled: boolean;
  steps?: PublicMilestoneStep[];
}

let publicMilestones: PublicTopUpMilestones | null = null;

function renderPublicMilestones(): void {
  const section = document.getElementById("topup-milestones-section");
  if (!section) return;
  const data = publicMilestones;
  if (!data?.enabled || !data.steps?.length) {
    section.hidden = true;
    return;
  }
  section.hidden = false;
  const vi = document.documentElement.lang === "vi";
  element("topup-milestones-heading").textContent = vi ? "Thưởng mốc nạp" : "Top-up milestones";
  element("topup-milestones-notice").textContent = vi
    ? "Nhận thưởng một lần mỗi mốc khi tổng tiền nạp tích lũy đạt ngưỡng."
    : "Claim each milestone reward once when cumulative confirmed top-ups pass the threshold.";
  const list = clear("topup-milestones-list");
  for (const step of data.steps) {
    const card = document.createElement("article");
    card.className = "panel";
    const h3 = document.createElement("h3");
    h3.textContent = `${step.thresholdVnd.toLocaleString(vi ? "vi-VN" : "en-AU")} VND — ${vi && step.titleVi ? step.titleVi : step.titleEn}`;
    const rewards: string[] = [];
    if (step.coins > 0) rewards.push(`${step.coins.toLocaleString(vi ? "vi-VN" : "en-AU")} ${step.coinType}`);
    if (step.zen > 0) rewards.push(`${step.zen.toLocaleString(vi ? "vi-VN" : "en-AU")} Zen`);
    for (const item of step.items) {
      rewards.push(`${item.name ?? `Item ${item.group}:${item.number}`}${item.level > 0 ? ` +${item.level}` : ""} ×${item.quantity}`);
    }
    const p = document.createElement("p");
    p.textContent = rewards.join(" · ");
    card.append(h3, p);
    list.append(card);
  }
}

async function refreshPublicMilestones(): Promise<void> {
  try {
    const response = await fetch("/website/topup-milestones", { cache: "no-store", signal: AbortSignal.timeout(10000) });
    if (!response.ok) throw new Error("Server unavailable");
    publicMilestones = (await response.json()) as PublicTopUpMilestones;
  } catch {
    publicMilestones = null;
  }
  renderPublicMilestones();
}

for (const id of ["en", "vi"]) element(id).addEventListener("click", () => renderPublicMilestones());
element("refresh").addEventListener("click", () => void refreshPublicMilestones());
void refreshPublicMilestones();

// ---------- full event, invasion & boss timetable (/website/timetable) ----------

interface PublicTimetableEntry {
  id: string;
  category: "event" | "invasion" | "boss" | "custom";
  nameEn: string;
  nameVi: string;
  mapsEn: string;
  mapsVi: string;
  dropsEn: string;
  dropsVi: string;
  schedule: string;
  durationMin: number;
  active: boolean;
  nextStart: number | null;
  endsAt: number | null;
}

interface PublicTimetable {
  v: number;
  off?: boolean;
  now: number;
  timeZone: string;
  entries: PublicTimetableEntry[];
  findGmWinners?: { name: string; map: string; coins: number; coinType: string; zen: number; at: number }[];
}

let timetableData: PublicTimetable | null = null;
let timetableFilter: "all" | "event" | "invasion" | "boss" = "all";

function formatSydneyOrLocal(atMs: number | null, timeZone?: string): string {
  if (!atMs) return "—";
  return new Intl.DateTimeFormat(language === "vi" ? "vi-VN" : "en-AU", {
    weekday: "short",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    ...(timeZone ? { timeZone } : {}),
  }).format(atMs);
}

function renderTimetable(): void {
  const filters = clear("timetable-filters");
  const filterDefs: ["all" | "event" | "invasion" | "boss", string][] = [
    ["all", t("ttAll")],
    ["event", t("ttEvents")],
    ["invasion", t("ttInvasions")],
    ["boss", t("ttBosses")],
  ];
  for (const [key, label] of filterDefs) {
    const btn = node("button", label, key === timetableFilter ? "btn" : "btn muted") as HTMLButtonElement;
    btn.type = "button";
    btn.addEventListener("click", () => {
      timetableFilter = key;
      renderTimetable();
    });
    filters.append(btn);
  }

  const notice = element("timetable-notice");
  const gmEl = element("timetable-gm-winners");
  if (!timetableData || timetableData.off) {
    notice.textContent = timetableData?.off ? t("ttOffline") : "";
    table("timetable-table", [], [], timetableData ? "" : t("ttOffline"));
    gmEl.textContent = "";
    return;
  }

  notice.textContent = "";
  const tz = timetableData.timeZone || "Australia/Sydney";
  const rows = timetableData.entries
    .filter((entry) => timetableFilter === "all" || entry.category === timetableFilter)
    .map((entry) => {
      const name = language === "vi" ? entry.nameVi : entry.nameEn;
      const map = language === "vi" ? entry.mapsVi : entry.mapsEn;
      const drops = language === "vi" ? entry.dropsVi : entry.dropsEn;
      const status = entry.active
        ? t("ttActive")
        : entry.nextStart
          ? (() => {
              const sec = Math.max(0, Math.round((entry.nextStart - Date.now()) / 1000));
              const h = Math.floor(sec / 3600);
              const m = Math.floor((sec % 3600) / 60);
              return h > 0 ? `${h}h ${m}m` : `${m}m`;
            })()
          : "—";
      return [
        name,
        formatSydneyOrLocal(entry.nextStart, tz),
        formatSydneyOrLocal(entry.nextStart),
        status,
        map || "—",
        drops || "—",
        entry.schedule || "—",
      ];
    });

  table(
    "timetable-table",
    [
      [t("ttColName"), false],
      [t("ttColSydney"), false],
      [t("ttColLocal"), false],
      [t("ttColCountdown"), false],
      [t("ttColMap"), false],
      [t("ttColDrops"), false],
      [t("ttColSchedule"), false],
    ],
    rows,
    t("ttNone"),
  );

  const winners = timetableData.findGmWinners ?? [];
  gmEl.textContent = winners.length
    ? `${t("ttGmWinners")}: ` + winners.slice(0, 5).map((w) => `${w.name} (${w.map})`).join(", ")
    : "";
}

async function refreshTimetable(): Promise<void> {
  try {
    const response = await fetch("/website/timetable", { cache: "no-store", signal: AbortSignal.timeout(10000) });
    if (!response.ok) throw new Error("Server unavailable");
    const data = (await response.json()) as PublicTimetable;
    if (typeof data !== "object" || data === null || !Array.isArray(data.entries)) throw new Error("Invalid timetable");
    timetableData = data;
  } catch {
    timetableData = null;
  }
  renderTimetable();
}

for (const id of ["en", "vi"]) element(id).addEventListener("click", () => renderTimetable());
element("refresh").addEventListener("click", () => void refreshTimetable());
void refreshTimetable();
setInterval(() => {
  if (!document.hidden) void refreshTimetable();
}, 60000);

// ---------- daily lucky number (public draws on home page, /website/lucky-number) ----------

interface PublicLuckyNumber {
  enabled: boolean;
  roundKey: string;
  nextDrawAt: number;
  timeZone: string;
  minNumber: number;
  maxNumber: number;
  odds: { exactPercent: number; lastDigitPercent: number };
  prizes: {
    exact: { coins: number; coinType: string; jewelCount: number; jewelName: string };
    lastDigit: { enabled: boolean; coins: number; coinType: string; jewelCount: number; jewelName: string };
  };
  recentDraws: { dateKey: string; winningNumber: number; winners: { name: string; number: number; tier: string }[] }[];
}

let luckyData: PublicLuckyNumber | null = null;

function renderLuckyNumber(): void {
  const notice = element("lucky-notice");
  const summary = element("lucky-summary");
  const drawTime = element("lucky-draw-time");
  if (!luckyData || !luckyData.enabled) {
    notice.textContent = t("luckyOffline");
    summary.textContent = "";
    drawTime.textContent = "";
    table("lucky-draws", [], [], "");
    return;
  }

  notice.textContent = "";
  const exactPrize = `${luckyData.prizes.exact.coins} ${luckyData.prizes.exact.coinType}` +
    (luckyData.prizes.exact.jewelCount > 0 ? ` + ${luckyData.prizes.exact.jewelCount}x ${luckyData.prizes.exact.jewelName}` : "");
  drawTime.textContent = `${luckyData.roundKey} · Sydney ${new Intl.DateTimeFormat(language === "vi" ? "vi-VN" : "en-AU", { hour: "2-digit", minute: "2-digit", hour12: false, timeZone: luckyData.timeZone || "Australia/Sydney" }).format(luckyData.nextDrawAt)}`;
  summary.textContent = `${luckyData.minNumber}–${luckyData.maxNumber} · ${luckyData.odds.exactPercent}% exact · ${exactPrize}`;
  table(
    "lucky-draws",
    [[t("luckyDate"), false], [t("luckyWinning"), true], [t("luckyWinners"), false]],
    luckyData.recentDraws.map((d) => [
      d.dateKey,
      `#${String(d.winningNumber).padStart(2, "0")}`,
      d.winners.length ? d.winners.map((w) => `${w.name} (#${String(w.number).padStart(2, "0")})`).join(", ") : "—",
    ]),
    t("luckyNone"),
  );
}

async function refreshLuckyNumber(): Promise<void> {
  try {
    const response = await fetch("/website/lucky-number", { cache: "no-store", signal: AbortSignal.timeout(10000) });
    if (!response.ok) throw new Error("Server unavailable");
    luckyData = (await response.json()) as PublicLuckyNumber;
  } catch {
    luckyData = null;
  }
  renderLuckyNumber();
}

for (const id of ["en", "vi"]) element(id).addEventListener("click", () => renderLuckyNumber());
element("refresh").addEventListener("click", () => void refreshLuckyNumber());
void refreshLuckyNumber();
setInterval(() => {
  if (!document.hidden) void refreshLuckyNumber();
}, 60000);

// ---------- marriage couples registry (public, /website/marriage) ----------

interface PublicMarriage {
  enabled: boolean;
  rules: { minLevel: number; partyExpBonusPercent: number; partyDefenseBonus: number; ceremonyZenCost: number };
  couples: { partnerA: string; partnerB: string; title: string; marriedAt: number; days: number }[];
}

let marriageData: PublicMarriage | null = null;

function renderMarriage(): void {
  const notice = element("marriage-notice");
  const rulesEl = element("marriage-rules");
  if (!marriageData || !marriageData.enabled) {
    notice.textContent = t("marriageOffline");
    rulesEl.textContent = "";
    table("marriage-couples", [], [], "");
    return;
  }

  notice.textContent = "";
  rulesEl.textContent = `Lv ${marriageData.rules.minLevel}+ · +${marriageData.rules.partyExpBonusPercent}% EXP · +${marriageData.rules.partyDefenseBonus} Def`;
  table(
    "marriage-couples",
    [[t("marriagePair"), false], [t("marriageCoupleTitle"), false], [t("marriageDate"), false], [t("marriageDays"), true]],
    marriageData.couples.map((c) => [
      `${c.partnerA} ♥ ${c.partnerB}`,
      c.title || "—",
      new Intl.DateTimeFormat(language === "vi" ? "vi-VN" : "en-AU", { dateStyle: "medium", timeZone: "Australia/Sydney" }).format(c.marriedAt),
      c.days,
    ]),
    t("marriageNone"),
  );
}

async function refreshMarriage(): Promise<void> {
  try {
    const response = await fetch("/website/marriage", { cache: "no-store", signal: AbortSignal.timeout(10000) });
    if (!response.ok) throw new Error("Server unavailable");
    marriageData = (await response.json()) as PublicMarriage;
  } catch {
    marriageData = null;
  }
  renderMarriage();
}

for (const id of ["en", "vi"]) element(id).addEventListener("click", () => renderMarriage());
element("refresh").addEventListener("click", () => void refreshMarriage());
void refreshMarriage();
setInterval(() => {
  if (!document.hidden) void refreshMarriage();
}, 120000);

interface PublicMarketListing {
  id: string;
  sellerName: string;
  currency: string;
  price: number;
  expiresAt: number;
  item: { NameEn: string; NameVi: string; Level: number };
}
interface PublicMarket {
  v: number;
  enabled: boolean;
  listings?: PublicMarketListing[];
}
let marketData: PublicMarket | null = null;

function renderMarket(): void {
  const notice = element("market-notice");
  const data = marketData;
  const vi = language === "vi";
  if (data === null) notice.textContent = t("marketOffline");
  else if (!data.enabled) notice.textContent = t("marketOff");
  else notice.textContent = "";
  const listings = data && data.enabled ? data.listings ?? [] : [];
  const fmt = new Intl.DateTimeFormat(vi ? "vi-VN" : "en-AU", { month: "short", day: "2-digit", hour: "2-digit", minute: "2-digit", hour12: false, timeZone: "Australia/Sydney" });
  const rows = listings.map((l) => {
    const nm = (vi ? l.item.NameVi : l.item.NameEn) || l.item.NameEn;
    return [
      l.item.Level > 0 ? `${nm} +${l.item.Level}` : nm,
      l.sellerName,
      `${l.price.toLocaleString()} ${l.currency}`,
      fmt.format(l.expiresAt),
    ];
  });
  table("market-table", [
    [t("marketItem"), false],
    [t("marketSeller"), false],
    [t("marketPrice"), true],
    [t("marketExpires"), false],
  ], rows, t("marketNone"));
}

async function refreshMarket(): Promise<void> {
  try {
    const response = await fetch("/website/market", { cache: "no-store", signal: AbortSignal.timeout(10000) });
    if (!response.ok) throw new Error("Server unavailable");
    const data = (await response.json()) as PublicMarket;
    if (typeof data !== "object" || data === null || typeof data.enabled !== "boolean") throw new Error("Invalid market");
    marketData = data;
  } catch {
    marketData = null;
  }
  renderMarket();
}

for (const id of ["en", "vi"]) element(id).addEventListener("click", () => renderMarket());
element("refresh").addEventListener("click", () => void refreshMarket());
void refreshMarket();
setInterval(() => {
  if (!document.hidden) void refreshMarket();
}, 120000);
