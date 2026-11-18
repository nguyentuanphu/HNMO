---
title: Chaos Rates, Multi-Vault & Event Rewards
---

# Chaos Rates, Multi-Vault & Event Rewards

The **Chaos Rates, Multi-Vault & Event Rewards** page (`/vault-chaos-rewards`) manages:

1. **Chaos Machine Rates (`ChaosRatesPlugIn`, 14.4)**:
   - Configure a server-wide multiplier and per-recipe overrides (applied live to both crafting RNG rolls and the preview rate displayed to players).
2. **Multi-Vault (`MultiVaultPlugIn` & `VaultChatCommandPlugIn`, 14.5)**:
   - Configure free unlocked vaults, hard cap per account, VIP bonus vaults, and coin purchase price for `/vault buy`.
   - Players type `/vault` to inspect their vaults, `/vault <index>` to switch vaults (when closed), and `/vault buy` to unlock an extra vault slot.
   - Secondary vaults are stored individually in `hnmo.player_feature_state`.
3. **Event Rewards Editor (`EventRewardsOperations`, 14.7)**:
   - Edit `MiniGameReward` entries per mini-game type and level (Blood Castle, Devil Square, Chaos Castle, Illusion Temple, etc.) with audit logging.
