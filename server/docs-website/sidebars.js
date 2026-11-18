// @ts-check

/**
 * The sidebar is maintained by hand, so the reading order is a deliberate
 * decision and not a side effect of the file names.
 *
 * @type {import("@docusaurus/plugin-content-docs").SidebarsConfig}
 */
const sidebars = {
  docsSidebar: [
    "intro",
    {
      type: "category",
      label: "Getting Started",
      collapsed: false,
      items: [
        "getting-started/requirements",
        "getting-started/docker",
        "getting-started/from-source",
        "getting-started/game-client",
        "getting-started/test-accounts",
      ],
    },
    {
      type: "category",
      label: "Deployment",
      items: [
        "deployment/overview",
        "deployment/all-in-one",
        "deployment/all-in-one-traefik",
        "deployment/distributed",
        "deployment/startup-parameters",
      ],
    },
    {
      type: "category",
      label: "Admin Panel",
      items: [
        "admin-panel/overview",
        "admin-panel/setup",
        "admin-panel/configuration-updates",
        "admin-panel/configuration-captions",
        "admin-panel/servers",
        "admin-panel/worlds",
        "admin-panel/accounts",
        "admin-panel/ban-manager",
        "admin-panel/character-tools",
        "admin-panel/gm-manager",
        "admin-panel/online-accounts",
        "admin-panel/game-configuration",
        "admin-panel/gameplay-rules",
        "admin-panel/vault-chaos-rewards",
        "admin-panel/buffer-npc",
        "admin-panel/banned-words",
        "admin-panel/off-trade",
        "admin-panel/rankings-settings",
        "admin-panel/talismans",
        "admin-panel/custom-ranks",
        "admin-panel/teleport-npc",
        "admin-panel/fixed-options",
        "admin-panel/event-alerts",
        "admin-panel/crash-watch",
        "admin-panel/item-definitions",
        "admin-panel/gift-box",
        "admin-panel/import-server",
        "admin-panel/anti-cheat",
        "admin-panel/rate-tuning",
        "admin-panel/s6-events",
        "admin-panel/qol-settings",
        "admin-panel/escrow-contracts",
        "admin-panel/news-manager",
        "admin-panel/item-drops",
        "admin-panel/plugins",
        "admin-panel/state-retention",
        "admin-panel/activity-log",
        "admin-panel/maintenance-tools",
        "admin-panel/economy-dashboard",
        "admin-panel/chat-commands",
        "admin-panel/map-editor",
        "admin-panel/world-editor",
        "admin-panel/live-map",
        "admin-panel/logs-and-monitoring",
        "admin-panel/authentication",
        "admin-panel/mcp",
        "admin-panel/users",
        "admin-panel/common-tasks",
      ],
    },
    {
      type: "category",
      label: "Server features",
      items: [
        "server-features/castle-siege",
        "server-features/bots",
        "server-features/anti-cheat",
        "server-features/test-actors",
        "server-features/gens",
        "server-features/cash-shop",
        "server-features/discord",
        "server-features/player-shop-currencies",
      ],
    },
    {
      type: "category",
      label: "Development",
      items: [
        "development/architecture",
        "development/solution-structure",
        "development/contributing",
      ],
    },
    {
      type: "category",
      label: "Reference",
      items: [
        "reference/ports",
        "reference/packets",
      ],
    },
  ],
};

export default sidebars;
