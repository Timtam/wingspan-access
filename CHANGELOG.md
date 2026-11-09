# Changelog

All notable changes to Wingspan Access are listed here. New entries go under "Unreleased"; the
release workflow turns that section into the next version and uses it as the release notes.

## [Unreleased]

### Added

- Text fields (online name, invite a player) say each typed and deleted character.
- Online games: zone 8 tells the time left for the turn, and the mod warns when it runs low.
- Escape answers No in the game's yes/no questions (for example "join an online game?").

### Fixed

- Yes/no questions read their icon buttons as "Yes" and "No" instead of repeating the question;
  other buttons without text are named after what they do.
- The online menu tells the realtime and asynchronous "Standard game" apart by their headers.
- Tutorial hints and key hints are no longer cut off by the screen that opens after them.
- Icons are no longer read twice next to the same word in brackets (the nest types in chapter 2).
- The German name of the birdfeeder die icon fits the game's sentences ("jedes Futter").
- The pause menu no longer reads the opponents' speech bubbles before "Resume", and its
  positions no longer count the invisible background button.
- Options side by side are counted from left to right, as the arrow keys move: in the yes/no
  questions "No" is 1 of 2 and "Yes" 2 of 2.
- Enter on an option that is already chosen repeats "selected" instead of staying silent.
- German: the plural of seeds is read as "Samen" instead of the English "seeds".
- Food counts use the plural ("2 Wirbellose, 2 Früchte" instead of "2 Wirbelloses, 2 Frucht").
- The chapter summary after a tutorial chapter no longer repeats its title before the buttons.

## [0.1.0] - 2026-11-09

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
- Bird facts: the zones read each bird's Latin name, the continents it lives on and the trivia text
  of the card. S plays the bird's call, V the game's spoken trivia about it.
- Speech and braille through Prism (NVDA, JAWS and other screen readers, SAPI fallback). The mod
  follows the game's language; its own messages are in English and German.
- License: GNU General Public License, version 3 or later.
