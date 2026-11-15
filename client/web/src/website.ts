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
      "Open the browser client, select your server, and create your character.",
    download: "Take the realm with you",
    downloadText:
      "One download for Windows, Ubuntu and macOS. Unzip it and run Start HNMO.bat (Windows) or start-hnmo.sh (Ubuntu, macOS).",
    downloadLink: "Download HNMO →",
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
    chooseText: "Mở trò chơi trên trình duyệt, chọn máy chủ và tạo nhân vật.",
    download: "Mang theo thế giới của bạn",
    downloadText:
      "Một bản tải cho Windows, Ubuntu và macOS. Giải nén rồi chạy Start HNMO.bat (Windows) hoặc start-hnmo.sh (Ubuntu, macOS).",
    downloadLink: "Tải HNMO →",
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
