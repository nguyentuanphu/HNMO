# Banned Words Filter

The **Banned Words Filter** plug-in (`BannedWordsPlugIn`) lets hosts filter or block restricted English and Vietnamese words in chat messages and character/guild names from `/banned-words`.

## Key Options

- **Enable Banned Words Filter**: OFF by default until the administrator activates it.
- **Chat handling**: Choose between **Censor with \*\*\*** (masks matching terms before delivering chat) or **Block message & warn player** (rejects delivery and sends a localized EN/VI chat notice).
- **Scopes**: Independently toggle chat filtering (normal, whisper, party, guild, alliance, gens, `/post`, `/world`), character name creation filtering, and guild name creation filtering.
- **Ignore Vietnamese diacritics**: Disabled by default so everyday Vietnamese words (`các`, `lớn`, `đi`) are not flagged; unaccented slang terms can be listed explicitly in the Vietnamese word list.
- **Check substrings in names**: Detects banned words of length 4+ or CamelCase/digit tokens inside character and guild names (for example `ProDkm` or `PlayerBadWord`), without rejecting safe names such as `Admin` or `Classic`.
- **Exempt Game Masters**: Allows GMs to bypass the filter for moderation notices.
- **Live filter tester**: Test sample chat lines or names directly in the admin page before saving.
