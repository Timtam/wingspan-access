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

### Design decision

- Input goes through the virtual QAATT gamepad, and the game's keyboard map is disabled; see the
  Phase 1 notes above. The rejected alternative was rebinding Rewired's keyboard map, which would
  leave the game in mouse/keyboard mode.
- The key layout is in `Input/KeyboardBridge.cs` and in the README. Gameplay keys (habitats, cards,
  players) are provisional until Phase 2.
