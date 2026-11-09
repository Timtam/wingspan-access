# Changelog

All notable changes to Wingspan Access are listed here. New entries go under "Unreleased"; the
release workflow turns that section into the next version and uses it as the release notes.

## [Unreleased]

### Added

- Installer for Windows (`WingspanAccessInstaller.exe`): finds Wingspan in your Steam libraries,
  installs BepInEx and the mod, updates and uninstalls them, and adds an entry to "Installed apps".
- Update check: after the game starts, the mod tells you when a new version is available. F5
  checks on demand; pressing F5 twice closes the game, installs the update and starts the game again.
- Spoken menus: screens, popups, buttons, options, sliders and their values, including the custom
  game setup, the game mode and expansions popups and the saved games in the play menu.
- Keyboard control through the game's gamepad interface: Enter selects, Space confirms, Escape goes
  back, arrow keys move.
- Zones on the number keys: 1 to 3 your habitats (also moves the view), 4 birdfeeder, 5 bird tray,
  6 supply, 7 hand, 8 status, 9 players, 0 round goals. Left and Right move between items, Up and
  Down read the details line by line, Escape goes back to the game.
- Message history on Page Up and Page Down, Control for the first and the latest message.
- Turn announcements: each task with the food you may take, cards left to draw, the drawn card, eggs
  to lay, the cost and payment of the bird you play, food cached on a bird, bonus cards drawn.
- Other players' turns are summarized: birds played, food gained and spent, eggs, cards.
- Bird and bonus cards read in full; hand cards say where the rules let you play them now.
- Round end powers, the goal board with everyone's standings and points, and the final ranking.
- Backspace takes the last picked food out of a payment or a cache again.
- Tutorial: every hint is read in full with the card it shows, followed by the keys for the task
  (for example "Press 7 for your hand, choose Blue Jay with Left and Right and press Enter"). All 14
  chapters of the base game can be played with the keyboard; the chapter list says which chapters are
  completed.
- The news window at start is announced with its first entry; Enter reads a news item in full.
- X is the context action (trade a card, egg or food in a habitat, reroll), I shows the game's
  tooltips, H hides the interface, G opens the goal board and O the overview of the habitats.
- Zone 9: Enter on a player makes 1 to 3 read that player's board; Enter on "You" switches back.
- Speech and braille through Prism (NVDA, JAWS and other screen readers, SAPI fallback). The mod
  follows the game's language; its own messages are in English and German.
- License: GNU General Public License, version 3 or later.
