# Changelog

## Unreleased

### Added
- Native Windows MAUI keypad editor for all seven lower keys, with model and reasoning assignments.
- Settings popup with launch-on-startup and refresh-on-opening preferences, enabled by default for new installations.
- Creator website and Open Source buttons, plus a creator link on the main screen.
- Dark keycap app icon with the Codex logo, including nine Windows icon sizes.

### Changed
- Renamed the app to Creator Micro - Model Key Mapper for Codex.
- Compact 880 x 670 default and minimum window size, with the keyboard and model panel always side by side.
- Default keys remain blank; model-mapped keys show the Codex logo.
- Native dropdowns follow Codex's model order and show reasoning choices from highest to lowest.
- Done saves assignments and closes the panel; Cancel discards drafts and closes it.
- Smooth panel transitions move the keyboard before revealing the model panel.
- A spinner in Done indicates when assignments are being saved.
- Preferences in Settings save immediately; existing assignments and native action backups remain compatible.

### Fixed
- Wait for Codex's model list to finish loading and preserve the previous catalog if a refresh is incomplete.
- Allow the helper to focus Codex for configuration and return focus to the mapper after model refresh.
- Preserve the user's startup opt-out when updating the installation.
- Keep the popup pointer attached during transitions and support high Windows display scaling.

### Verification
- Release publishing and native editor interaction/render checks pass.
- All seven lower keys have been checked for override/default restoration; physical Luna Extra High selection was confirmed.
- Native visual checks cover compact and wider windows at 175% display scaling.
