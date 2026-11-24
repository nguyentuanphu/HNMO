# Audit Findings: `server/src/Persistence/Initialization/Captions`

- **Unit**: `server/src/Persistence/Initialization/Captions` (7 files, 815 lines)
- **Checked on**: 2026-11-24
- **Checked at**: `94d47a15`
- **Status**: `Checked`

## Comparison & Analysis

The Season 6 and Season 9 suites store localized text directly in static per-language tables (the suite's English language message file has 720 `<Msg>` entries in S6 and 739 in S9, plus client `.bmd` tables), with no runtime database caption-linking mechanism (**differs**: HNMO infrastructure feature).

## File-by-File Notes

- `CaptionChange.cs:10-64`: Defines `CaptionChangeKind` (`Missing`, `Updated`, `Removed`, `Customized`) and `CaptionChange` record (`IsRecommended` true unless `Customized`).
- `CaptionComparison.cs:13`: Record holding `LinkedCaptions`, `Changes`, `UnresolvedSourceKeys`.
- `CaptionLinkStep.cs:10-36`: Progress enum (`LoadingConfiguration` .. `Completed`).
- `CaptionLinkReference.cs:16-50`: Snapshots linked `LocalizedString` entries from an in-memory reference `GameConfiguration` keyed by `(OwnerId, PropertyName)`, `GetNumberKey`, and `GetNeutralNameKey`.
- `LocalizedCaption.cs:18-130`: Reflection graph walker (`FindAll`) collecting writable `LocalizedString` properties on `IIdentifiable` data-model objects, skipping persistence-only join/raw properties.
- `ConfigurationCaptions.cs:16-330`: Links captions to source keys by ID (`ConfigurationCaptions.cs:238`), number/armor-set/package sequence (`GetNumberKey`, `ConfigurationCaptions.cs:162-181`), or unique neutral text (`GetNeutralNameKey`, `ConfigurationCaptions.cs:188-194`); detects `DetermineChanges` (`268-318`) and applies changes with source-stamp updates (`ApplyChanges`, `68-101`). Note `GetNumberKey` checks `Number` as `byte|short|int|long|ushort` (`ConfigurationCaptions.cs:178`), while `LocalizedCaption` treats `sbyte`/`uint`/`ulong` as unsupported number types (all HNMO config entities use `byte`, `short`, `int`, or `ushort`).
- `ConfigurationCaptionService.cs:19-185`: Service layer caching per-initialization `CaptionLinkReference` instances behind `SemaphoreSlim ReferenceCreationLock` (`152-169`) and saving updates with change notifications suspended (`SaveWithoutChangeNotificationsAsync`, `127-131`).
