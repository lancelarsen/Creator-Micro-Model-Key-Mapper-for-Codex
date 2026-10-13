# Micro Model Presets

A small Windows notification-area app that lets Work Louder Creator Micro buttons select a Codex model and reasoning level. The first run uses H = GPT-6 Luna / Extra High, I = GPT-6.1 Sol / High, and J = GPT-6 Astra / Low. Click the app icon to change those assignments.

![Three highlighted H, I, and J keycaps](assets/codex-micro-presets.png)

This is an independent community project. It is not affiliated with or endorsed by OpenAI or Work Louder.

## What it does

- Listens to the Micro's vendor HID notifications in read-only mode; it never takes ownership of or writes to the keyboard.
- On a physical H/I/J press, applies that key's selected model and reasoning level through Codex's visible controls, then verifies the resulting composer label.
- A single click on the tray icon opens the assignment window. Choose a model and reasoning level for each key. The model list can be refreshed from Codex, and typed model names are accepted.
- Settings and runtime logs stay in the current Windows user's `%APPDATA%\CodexMicroPresets` folder. The app sends no telemetry and does not log normal keyboard text or chat contents.
- Right-click the tray icon to pause or exit.

Codex must be open and in front when you press H, I, or J. Native actions assigned to those three physical positions should be cleared in Creator Micro settings to prevent a second action from running. Leave other keys as you prefer. Set the Micro to the Codex input layer that exposes vendor action notifications on your firmware.

## Build and run

Requirements: Windows 10 or 11 x64. Building requires the .NET 10 SDK; running the published app requires the .NET 10 Desktop Runtime.

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

Run `publish\CodexMicroPresets.exe`. On first run, click the tray icon and choose your H/I/J assignments. The initial assignments match those listed above.

To start the app automatically when you sign in, run:

```powershell
.\scripts\Install-Startup.ps1 -ApplicationPath .\publish\CodexMicroPresets.exe
```

To disable automatic startup, run `scripts\Uninstall-Startup.ps1`. Neither script removes application files or your saved assignments.

## Model and reasoning choices

Use **Refresh models from Codex** in the assignment window to read the options currently shown by Codex. If your model is absent, type its exact displayed name. Each reasoning choice maps to a step in the model's available effort slider; if a model has fewer steps, higher choices select its highest available step. Codex's display label can differ by model.

## Project layout

- `ModelSelector.cs`: Codex window targeting, visible model controls, and selection verification.
- `MicroInputListener.cs` and `ReportDecoder.cs`: read-only device event capture and parsing.
- `PresetConfigurationForm.cs` and `PresetStore.cs`: per-key editor and per-user settings.
- `assets/codex-micro-presets.svg`: source for the three-key icon; `tools/GenerateIcon.ps1` generates the Windows icon files.
- `scripts`: add or remove the per-user Windows startup shortcut.

## License

MIT. See [LICENSE](LICENSE).
