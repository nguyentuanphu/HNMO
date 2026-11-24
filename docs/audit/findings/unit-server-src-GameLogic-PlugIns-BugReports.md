# Audit Unit: `server/src/GameLogic/PlugIns/BugReports`

- **Status**: Checked (on commit `4e6f9c9d`, 2026-11-24)
- **Files**: 8 files read in full (`BugReport.cs`, `BugReportEngine.cs`, `BugReportForwarder.cs`, `BugReportsConfiguration.cs`, `BugReportsPlugIn.cs`, `BugReportStatus.cs`, `BugReportStore.cs`, `BugReportStoreData.cs`).
- **Rule Category**: Custom HNMO feature — In-game bug reporting, status/reply notifications, coin rewards, and Discord webhook forwarding.

## Findings

1. **Quotas, idempotent rewards & privacy**: `BugReportEngine.Submit` enforces `MinTextLength = 10` and `DailyLimitPerAccount` (5/day in Sydney time). `BugReportsPlugIn.SetStatusAsync` rewards confirmed/fixed reports at most once (`RewardGiven == 0` + idempotent `MissionCoinGranter` reference). `Clean()` strips lines containing `password` or `ticket`, and `BugReportForwarder.IsDiscordWebhook` restricts forwarding to HTTPS Discord `/api/webhooks/` URLs. No defects found.
