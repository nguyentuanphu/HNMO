# News Manager (`/news`)

Manage bilingual **English / Vietnamese** server announcements, patch notes, and pinned notices from AdminPanel (`/news`).

## Features

- **Bilingual posts**: Store separate English and Vietnamese headlines and bodies per post; if one language is omitted, the other is used as fallback.
- **Sydney schedule & pinning**: Set publish timestamps in `Australia/Sydney` time. Scheduled posts remain hidden until the publish time is reached; pinned posts remain at the top.
- **Optional banner image**: Attach a relative path (`/images/...`) or `https://` image URL (sanitized against `javascript:`/`data:` URIs).
- **Website homepage (`#news`)**: Exposed via `GET /api/website/news` and bridged at `/website/news`.
- **Login & character screen panel**: Displayed automatically inside `#loginNewsPanel` on `index.html` when `ShowLoginPanel` is enabled.
- **In-game chat notice (`C2 F5 2A`)**: Broadcasts `[News]` / `[Tin tức]` alerts directly into online players' chat box when a post is published or manually notified, and sends the latest published post notice on world entry.
