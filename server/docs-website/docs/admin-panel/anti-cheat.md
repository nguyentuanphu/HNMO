---
title: Anti-Cheat Authority & Audit Log
sidebar_label: Anti-Cheat
---

# Anti-Cheat Authority & Audit Log

Open `/anti-cheat` in the Admin Panel to configure server-side anti-cheat protections and review the violation audit log (`AntiCheatPlugIn`, OFF by default).

- **Per-IP Connection Cap**: Limits simultaneous authenticated sessions sharing the same IP (`MaxConnectionsPerIp`, optional loopback exemption).
- **Attack Rate Authority**: Validates millisecond intervals between attacks against character attack speed with configurable tolerance factor and rolling strike kick threshold.
- **Max Agility Cap**: Flags impossible agility values when `EnableHitHackAgilityCheck` is active.
- **Safe-Zone Audit & Violation Log**: Logs blocked safe-zone attacks, rate violations, and IP kicks under `save/hnmo-state.json` (`antiCheatAudit`) in Sydney time.
