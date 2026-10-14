# Creator Micro Model Keys

A Windows tray app by [Lance Larsen](https://www.LanceLarsen.com), built to put your favorite Codex models and reasoning levels one keypress away. Click a key in the hardware-shaped editor to assign a preset to any of the seven lower keys, or choose **(default)** to keep its Codex action.

The initial presets are H = GPT-6 Luna / Extra High, I = GPT-6.1 Sol / High, and J = GPT-6 Astra / Low. G, K, L, and M retain their native actions. Existing three-key settings migrate automatically.

![Three highlighted H, I, and J keycaps](assets/codex-micro-presets.png)

This is an independent community project. It is not affiliated with or endorsed by OpenAI or Work Louder.

## What it does

- Listens to the Micro's vendor HID notifications in read-only mode; it never takes ownership of or writes to the keyboard.
- On an overridden physical G–M press, applies that key's model and reasoning level through Codex's visible controls, then verifies the resulting composer label. The six colored chat keys are never intercepted.
- A single click on the tray icon opens the dark keypad editor. Select any lower key, enable or disable its override, choose a model and reasoning level, then save. Changes are drafts until saved; closing a changed editor asks whether to discard them.
- Changing override/default mode opens Codex's Creator Micro settings. The app saves the original keycap and action, replaces the action with composer focus for an override, and restores it for default mode. This prevents Send/New Chat/dictation from firing alongside model selection. It checks the result and attempts rollback on errors. The dictation mode setting is not modified.
- Settings and runtime logs stay in the current Windows user's `%APPDATA%\CodexMicroPresets` folder. The app sends no telemetry and does not log normal keyboard text or chat contents.
- Right-click the tray icon to pause or exit.

Codex must be open and in front when you press a preset key. Set the Micro to the Codex input layer that exposes vendor action notifications on your firmware (Input 4 on the original setup). While saving settings or refreshing models, the tray listener ignores preset presses to avoid competing with configuration.

Saving override/default changes needs the Codex Creator Micro page to be available. Configuration uses direct accessibility actions through the profile menu, so it can navigate settings without a foreground keyboard shortcut. The helper keeps each keycap in place because reusing a keycap would swap physical positions. Unsupported/custom layouts fail without silently guessing key positions. Native UI integration depends on Codex's accessible control labels and may need adjustment after app updates.

## Build and run

Requirements: Windows 10 or 11 x64. Building requires the .NET 10 SDK and `maui-windows` workload; running the published app requires the .NET 10 Desktop Runtime.

```powershell
dotnet workload install maui-windows
.\scripts\Publish.ps1
```

Publish both projects together; keep the `editor` subfolder beside the helper. The MAUI editor includes its own runtime. Closing it leaves the helper listening.

Run `publish\CodexMicroPresets.exe`. Click the tray icon, choose your assignments, and save to set up the native bindings. Use `--listen --settings` to start the tray listener with its editor open.

To install both components, keep a backup of the previous installation, and start automatically at sign-in, run:

```powershell
.\scripts\Install.ps1
```

To register a custom installation location for startup, run:

```powershell
.\scripts\Install-Startup.ps1 -ApplicationPath .\publish\CodexMicroPresets.exe
```

To disable automatic startup, run `scripts\Uninstall-Startup.ps1`. Neither script removes application files or your saved assignments.

## Model and reasoning choices

Use **Refresh from Codex** to read the models currently shown in your account. The editor caches the list and its last successful check time. Enable **Refresh models when this window opens** and save to check automatically when opening settings. This briefly opens Codex's model menu; there is no background polling while you work. If Codex is unavailable, the last successful list remains usable. An exact displayed model name can also be typed.

Reasoning choices map to slider steps and the resulting label is verified, including Codex's Light/Low and Extended/High aliases. Unsupported levels report an error instead of silently using a lower level. Model discovery refreshes names, not per-model reasoning capabilities; the helper does not automatically reassign your saved keys when the catalog changes.

## Development checks

```powershell
dotnet build -c Release
dotnet .\bin\Release\net10.0-windows\CodexMicroPresets.dll --self-test
.\publish\editor\CreatorMicroEditor.exe --ui-check .\artifacts\ui
```

The native UI check exercises all seven key selections and default/override toggles and renders two window sizes. `--verify-bindings` is an interactive integration check: with Codex open, it temporarily changes and restores all seven lower keys. Do not run it while using those keys. `--refresh-models` checks and caches the current model list. `--default-key K` restores a single key to its saved native action; `--restore-layout` stops the listener and restores every overridden key.

## Project layout

- `ModelSelector.cs`: Codex window targeting, visible model controls, and selection verification.
- `MicroInputListener.cs` and `ReportDecoder.cs`: read-only device event capture and parsing.
- `Editor/`: native MAUI XAML editor, responsive keypad, and draft settings.
- `EditorProtocol.cs` and `EditorServer.cs`: current-user-only named pipe; UI requests load, refresh, or save while the helper owns native operations.
- `PresetStore.cs`: compatible per-user settings.
- `ModelCatalog.cs`: cached model discovery and optional refresh on open.
- `MicroBindings.cs`: native Codex keycap/action backup, change, verification, and rollback.
- `assets/codex-micro-presets.svg`: source for the three-key icon; `tools/GenerateIcon.ps1` generates the Windows icon files.
- `scripts`: add or remove the per-user Windows startup shortcut.

## License

MIT. See [LICENSE](LICENSE).
