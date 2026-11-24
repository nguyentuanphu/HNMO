# Audit Unit: `server/src/GameLogic/PlugIns/GmManager`

- **Status**: Checked (on commit `f9b96fc8`, 2026-11-24)
- **Files**: 5 files read in full (`GmAssignment.cs`, `GmCommandAuditEntry.cs`, `GmCommandPermissionCatalog.cs`, `GmPermission.cs`, `GmPermissionStore.cs`).
- **Rule Category**: Custom HNMO feature — Granular GM command permissions and audit trail.

## Findings

1. **Permission enforcement & audit**: `GmCommandPermissionCatalog` maps GM commands to 7 `GmPermission` flags (unlisted commands default to `GmPermission.EditCharacter`). `GmPermissionStore.AuthorizeAndAudit` checks per-character masks (or `RequireListedForGmCommands` for unlisted GMs), caps local audit history at `200`, persists via `HnmoFeatureRowSync<GmAssignment>`, and forwards to `ActivityLogPlugIn`. No defects found.
