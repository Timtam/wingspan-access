# Wingspan Access

A screen reader accessibility mod for the Steam version of **Wingspan** (Monster Couch / Stonemaier Games).
It speaks the game's menus through your screen reader and lets you control the game with the keyboard.

> **Status: early development.** The main menus and the custom game setup are accessible. A basic game
> against AI players can be played: the starting selection, the four actions (play a bird, gain food,
> lay eggs, draw cards), and announcements of what the other players did. The tutorial of the base
> game (all 14 chapters) can be played with the keyboard. Many bird powers, expansion features and the
> online modes have not been tested yet.

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
- **Cards read in full.** A bird card is read with its name, points, habitats, food cost, nest, egg
  limit, wingspan and power, one line at a time if you like. Birds on a board also report their eggs,
  cached food and tucked cards. Cards in your hand tell you where the rules let you play them now.
- **Zones.** The number keys open the parts of the game: your habitats, the birdfeeder, the bird
  tray, your supply, your hand, the round, the other players and the round goals. Inside every zone
  the arrow keys work the same way.
- **Turn announcements.** Each task is announced, for example "Choose 5 (1/5)" or "1 of 2 eggs laid",
  with the food you may take, the cards left to draw, the cost and payment of the bird you are playing
  and the card you drew. When another player finishes their turn, the mod summarizes what they did.
- **Round and game end.** Round end powers, the goal board with everyone's standings and the final
  ranking with points per category are read out.
- **Message history.** The last game messages can be read again at any time.
- **Tutorial.** Each hint of the tutorial is read in full, with the card it shows. Where a hint asks
  you to click something, the mod then says which keys do it, for example "Press 2 for grassland" or
  "Press 7 for your hand, choose Blue Jay with Left and Right and press Enter". The chapter list says
  which chapters you have completed.
- **News.** The news window that opens at start is announced with its first entry; Enter reads a
  news item in full.
- **Bird facts and calls.** The Latin name, the continents and the trivia text of every bird card can
  be read in the zones; S plays the bird's call and V the game's spoken trivia.
- **Expansions.** Games with all expansions (Europe, Oceania, Asia, bird packs) start and play,
  including nectar; many expansion powers are still untested.

## Requirements

- Windows 10 or 11.
- Wingspan from Steam.
- A screen reader such as NVDA or JAWS. Without one, Windows voices (SAPI/OneCore) are used.

## Installation

### With the installer

1. Download `WingspanAccessInstaller.exe` from the [latest release](../../releases/latest).
2. Start it. Windows may warn that the file is not signed: choose *More info*, then *Run anyway*.
3. The installer finds Wingspan in your Steam libraries. If it cannot, choose *Choose folder* and
   select `Wingspan.exe` in the game folder. The window shows the installed and the latest version.
4. Choose *Install*. The installer downloads the latest release and installs BepInEx (unless that
   version is there already) and the mod. It also turns off BepInEx's console window, which would
   take the focus away from the game.
5. Start Wingspan. The first start takes 1 to 3 minutes because BepInEx prepares its files. You will
   hear "Wingspan Access loaded. F1 for help."

The installer is a plain Windows dialog that works with screen readers: progress is announced, and
questions and results come as message boxes. It adds Wingspan Access to *Installed apps* in the
Windows settings and puts a copy of itself into the mod folder, which the mod uses for updates.

### Manual installation

1. Find the game folder: in Steam, open the context menu of Wingspan and choose *Manage*, then
   *Browse local files*. The folder contains `Wingspan.exe`.
2. Download `WingspanAccess-<version>.zip` from the [latest release](../../releases/latest) and
   extract it into the game folder, so that `winhttp.dll` and the `BepInEx` folder sit next to
   `Wingspan.exe`. The ZIP contains BepInEx 6 (build 793, IL2CPP, 32-bit) and the mod.
   If you have this BepInEx build already, extracting the folder `BepInEx\plugins\WingspanAccess`
   is enough. Then also set `Enabled = false` in the section `[Logging.Console]` of
   `BepInEx\config\BepInEx.cfg`, so that no console window opens next to the game.
3. Start Wingspan and wait 1 to 3 minutes the first time. You will hear "Wingspan Access loaded. F1
   for help."

## Updates

When the game has started, the mod asks GitHub whether a newer version is out and tells you if there
is one. F5 checks again at any time. To install an update, press F5 twice: the mod closes the game,
and the installer downloads and installs the new version and starts Wingspan again. This needs
`WingspanAccessInstaller.exe` in `BepInEx\plugins\WingspanAccess`, where the installer and the ZIP
put it; without it, F5 opens the download page instead. You can also run the installer again at any
time; it updates the mod and itself.

To turn off the check at start, set `CheckOnStart = false` in the section `[Updates]` of
`BepInEx\config\wingspan.access.cfg`. The check sends one request to the GitHub API and nothing else.

## Keys

While the mod is running it replaces the game's own keyboard shortcuts. The mouse keeps working.

| Key | Action |
| --- | --- |
| Arrow keys | Move through menus and through the choices of the current task. Inside a zone, see below |
| Enter | Select (gamepad A): activate a button, mark a card, take a die, lay an egg, play the card in your hand |
| Space | Continue or confirm (gamepad Y): start the game, confirm a selection or payment |
| Escape | Back (gamepad B). Inside a zone: back to the game |
| Backspace | Take the last picked food out again (paying for a bird, caching food on a bird) |
| X | Context action (gamepad X), for example trade a card for food or reroll the birdfeeder |
| G | Goal board |
| O | Overview of the habitats |
| I | Show the game's tooltips (gamepad right stick); F3 reads the description of the selected element |
| H | Hide the interface and show it again (gamepad left stick) |
| S | Play the call of the bird under the zone cursor or the selected card |
| V | Play the spoken trivia about that bird |
| Tab / Shift+Tab | Next / previous card |
| Page Up / Page Down | Read the previous / next message again (see "Message history") |
| Control+Page Up / Control+Page Down | Read the first / latest message |
| F10 | Pause menu |
| F1 | List the keys |
| F2 | Read the whole screen, including the current task |
| F3 | Read the description (tooltip) of the selected element |
| F5 | Check for updates. Twice: install the update (see "Updates") |
| F11 | Mod status: speech output, language, current screen |

Text fields (your online name, a player to invite) take every key while you type, and the mod
says each typed or deleted character. Enter, Tab or Down Arrow end the input and move to the
control below, usually the confirm button; press Enter there to confirm. Up Arrow or Shift+Tab move
to the control above. Escape closes the dialog without saving.

### Zones

During a game the number keys open zones. Inside a zone the keys are always the same: Left and
Right move between its items, Home and End jump to the first and last one, Up and Down read the
selected item line by line (for a bird: name, eggs and cards on it, points, habitats, food cost,
nest, egg limit, wingspan, power, where it can be played, then what the printed card tells about
the bird: Latin name, the continents it lives on and the trivia text), Escape goes back to the
game. S plays the call of the bird under the cursor and V the game's spoken trivia about it. Pressing
the zone key again repeats the overview. A new screen or task closes the zone, so the arrow keys
work for the game's own choices again.

| Key | Zone |
| --- | --- |
| 1, 2, 3 | Forest, grassland, wetland: the birds in that habitat. Also moves the view there, so Enter takes that habitat's action |
| 4 | Birdfeeder: the dice; while you take food, these are the dice to choose |
| 5 | Bird tray: the face-up cards and the deck |
| 6 | Your supply: food and the number of cards in your hand |
| 7 | Your hand. Opens it when you may play a bird; Enter plays the selected card. When a task asks for a card from your hand (to discard or tuck it), the cursor selects it and Enter marks it. While you choose your starting cards, 7 shows the dealt cards instead: Enter chooses a card or puts it back |
| 8 | Status: round, your actions left, whose turn it is |
| 9 | Players with their birds, food and cards, you first. Enter on a player makes 1 to 3 read that player's board; Enter on yourself ("You") switches back, and so does the start of your next turn |
| 0 | Round goals with the standings; Up and Down also read the game's explanation of the goal |

When the current task lets you choose the item under the cursor (a bird to lay an egg on, a card to
draw, a die to take), moving the cursor selects it in the game as well, so Enter acts on it.

When the game shows controller hints, the mod reads them as the matching keyboard keys.

### Message history

The mod keeps the last 200 game messages: turn announcements, what the other players did, tasks,
drawn cards, results. Page Up and Page Down step back and forth through them, Control+Page Up and
Control+Page Down jump to the first and the latest message. A new message puts you back at the end.
Announcements of the selected element, zones and information you asked for (F-keys) are not kept.

### Playing a turn

1. A new turn is announced ("Your turn. Round 1 of 4. 8 turns left"). Press Enter.
2. Choose an action. The habitat in view is announced. 1, 2 and 3 move to forest (gain food),
   grassland (lay eggs) and wetland (draw cards); Enter takes the action of the habitat you are in.
3. To play a bird, first choose its habitat with 1, 2 or 3, then press 7 for your hand, choose a card
   with Left and Right (each card says where the rules let you play it now) and press Enter. The mod
   reads the cost and the proposed payment; Space accepts it, the Change button lets you pick the
   food yourself (Backspace takes the last one out again). If the place costs eggs, pick the
   birds to take them from with Enter. Escape closes the hand again.
4. In the task that follows, Enter marks things (cards, dice, birds for eggs) and Space confirms.
   A card drawn from the deck is shown first: one Space puts it into your hand, the next confirms.
   The mod says how much food you may take, how many cards are left to draw and how many eggs to lay.
   Where the slot offers a trade (for example a card for one more die in the forest), X starts it;
   then choose the card with 7 and Enter, and confirm with Space.
5. When the other players have moved, the mod tells you what they did. Bird powers that give you an
   extra action between turns are announced as "out-of-turn action".
6. At the end of a round the mod names the birds whose "round end" powers you can activate, then
   reads the goal board. At the end of the game it reads the ranking with the points per category.

In the play menu, "Your games" lists saved games with mode, round, turns left, players and when the
game was started; Enter continues a game.

## Uninstalling

Open *Installed apps* in the Windows settings and uninstall *Wingspan Access*, or start the installer
and choose *Uninstall*. You are asked whether to remove BepInEx as well; keep it if you use other
BepInEx mods for Wingspan.

By hand: delete `BepInEx\plugins\WingspanAccess` to remove only the mod. To remove BepInEx as well,
delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` (if you installed
BepInEx yourself) and the folders `BepInEx` and `dotnet` from the game folder.

## Building from source

Requirements:

- .NET 8 SDK and PowerShell 7.
- Visual Studio 2022 or newer with the C++ workload, plus CMake, Ninja and Git. These are only needed
  to build Prism.
- Optional: Wingspan with BepInEx installed and started once. If the build finds it, it references
  the files BepInEx generated in `BepInEx\interop`. Otherwise it uses the reference assemblies in
  `lib\interop` and the BepInEx files that `tools\fetch-bepinex.ps1` downloads, like the CI does.

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

To build a release the way the CI does, run `tools\build-release.ps1`. It writes
`dist\WingspanAccess-<version>.zip`, `dist\WingspanAccessInstaller.exe` and `dist\SHA256SUMS.txt`.

`lib\interop` holds reference assemblies of the game: only the type and member signatures the mod
compiles against, no method bodies. If a game update changes them, make them again with
`tools\make-reference-assemblies.ps1` (needs the game with BepInEx started once).
More details about the build, the installer and the release packages are in
[docs/build-and-release.md](docs/build-and-release.md).

`tools/cycle-game.ps1` closes the game, builds and deploys the mod, starts the game again and waits
for the main menu. That is handy while developing.

`tools/InteropInspector` lists the game's classes and their members, which helps when you work on
the mod:

```bash
dotnet run --project tools/InteropInspector -- "^GamepadController$" --member "Screen"
```

## Releasing

1. While you work, add your changes under `## [Unreleased]` in `CHANGELOG.md`.
2. On GitHub, open *Actions*, choose *Release* and *Run workflow* on the default branch. Choose the
   part of the version to raise (patch, minor or major) or type an exact version. A version in
   `Directory.Build.props` that has not been released yet, like 0.1.0 for the first release, is
   used as it is.
3. The workflow moves the "Unreleased" entries into a section for the new version, sets the version
   in `Directory.Build.props`, builds everything, commits "Release vX.Y.Z", tags it and publishes a
   GitHub release. The changelog section becomes the release notes; the ZIP, the installer and
   `SHA256SUMS.txt` become its downloads.

The mod and the installer look for updates in the latest release of the repository they were built
from. The *CI* workflow builds every push to the default branch and every pull request and keeps the
ZIP and the installer as artifacts of the run. If the default branch is protected, the release
workflow needs permission to push to it.

## Translations

The mod's own strings live in `src/WingspanAccess/Localization/<language code>.json`. The file names
use the game's language codes, for example `en`, `de`, `fr`, `pt-BR`. To add a language, copy
`en.json`, translate the values, and keep the `{0}` placeholders.

## Troubleshooting

- The log is in `BepInEx\LogOutput.log` in the game folder. Every spoken message is also written there.
- F11 tells you which speech output is in use.
- The installer writes its log to `%TEMP%\WingspanAccessInstaller.log`.
- After a game update, start the game once and wait; BepInEx regenerates its files.

## License

Copyright (C) 2026 Toni Barth

Wingspan Access is free software: you can redistribute it and/or modify it under the terms of the
GNU General Public License as published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version. It is distributed in the hope that it will be
useful, but without any warranty. See [LICENSE](LICENSE) for the full text.

## Credits and licenses

- [BepInEx](https://github.com/BepInEx/BepInEx), LGPL-2.1. The release ZIP contains it unchanged.
- [Prism](https://github.com/ethindp/prism) by Ethin Probst, MPL-2.0.
- Wingspan © Monster Couch and Stonemaier Games. This project contains no game code or assets;
  `lib\interop` holds only the type and member signatures that compiling the mod needs.
