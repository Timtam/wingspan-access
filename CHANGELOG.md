# Changelog

All notable changes to Wingspan Access are listed here. New entries go under "Unreleased"; the
release workflow turns that section into the next version and uses it as the release notes.

## [Unreleased]

## [0.1.3] - 2026-11-09

### Added

- The game's notifications about your other online games are read ("It is your turn in an online
  game!", "1 minute left"), with the game's mode and players and where to find it. Opening the
  online menu says in how many games it is your turn.
- The goal board (G) is read line by line: it opens with its first line, Up and Down read one round
  (or one nectar habitat) and finally your points, Home and End jump to the first and last line.
- The final result is read line by line: it opens with the places, then Up and Down read each place
  and each category with everybody's points ("Birds: OvsXMGdgvD 31, you 37"), and the food left
  when it decides a tie. Home and End jump to the first and last line.

### Fixed

- The final result names every player correctly. Online both players can have the same color, and
  the other player was called "you" too; the players now come from the game's scores. The nectar
  standings on the goal board name players by their id the same way.
- Online, the other players' moves are announced: the mod now catches every speech bubble at a
  player's portrait ("I drew birds."), also those the game sets on the bubble directly, and it
  sums up their changes as soon as their moves arrive (before a power of yours that reacts to
  them), or else when your turn begins. It reports everything that changed since the player's
  last report, so a move is no longer lost when a round begins with the other player's turn.
  After loading an online game, the other players' state counts as the start.
- Bird powers that take a card from the tray (one with a certain nest, after refilling or
  resetting the tray) say which card is chosen ("Chosen: ... All cards chosen, Space confirms"),
  and chosen cards read "selected" like when drawing.
- The other players' hand size is read as it is now. Online it lagged a turn behind, so the summary
  of their moves left out cards they drew and said "drew a card" when they played a bird. The
  players zone (9), the status (8, also whose turn it is), the actions left and the tray had the
  same lag.
- A question over a task, like "Skip this power?" while drawing several birds, no longer closes
  the open zone: it pauses, and when the question is answered the zone goes on ("Back in zone 5"),
  so Up and Down read the cards again. The same goes for a choice the task opens, like nectar or
  seed for a die taken in zone 4: it is read right away and the arrows reach it, without leaving
  the zone with Escape first.
- Up and Down read the selected card line by line wherever the game's own selection is on a card in
  a single row (the tray while drawing, the hand), also without opening a zone.
- At the end of a round (and of the game) the birds still to activate are named correctly, also
  after the first one. Before, a bird whose condition was not met (one that counts double for a
  round goal it does not meet) was named as still to activate; such birds are now named as "cannot
  be activated". The task also tells that the game only offers the birds of the habitat on screen,
  and that 1 to 3 switch it.

## [0.1.2] - 2026-11-09

### Added

- Online invitations are announced when they arrive ("New invitation from ..."), also those still
  waiting when you log in.
- While the starting cards are chosen, 7 shows them in place of the empty hand: Left and Right
  choose a card, Up and Down read it line by line, Enter chooses it or puts it back.
- Online games announce what the other players did: the mod reads the speech bubbles the game
  shows at their portraits ("I played a bird: ... in the wetland") right away, and when your turn
  begins it sums up all their changes: birds played, eggs laid, food taken or spent, cards drawn.
- Drawing bird cards points to the tray zone (5), where Up and Down read a card line by line.

### Fixed

- "Your active games" in the online menu tells invitations (with the host), running games (whose
  turn, time left), finished and timed out games apart instead of calling all of them saved games.
- The short wait at the end of a round is called "Please wait" instead of its internal name.
- The hand (7) counts bird cards and bonus cards separately ("Hand: 2 bird cards, 1 bonus card").
- Birds on other players' boards (and other birds without a card on screen) include their power
  ("When activated: ..."), taken from the game's card texts.
- A bird tried in a habitat it does not fit in no longer stays chosen: the hand stayed open after
  the game's message, and every habitat action then tried to play that bird again. The mod now
  closes the hand with the card preview button ("Hand closed, no bird chosen any more"); leaving
  zone 7 closes it the same way.
- A selection left in the closed pause menu no longer takes Enter during the next task; Enter on a
  habitat takes its action again.
- The friend list ("Your friends", inviting friends to a custom game) can be used with the arrow
  keys: the game only lets one of its two parts take input and switches them with the shoulder
  buttons; the mod switches for you, Left and Right move between friends, Up goes to the search.
- In the starting choice, Up from the food row returns to the card you came from.
- Number keys on the open habitat overview say that it is open and O closes it.
- A bird on a board reads "0 of 4 eggs" once instead of a second "up to 4 eggs"; cards without
  a board read the nest and the egg limit in one line.
- If one of the mod's hooks into the game no longer fits after a game update, the others still work.
- The habitat overview (O) names each slot and what is in it ("Grassland 1, Carrion Crow, 2 of 2
  eggs", "the habitat's action", "empty") instead of reading the same bird for every slot. A task
  that starts under the open overview says so, and O closes it.
- Text fields can be left with the keyboard: Enter, Tab or Down Arrow end the input and move to
  the control below (the confirm button of the online name dialog), Up Arrow or Shift+Tab to the
  control above. Before, Enter opened the field again and only Escape, which does not save, worked.
- Buttons named by their icon no longer read all texts around them (the confirm button of the
  online profile read the whole profile).
- Enter in a zone no longer acts on the game's hidden selection when the item under the zone
  cursor cannot be chosen in the current task (a tray card while choosing the starting cards
  toggled a starting bird); the mod says "not selectable here" instead.
- Leaving a zone with Escape during a task also reads the game's selection, so you know where the
  arrow keys continue.
- The starting cards no longer begin with the players' speech bubbles ("Talk talk talk talking
  player").
- The nectar board names each player correctly in online games, where two players can have the
  same color (it said "you" twice).

## [0.1.1] - 2026-11-09

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
