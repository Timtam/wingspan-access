# Wingspan Access

A screen reader accessibility mod for the Steam version of **Wingspan** (Monster Couch / Stonemaier Games).
It speaks the game's menus through your screen reader and lets you control the game with the keyboard.

> **Status: early development.** The main menu, settings, news, the play menu and the custom game setup
> are accessible. Gameplay itself (the board, cards and turns) is not accessible yet.

The mod is unofficial and not affiliated with Monster Couch or Stonemaier Games.

## Features

- **Spoken menus.** Screens, popups, buttons, options, sliders and their values are announced when they
  get focus or change.
- **Keyboard control.** The mod drives the game's built-in gamepad interface, so every menu that works
  with a controller works with the keyboard, too.
- **Screen readers.** Speech and braille go through [Prism](https://github.com/ethindp/prism), which
  supports NVDA, JAWS, ZoomText, System Access, ZDSR and others, and falls back to SAPI or OneCore voices.
- **Follows the game's language.** The game's own texts are read in whatever language the game uses. The
  mod's own messages are available in English and German; other languages fall back to English.
- **Game tooltips on demand.** Descriptions that sighted players see on mouse-over can be read with a key.

## Requirements

- Windows 10 or 11.
- Wingspan from Steam. The game is a 32-bit application, so all downloads below must be the **x86**
  (32-bit) versions.
- A screen reader such as NVDA or JAWS. Without one, Windows voices (SAPI/OneCore) are used.

## Installation

1. Find the game folder: in Steam, open the context menu of Wingspan and choose
   *Manage*, then *Browse local files*. The folder contains `Wingspan.exe`.
2. Download **BepInEx 6 bleeding edge, IL2CPP, win-x86**, build 793 or newer, from
   <https://builds.bepinex.dev/projects/bepinex_be>. The file is named like
   `BepInEx-Unity.IL2CPP-win-x86-6.0.0-be.793+dbc82f0.zip`.
3. Extract the zip into the game folder, so that `winhttp.dll` and the `BepInEx` folder sit next to
   `Wingspan.exe`.
4. Start Wingspan once and wait. The first start takes about a minute because BepInEx prepares its
   files. When the main menu appears, quit the game.
5. Put the mod into `BepInEx\plugins\WingspanAccess` inside the game folder. The folder needs
   `WingspanAccess.dll` and `prism.dll`. Release downloads will be published later; until then,
   build the mod yourself (see below).
6. Recommended: open `BepInEx\config\BepInEx.cfg` in a text editor. In the section
   `[Logging.Console]`, set `Enabled = false`. This stops a console window from opening next to the game.
7. Start Wingspan. You will hear "Wingspan Access loaded. F1 for help."

## Keys

While the mod is running it replaces the game's own keyboard shortcuts. The mouse keeps working.

| Key | Action |
| --- | --- |
| Arrow keys | Move through the menu |
| Enter | Select (gamepad A) |
| Escape | Back (gamepad B) |
| Space | Continue or confirm (gamepad Y) |
| Shift+F10 or Applications key | Context action (gamepad X) |
| Page Up / Page Down | Previous / next habitat |
| Control+Page Up / Control+Page Down | Show other players; switch pages in some popups, for example the game mode |
| Tab / Shift+Tab | Next / previous card |
| F10 | Pause menu |
| F1 | List the keys |
| F2 | Read the whole screen |
| F3 | Read the description (tooltip) of the selected element |
| F11 | Mod status: speech output, language, current screen |

When the game shows controller hints, the mod reads them as the matching keyboard keys.

## Uninstalling

Delete `BepInEx\plugins\WingspanAccess` to remove only the mod. To remove BepInEx as well, delete
`winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` and the folders `BepInEx`
and `dotnet` from the game folder.

## Building from source

Requirements:

- .NET 8 SDK.
- Visual Studio 2022 or newer with the C++ workload, plus CMake, Ninja and Git. These are only needed
  to build Prism.
- Wingspan with BepInEx installed and started once. The build references the files BepInEx generates
  in `BepInEx\interop`.

Steps:

1. Build Prism as a 32-bit DLL:

   ```bash
   tools\build-prism-x86.cmd
   ```

   This creates `third_party\prism\x86\prism.dll`.
2. Build the mod and copy it into the game. The game must be closed:

   ```bash
   dotnet build src/WingspanAccess/WingspanAccess.csproj -c Release -p:DeployToGame=true
   ```

   If Wingspan is not in the default Steam folder, add `-p:WingspanDir="D:\path\to\Wingspan"`.

`tools/InteropInspector` lists the game's classes and their members, which helps when you work on
the mod:

```bash
dotnet run --project tools/InteropInspector -- "^GamepadController$" --member "Screen"
```

## Translations

The mod's own strings live in `src/WingspanAccess/Localization/<language code>.json`. The file names
use the game's language codes, for example `en`, `de`, `fr`, `pt-BR`. To add a language, copy
`en.json`, translate the values, and keep the `{0}` placeholders.

## Troubleshooting

- The log is in `BepInEx\LogOutput.log` in the game folder. Every spoken message is also written there.
- F11 tells you which speech output is in use.
- After a game update, start the game once and wait; BepInEx regenerates its files.

## Credits and licenses

- [BepInEx](https://github.com/BepInEx/BepInEx), LGPL-2.1.
- [Prism](https://github.com/ethindp/prism) by Ethin Probst, MPL-2.0.
- Wingspan © Monster Couch and Stonemaier Games. This project contains no game files.
