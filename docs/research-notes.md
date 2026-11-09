# Wingspan accessibility mod: research notes

Findings from the initial investigation (2026-11-08). Everything below was read from the installed
game files (IL2CPP metadata), not from decompiled code, so member names are reliable but signatures
and behaviour still need to be confirmed against the generated interop assemblies.

## Game build

- Steam app 1054490, build id 25936483, installed at
  `C:\Program Files (x86)\Steam\steamapps\common\Wingspan`.
- Unity **6000.3.10f1**, scripting backend **IL2CPP**, **32-bit x86** (`Wingspan.exe`,
  `GameAssembly.dll` and `UnityPlayer.dll` are all PE i386).
- `global-metadata.dat` is **version 39**. The header is a list of (offset, size, count) triples, and
  many indices are compressed to 2 bytes.
- 117 assemblies. The game code is in **`ScriptAssembly.dll`** (8,807 types). `Assembly-CSharp.dll`
  only contains the Rewired integration and demos.
- Third-party components: Rewired (input), uGUI with TextMeshPro, I2 Localization (`I2Loc.dll`),
  Addressables, Nakama (online play), Steamworks.NET, GOG Galaxy, Quantum Console (`QFSW.QC`),
  QAATT (QA automation), Anti-Cheat Toolkit. The ACTk assemblies are present, but no detector usage
  was found in the game code; only the ACTk example strings show up.

## UI and navigation architecture

The console ports left a complete gamepad UI in the PC build. That is the main hook for the mod.

- **`GamepadController`** (MonoBehaviour, singleton property `I`):
  - Keeps a screen stack: `Register`, `Unregister`, `PeekScreenFromStack`, `GetScreensOnStack`,
    and the events `StackTopChanged`, `ScreenRegistered` and `ScreenUnregistered`.
  - Has action handlers: `Accept`, `Back`, `Submit`, `Context`, `HabitatUp`/`HabitatDown`,
    `NextCard`/`PreviousCard`, `ContextNavigationX/Y_Positive/Negative`, `PreviewPlayerLeft`/`PreviewPlayerRight`,
    `ToggleCardPreview`, `ToggleDiscards`, `ToggleHabitatsOverview`, `ShowGoals`, `ShowTooltips`,
    `ToggleRulebook`, `Pause` and `HideInterface`.
  - Tracks the controller type: `LastControllerIsMouseOrKeyboard`, `LastControllerIsKeyboard`, `QaattControllerId`.
- **`GamepadScreen`** (MonoBehaviour) is the base class of every screen and popup (35 direct subclasses):
  - Selection: `_lastSelection`, `TrySelectDefault`, `TryRestoreSelection`,
    `TrySelectSomethingIfNothingSelected`, `SaveSelection`.
  - Also `IsPopup`, `ScreenPriority`, and a virtual handler for every action.
- **`BaseGamepadPopup : GamepadScreen`**: `Show`, `Hide`, `IsVisible`. Subclasses include `YesNoPopup`,
  `ConfirmPopup`, `InfoPopup`, `WaitPopup` and `AwaitablePopup`.
- **`Decision : GamepadScreen`** is the base of 369 `*_Decision` classes (actions and bird powers).
  Relevant members: `CurrentActiveDecision`, `ID`, `OnShow`, `Finish`, `Back`,
  `CheckIfRequirementsAreFulfilled`, `GetRequirementNotFulfilledContext`, `StartChildDecision`
  and `RegisterForQaatt`. There are shared panels as well, for example `ChooseFoodFromSupplyPanel`,
  `ChoosePlayerPanel` and the `View.Decisions.PlayBird.States.*` state controllers for the bird payment flow.
- Selection runs through uGUI `Selectable`s and the `EventSystem`: `GamepadCardSlot.OnSelect`/`OnDeselect`,
  `BirdSlot.OnSelect` and `CardSlotSelectables`.
- Rewired actions (`RewiredConsts.Action`): Submit, Back, Context, Accept, HabitatUp, HabitatDown,
  NavigationX, NavigationY, ToggleCardPreview, ToggleDiscards, NextCard, PreviousCard, Pause,
  ShowGoals, ShowTooltips, PreviewPlayerLeft/Right, HideInterface, ToggleHabitatsOverview,
  ToggleFluffs, ToggleRulebook, ExchangeResources and HoldB.
- The Rewired custom controller **`CustomController_QAATT`** (Submit, Back, Context, Accept,
  HabitatUp/Down, NextCard, PreviousCard, Pause, ToggleGoals and others, plus the axes NavigationX/Y)
  is used by the QA automation to inject input. It is a candidate for injecting keyboard input as
  "gamepad" input.
- The `InputMode` enum has the values Mouse, Touch and Gamepad (see `PlayerHandUI.InputMode`).

## Game model

- **`GameStateDriver`**: `Players`, `CurrentPlayerController`, `Deck`, `BonusDeck`, `BirdFeeder`,
  `GoalBoard`, `ScoreCalculator`, `DuetMap`, `ActiveDLCs`, `Operations`/`CurrentOperation`.
- **`BirdData`**: `BirdName` (enum), `LatinName`, `VictoryPoints`, `EggLimit`, `WingspanCM`,
  `NestType`, the habitats (`HabitatForest`/`HabitatGrass`/`HabitatWetlands`), the food costs
  (`FoodWild`/`FoodInvertebrate`/`FoodSeed`/`FoodFruit`/`FoodFish`/`FoodRodent`/`FoodNectar`),
  `PowerType`, `BirdPowerHeader` and the regions. Related: `BirdDatabase`, `BonusCardData` and `BonusCardDatabase`.
- UI classes:
  - `PlayerHandUI`: `SelectedCard`, `NextCard`/`PreviousCard`, `ValidateBirdToPlay`.
  - `BirdfeederUI`: `Dice`.
  - `BirdSlot`: `Card`, `Habitat`.
  - `CardView`, `BirdCardView`, `BonusCardView` and `TooltipsManager`.

## Localization

- I2 Localization: `I2.Loc.LocalizationManager.CurrentLanguageCode` and `CurrentLanguage`,
  `OnLocalizeEvent`, `GetTranslation(term)` and `GetTermsList`.
- Term names follow patterns such as `BirdCards/Names/...`, `BirdCards/Powers/<id>`,
  `BirdCards/Fluffs/...`, `BonusCards/Description/<id>` and `Decisions/A/...`.
- Language codes found in the code: en, de, fr, es, it, pl, pt-BR, ru, ja, ko, tr and nl. Chinese
  voice-over assets also exist.
- Plan: the mod's own strings live in per-language files selected by `CurrentLanguageCode`, with
  English as the fallback. Game terms such as food, habitats and bird names are reused through
  `GetTranslation`.

## Tooling status (as of 2026-11)

- Cpp2IL `2022.1.0-pre-release.21` (Feb 2026) added metadata v35, v38 and v39. Upstream Il2CppDumper
  fails on v39.
- BepInEx 6 bleeding edge: build #755 added metadata v23–106 ("Unity 6+"), and build #774 made
  `UnityInput`/`KeyboardShortcut` available on IL2CPP. The latest build, #793 (2026-11-05), ships
  `BepInEx-Unity.IL2CPP-win-x86`.
- MelonLoader 0.7.2 (Mar 2026) bundles Cpp2IL pre-release.21. Version 0.7.3 (May 2026) ships
  `MelonLoader.x86.zip`.
- Prism v0.18.3 (2026-09-30), MPL-2.0:
  - Windows backends: NVDA, JAWS, SAPI, OneCore, UIA, ZoomText, SenseReader, PC-Talker, ZDSR and
    BoyPCReader, plus System Access and Window-Eyes as legacy backends.
  - The release only ships `windows-x64` and `windows-arm64`, so the **x86 DLL has to be built from
    source**. Visual Studio 2026, CMake and Ninja are installed on this machine.
  - The .NET binding `prismatoid` 0.4.0 targets net8.0 and net10.0 only. A thin P/Invoke wrapper is
    the safer choice inside the loader's runtime.

## Proof of concept results (2026-11-08)

### Loader

- BepInEx 6.0.0-be.793 (`BepInEx-Unity.IL2CPP-win-x86`) works on Unity 6000.3.10f1 with metadata v39.
- On the first start, Cpp2IL and Il2CppInterop generate `BepInEx/interop` (151 assemblies) in about
  65 seconds. Later starts take a few seconds.
- The only warning is "Class::Init signatures have been exhausted, using a substitute!", which is harmless.
  Class injection (custom MonoBehaviour) works.
- Memory: about 3 GB during the first start (interop generation), then about 1.2 GB in normal use.
  This is still within the 32-bit limit.
- BepInEx config changes made for development:
  - `[Logging.Console] Enabled = false`, because the console window steals focus.
  - `[Logging.Disk] WriteUnityLog = true`, so the game's own messages land in `LogOutput.log`, for
    example `[GamepadController] Register - NewsCenter_Screen - Screen stack top: ...`.
  - The disk log is buffered. Set `InstantFlushing = true` when reading it live.

### Speech

- Prism v0.18.3 builds as an x86 DLL with `tools/build-prism-x86.cmd`. It has no CRT dependencies;
  it only imports system DLLs.
- `prism_registry_create_best` picks NVDA when NVDA is running, and `prism_backend_output` speaks and brailles.

### Screens, focus and language

- Read the top screen with `GamepadController.I.screenStack.Peek()` (`AlmostStack<GamepadScreen>`).
  `GetScreensOnStack()` does not return the stack in top-last order.
- `EventSystem.current.currentSelectedGameObject` is set in the main menu even in mouse/keyboard mode
  (the default is "Spielen"). Popups such as `NewsCenter_Screen` do **not** take the selection in
  this mode, so the selection stays on the screen behind them.
- `I2.Loc.LocalizationManager.CurrentLanguageCode` returns `de` for the German game.

### Keyboard

- The input module is `Rewired.Integration.UnityUI.RewiredStandaloneInputModule`.
- Rewired's keyboard map (player 0) only binds game actions:
  - Tab: ToggleHabitatsOverview
  - Up: ToggleCardPreview
  - Down: ToggleDiscards
  - Right: NextCard
  - Left: PreviousCard
  - Space: Submit
  - Return, Keypad Enter and C: Accept
  - X and Ctrl+Z: Back
  - Esc: Pause
  - I: ShowTooltips
  - BackQuote: ShowGoals
  - O: HideInterface
  - S: HabitatDown
  - D: HabitatUp
- **NavigationX/NavigationY have no keyboard binding**, so menus cannot be navigated by keyboard
  natively.
- Mod-side navigation (`Selectable.FindSelectableOn*` plus `Select()`) works in the main menu and
  is announced. Known gaps:
  - It reaches hidden console-only buttons (`Xbox_SwitchUser`, "Change player").
  - It is not limited to the top screen.
- Unity legacy input (`Input.GetKeyDown`) works in this build. Polling `GetAsyncKeyState` misses
  short taps.

## Phase 1 implementation notes (2026-11-08)

### Input through a virtual gamepad

- `ReInput.controllers.CreateCustomController(CustomController_QAATT.sourceId)` creates the game's QA
  automation controller. After `player.controllers.AddController(controller, true)`, the game already
  has a map with 25 bindings for it, covering every gamepad action plus NavigationX/Y.
- The mod feeds it with `SetButtonValueById` and `SetAxisValueById` (`Input/VirtualGamepad.cs`,
  `Input/KeyboardBridge.cs`). Rewired picks it as the last active controller, so
  `LastControllerIsMouseOrKeyboard()` becomes false and the game runs in gamepad mode.
- The game's keyboard map is disabled (`map.enabled = false`); otherwise every key would switch the
  game back to mouse/keyboard mode.
- Hold each press for at least two frames. Count modifiers that were pressed or released in the same
  frame (`GetKeyDown`/`GetKeyUp`), so fast or synthetic combinations work.
- Carousel popups such as `CustomGameChooseMode_Popup` override `PreviewPlayerLeft/Right` for paging,
  which is Control+Page Up/Down in the mod layout. Their arrow buttons are not part of the navigation graph.

### Screens and focus

- Read the top screen with `GamepadController.I.screenStack.Peek()`.
- `StartMenuGamepadScreen` is the root `StartMenu_Canvas`, which contains every main-menu screen.
  Hidden screens stay *active* and are only hidden through a disabled `Canvas` or a `CanvasGroup` with
  alpha 0. Text collection therefore has to check visibility (`UI/UiText.cs`, `Visibility`).
- After a screen change, the game does not always select an element inside the new top screen. The
  mod waits 15 frames and then restores the last selection it remembered for that screen. Failing
  that, it calls `TryRestoreSelection()` and then `TrySelectDefault()`. This correction is skipped
  for `Decision` screens.
- Labels often sit next to a control rather than inside it: options use `TitleTxt (TMP)` siblings,
  and the resolution uses `ResolutionTxt`. Unlabeled controls use the texts of their siblings. Changes
  to those texts, and to texts inside an open popup, are announced.
- Game tooltips: call `BaseTooltip.GetText().ToString()` on `I2.Loc.LocalizedString` to get localized
  descriptions. The mod reads them with F3.
- Custom game setup: `CustomGamePlayer_Panel` exposes `CustomGamePlayerType`, `ColorName`,
  `PortraitName` and `Username`. The localized type names come from the panel's `ChoosePlayerTypeButton`
  texts. Toggle meanings were verified visually:
  - `GoalMat` on means the podium icon, i.e. the green side with direct competition.
  - `Order` on means the shuffle icon, i.e. random order.

### Il2CppInterop pitfalls

- `Component.GetComponentInChildren<T>(bool)` throws `VerificationException` for abstract `T`, for
  example `TMP_Text`. Use `GetComponentsInChildren<T>()` instead.
- Interface and base-class checks work with `TryCast<T>()`.

## In-game notes (2026-11-09)

### Making decisions usable

- The game only selects defaults in decisions when `GamepadController.HasController()` is true, and
  that is only the case for real joysticks. A Harmony postfix (`Patches/GamepadPatches.cs`) returns
  true while the virtual gamepad is active. After that, decisions focus their first card or slot
  (`Game_Canvas/CardSlotSelectables/GamepadCardSlot`).
- The pattern in decisions: Enter (Submit) marks a card, die or bird, and Space (Accept) confirms.
  Start game = Space in the setup screen.

### Where things are

- `UIGod.I` is the UI singleton:
  - `GetCurrentHabitat()` returns the habitat in view; Page Up/Down move it.
  - `CanPlayInCurrentHabitat(BirdCardView)` tells whether a hand card is playable there.
  - It also has `GoTo(ActionType)` and the `Forest`, `Grassland` and `Wetland` `HabitatUI` objects.
- Every `Decision` holds `gameStateDriver`. Its `GameState` is the rules model `Rules.GameState`:
  - `Players` (`Rules.Player`), `BirdFeederState` (`Rules.Food`), `BirdsDeckState.Visible` (the tray),
    `CurrentPlayerID`, `RoundsCounter`.
  - Per player: `GetAllBirdsInHabitat(ActionType)`, `BirdCards`, `BonusCards`, `_pf` (food supply),
    `TokensLeft`.
  - Per bird (`Rules.Bird`): `Eggs`, `CachedFood`, `TuckedCardsCount`.
- Habitat ↔ `ActionType` mapping: GainFood = forest, LayEggs = grassland, DrawBirds = wetland.
- Bird names: `I2 GetTranslation(TextLocalizationHelper.GetBirdCardNameTerm(id))`.
- Card views:
  - Locations: `MainCamera/CardPreview_preview/StartingBirdCardsChoice/SlotN/...` (starting
    choice), `MainCamera/LocalGameStateView/PlayerHand/...` (hand) and
    `HabitatsObjects/CardTray/TrayCardSlotN/...` (tray).
  - Texts: `Name_TMP`, `Food_TMP`, `Wingspan_TMP`, `VP_TMP`, `BirdPowerHeader_TMP` and
    `Description_TMP`.
  - `BaseBirdCardView.ID` is the bird id for `BirdDatabase.Get`; `BirdData` values are ACTk
    `Obscured*` types (`.GetDecrypted()`).
  - `BaseBirdCardView.GameStateBird` is the bird on a board.
  - The face-down deck is a `DeckSlot` (bird view with id 0).
- Instruction texts:
  - Inside the decision object, e.g. `DrawBirds_Decision/CardsTxt`.
  - Or as 3D texts, e.g. `StartingBirdCardsChoice/DescriptionCardsFoodTxt`.
  - 3D texts (`TextMeshPro`) are "shown" when their `MeshRenderer.isVisible` is true.
- Playing a bird (`PlayBird_Decision`):
  - It runs through states (`CurrentState`); `HasShownUIToPlayer` separates real steps from
    pass-through ones.
  - Cost and payment: `_currentData.BirdPayment` (`Food`, `EggsCost`, `EnoughFood`).
  - The payment buttons are `ChangePanel/Buttons/<Food>/ButtonInPanel_*`. The child names can be
    wrong (`Nectar/ButtonInPanel_Seed`), so the parent name is used.
- Food from the feeder: `ChooseDice/DiceN/ButtonInPanel_<Food>`. Chosen dice appear under
  `DicesChosenButtons`.
- Eggs: `BaseLayEggs_Decision` has `numberOfEggs` and `laidEggs`. The eggs are only written to the
  rules model after confirming.
- The new turn popup is reused every turn. Its own texts are `RoundText`, `TurnText` and `TapText`;
  the goal track is under `Turn/RoundGoalN`.
- Round goals: `Game_Canvas/HabitatsNotchPadding/GoalsMiniBoard_HUD/TokenSlotN`. Their tooltips
  (`GoalDescriptionTooltip`) hold the full description.

### Opponent turns

- The AI acts within a few frames of its turn starting. Snapshots of every non-acting player are
  therefore refreshed continuously.
- A finished turn is compared about 1.5 s later (`Game/OpponentWatcher.cs`), because the board
  updates after the turn passes on.
- The game's own log also shows moves, e.g. `[TurnProcessing] Played bird - 250 in habitat LayEggs`.

### Round 2 to game end (2026-11-09, morning)

- Decisions that drop the selection (e.g. after rolling dice for a power) can stop taking gamepad
  input. The focus guard now also runs when a decision's selection becomes empty and calls
  `SetGamepadInteractive(true)` again. Before that, Space did nothing after the roll.
- `GamepadScreen.GamepadAccept` is patched: decisions that do not declare their own `GamepadAccept`
  press the shared Next button (`GAME/Game_Canvas/NEXT_NotchPadding/NEXT`, `MCButton.InvokeClick*`)
  when it is enabled. `Game.NextButton.IsAvailable()` also tells the payment announcer whether a
  payment can be confirmed.
- Drawing from the deck takes two confirmations: the first moves the shown card into the hand
  (`DrawBirds_Decision._isDrawingCardFromDeck`), the second confirms. `_cardsDrawnFromDeck` holds the
  drawn bird ids; marked tray cards count in `CardsTakenTotalCount`.
- Birdfeeder dice: `DiceUI_Image.FoodType` names double faces ("nectar or fruit");
  `BaseGainFoodFromBirdFeeder_Decision.foodChosen.List[i].Token` is the food actually taken and
  `amountToChoose` the food still allowed (it drops after a reroll of an empty feeder).
- Playing a bird: `Rulebook.CanPlay(data, player, null, new Nullable<ActionType>(), 0)` gives the
  habitats where the rules allow the bird now (food, eggs, space). The cost panel shows the payment
  in `FoodAboutToPay.FoodButtons` (active buttons) of `ChoosePayment_Panel` (proposal) or
  `ChangePayment_Panel` (manual choice); `BirdPayment.Food` is only filled after confirming. The egg
  cost comes from `EggsPaymentStateController.GetEggsCost(_playerController, _currentData)`.
- The hand: `PlayerHandUI.IsInCardPreview` on `GAME/MainCamera/LocalGameStateView/PlayerHand` says
  whether it is open. The selection stays on a card after closing, so Enter would still play it
  while the hand is open only.
- Round goals: `GameState.GoalBoardState.GetGoalForRound(index)` (0-based) with `GoalName`,
  `GetGoalCount(playerId)` and `Players` (`GoalPlayer.P_Score`, provisional for the running round).
  `GoalTileData.GetLocalizedName()` returns placeholders ("KEY Short/24 ...") for many tiles, so the
  mod has its own short names (`goal.*`); `GetLocalizedDescription()` is the tooltip text.
- Round end: `BaseResolveSpecialPowers_Decision._birdsToResolve` / `_notActivatedBirds`;
  `Player.GetBirdHabitatType(birdId)`.
- Goal board (`GoalAndNectarMat_Screen`): standings per round from the rules model; the points row
  belongs to `_playerController.Player`, which is the opponent during their round end turn. Tabs
  switch with the PreviewPlayerLeft/Right buttons (mapped to Left/Right there).
- Game end (`GameFinished_Screen`): per player `GameFinishedPlayerSliders*` with
  `GameFinishedSliderScore_<Category>/Background` texts; the total counts up during an animation,
  so the mod adds up the categories.
- Saved games (`MatchUILocal`): `GameSave` has round, turns left, mode and portraits; the date text
  is a hidden placeholder, the match id ends in the creation time as a Windows file time.
- Message history: every spoken message except focus announcements and information asked for with
  a key (`ScreenReader.SayTransient`) is kept (200 entries).

### Zones (2026-11-09)

The keyboard layout was consolidated after the example of Accessible Arena (MTG Arena): the number
keys open zones (`UI/Zones`), inside a zone Left/Right/Home/End move, Up/Down read detail lines and
Escape leaves. Enter, Space and Tab stay with the game. The key table is at the top of
`ZoneNavigator`, so zones can move to letter keys later.

- Habitat zones read the rules model (`Player.GetAllBirdsInHabitat`) and move the view with
  `UIGod.GoTo(habitat)`. Birds sync to the game's `GamepadCardSlot` (`_birdSlot.Card.GameStateBird`)
  during tasks other than choosing an action, so Enter acts on the bird under the cursor.
- The tray syncs to `GamepadCardSlot._slot.Card` (bird id) and the deck to the `DeckSlot`; birdfeeder
  dice to `DiceUI_Image.Button` while taking food.
- The hand zone presses ToggleCardPreview to open the hand, steps `PlayerHandUI.NextCard()` /
  `PreviousCard()` to the card under the cursor and presses Back to close it again.
- While a zone is open, `KeyboardBridge` does not translate the arrows and Escape (and Enter in the
  players zone), and `FocusAnnouncer` stays silent about selections.
- Page Up/Down moved from the habitats to the message history.
- The hand zone only opens the hand while choosing an action or in tasks about hand cards (type
  name contains "Hand"); elsewhere it reads the hand without opening it.
- Decisions shown right after loading a saved game ignored the first Enter although something was
  selected. The focus guard now calls `SetGamepadInteractive(true)` for every new decision (except
  waiting for other players), not only when nothing is selected.
- Backspace (`Game.UndoKey`) clicks the last active `FoodAboutToPay.FoodButtons` entry in the manual
  payment panel, or calls `CacheUpToAmountOfFoodFromSupplyOnBird_Decision.RevertLastCachedFood()`.
- `PowerProgressAnnouncer`: food cached so far (`foodToCache` of `amountToCache`) and the bonus card
  drawn (`BaseDrawBonusCardFromDeck_Decision.bonusCardDraw` / `bonusCard`). Not seen in a game yet.

### Tutorial (2026-11-09)

- `Tutorial.TutorialDriver._currentTask` is the running `Tutorial.Task`; `Hints` and `_hintIndex`
  tell whether all its hints were shown. Hint bubbles are `Tutorial.HintAnnouncer` screens (Small,
  SmallLeft, ... chosen by `HintAnnouncerChooser`); `BaseHint.GetText()` has the full text while the
  bubble's `SpeechBubble.HintText` types it letter by letter. A new bubble first plays its show
  animation and only then starts revealing, so `RevealNow()` is called whenever `IsRevealing`.
- `Game.TutorialAnnouncer` reads each hint once (with the bird or bonus card the bubble shows) and,
  after the last hint of a task that waits for the player, the keys for it. Task types handled:
  GoToHabitat, ChooseAction, ChooseFood (and the two-faced die, several dice), ChooseBirdCard(s)
  (tray, hand, board via `ClickableBirds`), ChooseBirdInHabitat, ClickDiscardButton (X),
  RerollBirdfeeder, CacheOrTakeSeed, ClickPlayerPortrait, Confirm, popups, goals, overview, back.
- Tutorial bubbles lie over the decision they explain, so the decision announcers get the decision
  under a bubble (`Screens.Find<Decision>()`), selections under a bubble are read once it closes,
  and zones work while a bubble is shown.
- Clicking a player's portrait: the gamepad's PreviewPlayerRight button shows the next player's board
  (`PlayerPreview_Screen`) and satisfies `ClickPlayerPortraitTask`; zone 9 presses it while the task
  runs. The top-bar portraits are no `PlayerButton`/`PlayerPortraitUI` the mod could click.
- The forest trade (discard a card for a die) is the gamepad's Context button (X key); the birdfeeder
  reroll is `Buttons/ReloadBirdfeederButton`, now an item of zone 4.
- Starting a game or chapter passes through the play menu and player setup; `GameStartPatch`
  (prefix on `GameLoader.InvokeStartGame`) silences them until the board or a bubble shows.
  `GameLoader.IsStartingGame` is not usable for this (true while the tutorial menu loads).
- The virtual controller (startup log "[Gamepad] virtual controller ... bindings") maps
  HideInterface to LeftStickButton and ShowTooltips to RightStickButton; the mod binds them to H
  and I. All trades (forest card for a die, grassland food for an egg, wetland egg for a card) are
  the Context button (X).
- All 14 chapters of the base game were played through with the keyboard (2026-11-09). Handled
  task types now also include ClickHideInterface, DiscardEggToDrawCard, ChooseBonusCard,
  tooltips, overview, goals; the food and card hints depend on the decision under the bubble
  (birdfeeder or supply; tray, hand or board). Tooltip tasks did not come up in the base game.
- The tray zone shows only cards that still have a view in the tray: cards taken this turn stay in
  `BirdsDeckState.Visible` until the turn ends.
- The payment proposal can be plain pictures in `CostPaymentState/Background/FoodPanel`; the food
  sprite names are `food_seed`, `food_fruit`, ... and `food_inverebrate` (sic). Eggs picked for a
  bird's place are counted in `EggsPaymentStateController.ChooseEggsPanel.payment`.
- Untested: the expansion tutorials (Europe, Oceania, Asia, Duet).
- With the computer-use test harness the first key of a batch is sometimes lost (the window is
  re-activated); a Shift tap first avoids it. Not seen with a real keyboard so far.

### Start screen and news

- The game is in mouse mode until the first key press, so the focus guard did not move the
  selection into the news window that opens at start; arrows then moved through the main menu
  behind it. Menus and popups now get their selection in either mode (decisions still only in
  gamepad mode).
- `NewsCenter_Screen`: entries are `NewsDataButton` (`_titleText`, `_dateText`,
  `_unreadNewsIndicator`, `_pinnedNewsIndicator`); Enter shows the news in `_newsDisplayPanel`
  (`_headerText`, `_contentText`), which `NewsDescriber` reads when it changes.

### Known gaps

- Most bird powers with their own decisions are still untested; seen so far: copy a white power
  (requirement note), roll dice and cache, discard food to tuck cards, tuck cards from the deck,
  gain food from the supply (yes/no), skip power.
- Untested: Automa games, Duet mode, the colour, portrait and background popups, the online menus,
  bonus card scoring details at game end, tie breaks.
- Sometimes the first Enter in a decision is ignored right after the camera moved (e.g. drawing
  right after Page Down). Pressing again works.

### Design decision

- Input goes through the virtual QAATT gamepad, and the game's keyboard map is disabled; see the
  Phase 1 notes above. The rejected alternative was rebinding Rewired's keyboard map, which would
  leave the game in mouse/keyboard mode.
- The key layout is in `Input/KeyboardBridge.cs` and in the README. Gameplay keys (habitats, cards,
  players) are provisional until Phase 2.

### Bird facts and sounds (2026-11-09)

- `BirdData` has `LatinName` and the continent flags (`NorthAmerica`, `SouthAmerica`, `Europe`,
  `Africa`, `Asia`, `Australia`, encrypted bools). The trivia text is a localization term
  `BirdCards/Fluffs/<id>` next to the name term `BirdCards/Names/<id>`
  (`TextLocalizationHelper.GetBirdCardNameTerm`); `BirdCardView.BirdTrivia` is its `Localize`.
  The game calls the trivia "fluffs" (GamepadController.ToggleFluffs shows them on the cards).
- `BirdAudioManager.I.PlayMain(BirdName)` plays a bird's call and returns a length (2 s in all tests).
  `BirdCardView.PlayVoiceOver()` plays the spoken trivia (it loads the voice-overs on demand);
  `BirdAudioManager.PlayVoiceOver(id, forcePlay, isRetry)` is the fallback without a card view.
  Whether the sounds are audible was not checked by the mod's tests.
- The bird atlas in the main menu (`AtlasGamepadScreen`, `BirdAtlasManager`) has song and
  voice-over buttons (`BirdSongButton`, `BirdVOButton`); it is not made accessible yet.
- With all expansions (521 bird cards) a game against the AI started and played: start selection,
  nectar in the supply and payments, opponent summaries with nectar.
