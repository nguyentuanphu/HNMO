---
title: Daily Lucky Number
sidebar_position: 15
description: Configure daily lucky number picks, draw time, prizes (coins & jewels), odds, and website integration.
---

# Daily Lucky Number

**Navigation:** *Rules and events → Daily Lucky Number* — route `/lucky-number`

## Overview

When enabled (OFF by default), accounts can pick one or more daily numbers (default `0–99`) in the web game (<kbd>L</kbd>) or via the website portal. At the configured Sydney wall-clock draw time (default `20:00`), a random winning number is drawn:

- **Exact match** (default `1%` odds): grants configured coins (`WCoinC` / `WCoinP` / `GoblinPoints`) immediately and queues configured jewels for in-game claiming.
- **Last-digit consolation prize** (optional): grants smaller coins/jewels when matching the winning number's last digit.
- All rounds, picks, winners, and unclaimed jewels persist in `save/hnmo-state.json` (`luckyNumber` section).
