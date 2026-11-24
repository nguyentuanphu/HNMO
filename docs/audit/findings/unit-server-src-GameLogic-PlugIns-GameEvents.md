# Audit Findings — `server/src/GameLogic/PlugIns/GameEvents`

- **Unit**: `server/src/GameLogic/PlugIns/GameEvents` (2 files, 410 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `GameEventPublisherConfiguration.cs`, `GameEventPublisherPlugIn.cs`

## Behavior Summary
- Optional (`IDisabledByDefault`) server event bus publisher for mini-games, invasions, Castle Siege transitions, GM global notices (`!`), rare Exc/Ancient monster drops (deduplicated against `CommunityPlugIn.PostsDropsToDiscord`), boss kills (credited to summon owner if summoned monster kills), level milestones (`LevelMilestones` default `[400]`), master level milestones, and Guild/Alliance/World chat (`PublishChatMessages` off by default).
- No defects found (note `CharacterLeveledUp` triggers background `_ = PublishMilestoneAsync(...)` with `try/catch` because `ICharacterLevelUpPlugIn` is synchronous).
