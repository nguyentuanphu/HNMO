# Unit Audit: `client/src/source/UI/Dialogs (files 1)`

- **Unit**: `client/src/source/UI/Dialogs (files 1)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `18` (`5,926` lines) including `CustomMessageBox.cpp`, `GenericConfirmDialog.cpp`, `CommonMessageBox.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — `CustomMessageBox.cpp` computes Lahap gem bundle sizes for unbundling)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Lahap gem bundle jewel count rule | `CustomMessageBox.cpp:221-227` | Computes `(entry.level + 1) * kJewelsPerBundleLevel` with `kJewelsPerBundleLevel = 10` (`10 / 20 / 30` jewels per bundle level `0/1/2`) when listing dissolvable bundles | Server Lahap `JewelMixAction` (`10 / 20 / 30` jewels at item level `0/1/2`) | Same |
| Generic modal/message box wrappers | Remaining 17 files (`GenericConfirmDialog.cpp`, etc.) | Shared RmlUi / C++ modal box layout & focus handling | UI presentation | No game rules |
