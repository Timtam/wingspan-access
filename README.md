# Wingspan Access

A screen reader accessibility mod for the Steam version of **Wingspan** (Monster Couch / Stonemaier Games).
It speaks the game's menus through your screen reader and lets you control the game with the keyboard.

> **Status: early development.** The main menus and the custom game setup are accessible. A basic game
> against AI players can be played: the starting selection, the four actions (play a bird, gain food,
> lay eggs, draw cards), and announcements of what the other players did. The first chapters of the
> tutorial work with the keyboard. Many bird powers, expansion features and the online modes have not
> been tested yet.

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
| Arrow keys | Move through menus and through the choices of the current task. Inside a zone, see below |
| Enter | Select (gamepad A): activate a button, mark a card, take a die, lay an egg, play the card in your hand |
| Space | Continue or confirm (gamepad Y): start the game, confirm a selection or payment |
| Escape | Back (gamepad B). Inside a zone: back to the game |
| Backspace | Take the last picked food out again (paying for a bird, caching food on a bird) |
| X | Context action (gamepad X), for example trade a card for food or reroll the birdfeeder |
| G | Goal board |
| O | Overview of the habitats |
| Tab / Shift+Tab | Next / previous card |
| Page Up / Page Down | Read the previous / next message again (see "Message history") |
| Control+Page Up / Control+Page Down | Read the first / latest message |
| F10 | Pause menu |
| F1 | List the keys |
| F2 | Read the whole screen, including the current task |
| F3 | Read the description (tooltip) of the selected element |
| F11 | Mod status: speech output, language, current screen |

### Zones

During a game the number keys open zones. Inside a zone the keys are always the same: Left and
Right move between its items, Home and End jump to the first and last one, Up and Down read the
selected item line by line (for a bird: name, eggs and cards on it, points, habitats, food cost,
nest, egg limit, wingspan, power, where it can be played), Escape goes back to the game. Pressing
the zone key again repeats the overview. A new screen or task closes the zone, so the arrow keys
work for the game's own choices again.

| Key | Zone |
| --- | --- |
| 1, 2, 3 | Forest, grassland, wetland: the birds in that habitat. Also moves the view there, so Enter takes that habitat's action |
| 4 | Birdfeeder: the dice; while you take food, these are the dice to choose |
| 5 | Bird tray: the face-up cards and the deck |
| 6 | Your supply: food and the number of cards in your hand |
| 7 | Your hand. Opens it when you may play a bird; Enter plays the selected card. When a task asks for a card from your hand (to discard or tuck it), the cursor selects it and Enter marks it |
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

`tools/cycle-game.ps1` closes the game, builds and deploys the mod, starts the game again and waits
for the main menu. That is handy while developing.

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
