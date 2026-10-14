# Changelog

## Unreleased
- Replaced the settings window with a native Windows MAUI keypad editor.
- Removed visible position letters and added a stacked layout for narrower windows.
- Preserved assignments, tray listening, startup, and native action restoration through a private per-user helper connection.


## Unreleased

### Added
- A dark settings window modeled on the Creator Micro, with seven selectable lower keys.
- Optional model presets for G through M; default keys display `(default)`.
- Native keycap/action backups and restoration when changing override mode.
- Cached model discovery, last-checked time, and optional refresh when opening settings.
- Lance Larsen attribution, website, and open-source project links.

### Fixed
- Settings layout and key text at high Windows display scaling.
- Existing three-key settings migrate without overriding other keys.
- Unsupported reasoning levels report an error instead of silently clamping to another level.

### Verification status
- Release build, 22 logic checks, and native editor interaction/render checks pass.
- Model discovery was verified against Codex's live model list.
- All seven lower keys passed live override/default round-trip checks, with original settings restored. Preserved keys were checked after each change.
- Visual checks used the supplied Creator Micro reference at two native window sizes and 175% display scaling. A physical keypad press after this update remains a user hardware check.
