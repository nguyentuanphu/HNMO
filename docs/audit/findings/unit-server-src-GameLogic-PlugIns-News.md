# Audit Unit: `server/src/GameLogic/PlugIns/News`

- **Status**: Checked (on commit `f9b96fc8`, 2026-11-24)
- **Files**: 5 files read in full (`NewsConfiguration.cs`, `NewsPlugIn.cs`, `NewsPost.cs`, `NewsStateWriter.cs`, `NewsStore.cs`).
- **Rule Category**: Custom HNMO feature — Bilingual EN/VI news posts & in-game notices (`C2 F5 2A`).

## Findings

1. **Minor note (`NotifyInChat` on login)**: `NewsPlugIn.PlayerStateChangedAsync` checks `cfg.ChatNoticeOnEnterWorld` and picks `store.ListPublished(DateTime.UtcNow, 1).FirstOrDefault()`, without filtering by `latest.NotifyInChat`.
2. **Persistence & sanitization**: `NewsStore` synchronizes on `_stateStore?.SyncRoot ?? _gate`, persists via `HnmoFeatureRowSync<NewsPost>` (`news.post`), sanitizes `ImageUrl` (relative `/` excluding `//` or `http(s)://`, max 512 chars), and handles Sydney DST transitions.
