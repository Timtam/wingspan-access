using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using WingspanAccess.Speech;

namespace WingspanAccess.UI;

/// <summary>
/// Announces screen changes (with popup texts) and the selected element, watches the value of
/// the selected toggle/slider/dropdown, and keeps the selection inside the top menu screen.
/// </summary>
internal static class FocusAnnouncer
{
    /// <summary>Frames to wait after a screen change before correcting the selection, so the game selects first.</summary>
    private const int SelectionGraceFrames = 15;

    private static int _lastScreenId;
    private static int _lastSelectionId = -1;
    private static int _lastGroupId;
    /// <summary>The last screen announcement as plain words (see <see cref="Words"/>).</summary>
    private static string _screenWords = string.Empty;
    private static bool? _lastGamepadMode;
    private static int _guardFrame = -1;
    private static int _screenChangedFrame = -1000;
    private const float StartTimeoutSeconds = 20f;
    private static float _startedAt = -1f;
    private static int _focusSpokenFrame = -1000;
    private static bool _selectionHiddenByHint;

    private static UnityEngine.UI.Toggle _toggle;
    private static UnityEngine.UI.Slider _slider;
    private static TMPro.TMP_Dropdown _dropdown;
    private static TMPro.TMP_InputField _input;
    private static string _lastInputText;
    private static string _lastValue;

    /// <summary>Frame at which Enter on an option that is already chosen gets its state repeated (0: none).</summary>
    private static int _repeatChosenFrame;

    /// <summary>Texts of and next to the selection; changes are announced (e.g. "1920 x 1080" after an arrow button).</summary>
    private static GameObject _selected;
    private static List<string> _watchedTexts = new();
    private static int _nextTextCheck;
    private static string _lastBoardState;
    private static int _lastCardId;

    public static void Tick()
    {
        GamepadController controller = GamepadController.I;
        if (controller == null)
            return;

        GamepadScreen top = Screens.Top();
        bool gamepadMode = !controller.LastControllerIsMouseOrKeyboard();
        bool modeChanged = gamepadMode != _lastGamepadMode;
        if (modeChanged)
            Plugin.Logger.LogInfo($"[Focus] gamepad mode {gamepadMode}");
        _lastGamepadMode = gamepadMode;

        // While a game or tutorial chapter starts, the game passes through menus (the play menu, the
        // player setup) that are not meant for the player; they are followed silently until the
        // board or the first tutorial bubble shows.
        bool starting = _startedAt >= 0f;
        if (starting && (InGame(top) || Time.realtimeSinceStartup - _startedAt > StartTimeoutSeconds))
        {
            _startedAt = -1f;
            starting = false;
            Plugin.Logger.LogInfo("[Focus] game started");
        }

        int screenId = top == null ? 0 : top.GetInstanceID();
        bool screenChanged = screenId != _lastScreenId;
        if (screenChanged)
        {
            _lastScreenId = screenId;
            _screenChangedFrame = Time.frameCount;
            if (_selectionHiddenByHint)
            {
                _selectionHiddenByHint = false;
                _lastSelectionId = -1; // read the selection the bubble covered
            }
            _popupId = 0; // the game reuses popups (e.g. the turn popup); compare texts per appearance
            Plugin.Logger.LogInfo($"[Screen] top={top?.GetIl2CppType().Name ?? "<none>"} stack=[{Screens.DescribeStack()}]");
            if (top != null && !starting)
            {
                // Tutorial hints and their key hints are queued; a screen opening right after them
                // (the decision under a closing bubble) must not cut them off.
                bool afterTutorial = Time.realtimeSinceStartup - Game.TutorialAnnouncer.LastSpokeAt < 2f;
                string screenText = DescribeScreen(top);
                _screenWords = Words(screenText);
                ScreenReader.Say(screenText, interrupt: !afterTutorial, history: IsGameEvent(top));
                // A selection inside the new screen, made just before it registered, was cut off by its
                // name: read it again.
                GameObject current = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
                if (Time.frameCount - _focusSpokenFrame < 30 && current != null && current.transform.IsChildOf(top.transform))
                    _lastSelectionId = -1;
            }
        }

        if (screenChanged || modeChanged)
            _guardFrame = Time.frameCount + SelectionGraceFrames;
        if (_guardFrame >= 0 && Time.frameCount >= _guardFrame)
        {
            _guardFrame = -1;
            // Until the first key press the game counts as mouse-controlled. Menus opening then (the
            // news at start) would leave the selection behind them, in the main menu; so menus and
            // popups get their selection in either mode, decisions only once the keyboard is used.
            if (top != null && !starting && (gamepadMode || top.TryCast<Decision>() == null))
                KeepSelectionInMenu(top);
        }

        // The selection the game or the guard sets right after a screen change follows the screen's
        // announcement instead of cutting it off.
        bool settling = Time.frameCount <= _screenChangedFrame + SelectionGraceFrames + 1;
        TrackSelection(top, interrupt: !settling, silent: starting);
        WatchValue();
        WatchPopupTexts(top);
        DecisionAnnouncer.Tick(top);
        GoalMatDescriber.Tick(top);
        NewsDescriber.Tick(top);
        Game.GameStateCache.Update(top);
        Game.ActionAnnouncer.Tick(top);
        Game.OpponentWatcher.Tick();
        Game.OnlineTimerWatcher.Tick();

        // Tutorial hint bubbles lie over the decision they explain (the payment proposal, the picked
        // food); the decision's own announcements still apply.
        GamepadScreen decision = top != null && top.TryCast<Tutorial.HintAnnouncerBase>() != null
            ? Screens.Find<Decision>() ?? top
            : top;
        Game.PlayBirdAnnouncer.Tick(decision);
        Game.ChosenDiceAnnouncer.Tick(decision);
        Game.EggDecisionAnnouncer.Tick(decision);
        Game.DrawAnnouncer.Tick(decision);
        Game.SpecialPowersAnnouncer.Tick(decision);
        Game.DiscardAnnouncer.Tick(decision);
        Game.PowerProgressAnnouncer.Tick(decision);
        Game.TutorialAnnouncer.Tick(top);
    }

    /// <summary>Called when the game starts a game or tutorial chapter (see GameStartPatch).</summary>
    public static void GameStarting()
    {
        _startedAt = Time.realtimeSinceStartup;
        Plugin.Logger.LogInfo("[Focus] starting a game");
    }

    private static bool InGame(GamepadScreen top) =>
        top != null && (top.TryCast<Tutorial.HintAnnouncerBase>() != null || UiText.PathOf(top.gameObject).StartsWith("GAME/Game_Canvas"));

    private static readonly HashSet<string> NotInHistory = new() { "WaitForOtherPlayer_Decision", "PauseMenu_Screen", "OptionsMenuPause" };

    /// <summary>In-game screens (turns, decisions, results) go into the message history; menus do not.</summary>
    private static bool IsGameEvent(GamepadScreen screen) =>
        !NotInHistory.Contains(screen.GetIl2CppType().Name) && !UiText.PathOf(screen.gameObject).StartsWith("GAME/StartMenu_Canvas");

    /// <summary>Screen name; for popups also the texts that are not part of a button; then an optional key hint.</summary>
    public static string DescribeScreen(GamepadScreen screen)
    {
        string typeName = screen.GetIl2CppType().Name;
        string name = UiText.ScreenName(screen);
        var parts = new List<string> { name };
        string special = GoalMatDescriber.Describe(screen) ?? GameEndDescriber.Describe(screen);
        if (special != null)
        {
            parts.Add(special);
        }
        else if (SelectedTexts.TryGetValue(typeName, out string[] objectNames))
        {
            parts.AddRange(Screens.NamedTexts(screen.gameObject, objectNames));
        }
        // Message-like popups (few controls) are read completely; larger panels only by name.
        else if (screen.IsPopup() && Screens.CountControls(screen.gameObject) <= 3)
        {
            List<string> texts = Screens.PlainTexts(screen.gameObject);
            texts.Remove(name);
            parts.AddRange(texts);
        }

        // Key hints may belong to a base type, e.g. all decisions that discard cards from the hand.
        for (Il2CppSystem.Type type = screen.GetIl2CppType(); type != null; type = type.BaseType)
        {
            string hint = "hint." + type.Name;
            if (Localization.Loc.Has(hint))
            {
                parts.Add(Localization.Loc.Get(hint));
                break;
            }
        }

        return UiText.JoinSentences(parts);
    }

    /// <summary>Screens whose announcement should only contain these texts (by object name, in this order).</summary>
    private static readonly Dictionary<string, string[]> SelectedTexts = new()
    {
        // The round goal track in this popup is read with the goals key instead.
        ["NewTurnAnnouncement_Popup"] = new[] { "RoundText", "TurnText", "TapText" },
        // Another player's power gives you an extra action: which bird and what it does.
        ["IncomingInterjectionTurnAnnouncement_Popup"] = new[] { "CentralBirdPowerText", "TapText" },
        // Tutorial bubbles: their text appears letter by letter; the tutorial announcer reads it in full.
        ["HintAnnouncer"] = new string[0],
        ["HintAnnouncerChooser"] = new string[0],
    };

    private static readonly HashSet<string> SilentFocusScreens = new() { "NewTurnAnnouncement_Popup", "IncomingInterjectionTurnAnnouncement_Popup" };

    /// <summary>Last selection seen inside each screen, restored when the screen becomes the top again.</summary>
    private static readonly Dictionary<int, GameObject> LastSelectionPerScreen = new();

    /// <summary>
    /// Menus and popups own the selection. Decisions may select board elements elsewhere, so there
    /// the mod only steps in when nothing at all is selected. The game does not pick a default
    /// selection by itself while the virtual controller is used.
    /// </summary>
    private static void KeepSelectionInMenu(GamepadScreen top)
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;

        if (top.TryCast<Decision>() != null)
        {
            // Decisions shown after loading a game, or while the camera moves, can ignore the first
            // gamepad input although something is selected; switching gamepad input on again fixes it.
            if (top.TryCast<WaitForOtherPlayer_Decision>() == null)
                top.SetGamepadInteractive(true);

            if (selected == null || !selected.activeInHierarchy)
            {
                Plugin.Logger.LogInfo($"[Focus] nothing selected in {top.GetIl2CppType().Name}, enabling gamepad interaction and selecting its default");
                top.SetGamepadInteractive(true);
                top.TrySelectDefault();
                GameObject now = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
                Plugin.Logger.LogInfo($"[Focus] after default selection: {(now == null ? "<none>" : UiText.PathOf(now))}");
            }
            else
            {
                Plugin.Logger.LogInfo($"[Focus] {top.GetIl2CppType().Name}: gamepad input on, keeping {UiText.PathOf(selected)}");
            }

            return;
        }
        if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(top.transform))
            return;

        if (LastSelectionPerScreen.TryGetValue(top.GetInstanceID(), out GameObject remembered) &&
            remembered != null && remembered.activeInHierarchy &&
            new Visibility(top.transform).IsVisible(remembered.transform))
        {
            UnityEngine.UI.Selectable selectable = remembered.GetComponent<UnityEngine.UI.Selectable>();
            if (selectable != null && selectable.interactable)
            {
                Plugin.Logger.LogInfo($"[Focus] selection outside {top.GetIl2CppType().Name}, back to {remembered.name}");
                selectable.Select();
                return;
            }
        }

        bool restored = top.TryRestoreSelection();
        Plugin.Logger.LogInfo($"[Focus] selection outside {top.GetIl2CppType().Name}, {(restored ? "restored previous selection" : "selecting its default")}");
        if (!restored)
            top.TrySelectDefault();
    }

    private static int _popupId;
    private static List<string> _popupTexts = new();
    private static int _nextPopupCheck;

    /// <summary>Announces texts that appear or change in an open popup (e.g. a carousel showing the next game mode).</summary>
    private static void WatchPopupTexts(GamepadScreen top)
    {
        if (top == null || !top.IsPopup() || Time.frameCount < _nextPopupCheck)
            return;

        _nextPopupCheck = Time.frameCount + 10;
        List<string> now = Screens.PlainTexts(top.gameObject);
        if (top.GetInstanceID() != _popupId)
        {
            _popupId = top.GetInstanceID();
            _popupTexts = now;
            return;
        }

        var changed = now.FindAll(text => !_popupTexts.Contains(text));
        _popupTexts = now;
        if (changed.Count > 0)
            ScreenReader.SayTransient(UiText.JoinSentences(changed));
    }

    private static void TrackSelection(GamepadScreen top, bool interrupt, bool silent)
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        int id = selected == null ? 0 : selected.GetInstanceID();
        if (id == _lastSelectionId)
            return;

        if (selected != null && top != null && selected.transform.IsChildOf(top.transform))
            LastSelectionPerScreen[top.GetInstanceID()] = selected;

        _lastSelectionId = id;
        _toggle = null;
        _slider = null;
        _dropdown = null;
        _lastValue = null;
        _repeatChosenFrame = 0;
        _selected = selected;
        _watchedTexts = new List<string>();
        _lastBoardState = null;
        if (selected == null)
        {
            Plugin.Logger.LogInfo("[Focus] <none>");
            // A decision that drops the selection (e.g. after rolling dice for a power) may also stop
            // taking gamepad input; the guard turns it back on.
            if (top != null && top.TryCast<Decision>() != null && top.TryCast<WaitForOtherPlayer_Decision>() == null && _guardFrame < 0)
                _guardFrame = Time.frameCount + SelectionGraceFrames;
            return;
        }

        _toggle = selected.GetComponent<UnityEngine.UI.Toggle>();
        _slider = selected.GetComponent<UnityEngine.UI.Slider>();
        _dropdown = selected.GetComponent<TMPro.TMP_Dropdown>();
        _input = selected.GetComponent<TMPro.TMP_InputField>();
        _lastInputText = _input?.text?.Replace("\u200B", string.Empty);
        _lastValue = CurrentValue();
        _watchedTexts = WatchedTexts(selected);
        CardView selectedCard = CardDescriber.FindCardView(selected);
        _lastCardId = selectedCard == null ? 0 : selectedCard.GetInstanceID();

        // Full-screen "click to continue" buttons repeat what the screen announcement just said.
        if (top != null && SilentFocusScreens.Contains(top.GetIl2CppType().Name))
            return;

        // In a zone the zone navigator announces its items; the game's selection only follows it.
        if (Zones.ZoneNavigator.IsActive)
        {
            Plugin.Logger.LogInfo($"[Focus] (zone) {UiText.PathOf(selected)}");
            return;
        }

        if (silent)
        {
            Plugin.Logger.LogInfo($"[Focus] (starting game) {UiText.PathOf(selected)}");
            return;
        }

        // Under a tutorial bubble the game already selects the decision's first element. The bubble
        // is read now; the selection follows once the bubble closes.
        if (top != null && top.TryCast<Tutorial.HintAnnouncerBase>() != null && !selected.transform.IsChildOf(top.transform))
        {
            Plugin.Logger.LogInfo($"[Focus] (under hint) {UiText.PathOf(selected)}");
            _selectionHiddenByHint = true;
            return;
        }

        string description = UiText.Describe(selected);
        Transform parent = selected.transform.parent;
        int groupId = parent == null ? 0 : parent.GetInstanceID();
        if (groupId != _lastGroupId)
        {
            _lastGroupId = groupId;
            string group = UiText.GroupLabel(selected, top == null ? null : top.transform);
            // A header the screen announcement began with (the title of the chapter summary) is
            // not repeated before the button.
            if (group != null && !description.StartsWith(group) && !_screenWords.StartsWith(Words(group)))
                description = group.EndsWith(":") ? $"{group} {description}" : $"{group}: {description}";
        }

        Plugin.Logger.LogInfo($"[Focus] {UiText.PathOf(selected)} => {description}");
        ScreenReader.SayTransient(description, interrupt);
        _focusSpokenFrame = Time.frameCount;
    }

    private static void WatchValue()
    {
        WatchInput();

        // Enter on an option that is already chosen (the preselected "Ja" of a yes/no question)
        // changes nothing in the game; its state is repeated so the key does not go unanswered.
        if (_toggle != null && _toggle.isOn && !Zones.ZoneNavigator.IsActive &&
            (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter)))
            _repeatChosenFrame = Time.frameCount + 10;
        if (_repeatChosenFrame > 0 && Time.frameCount >= _repeatChosenFrame)
        {
            _repeatChosenFrame = 0;
            if (_toggle != null && _toggle.isOn && CurrentValue() == _lastValue)
                ScreenReader.SayTransient(_lastValue);
        }

        if (_toggle != null || _slider != null || _dropdown != null)
        {
            string value = CurrentValue();
            if (value != _lastValue)
            {
                _lastValue = value;
                ScreenReader.SayTransient(value);
                return;
            }
        }

        if (_selected == null || Time.frameCount < _nextTextCheck)
            return;

        _nextTextCheck = Time.frameCount + 6;

        // Board slots stay selected while the view moves to another habitat; a new card under the
        // selection is announced like a new selection.
        CardView card = CardDescriber.FindCardView(_selected);
        int cardId = card == null ? 0 : card.GetInstanceID();
        if (cardId != _lastCardId)
        {
            _lastCardId = cardId;
            if (card != null)
            {
                _lastBoardState = null;
                _watchedTexts = WatchedTexts(_selected);
                if (!Zones.ZoneNavigator.IsActive)
                    ScreenReader.SayTransient(UiText.Describe(_selected));
            }
        }

        // Birds on the board: announce eggs, cached food and tucked cards when they change.
        BaseBirdCardView bird = card == null ? null : card.TryCast<BaseBirdCardView>();
        string boardState = bird == null ? null : CardDescriber.BoardState(bird);
        if (boardState != _lastBoardState)
        {
            if (_lastBoardState != null && boardState != null)
                ScreenReader.SayTransient(boardState);
            _lastBoardState = boardState;
        }
        List<string> now = WatchedTexts(_selected);
        var changed = now.FindAll(text => !_watchedTexts.Contains(text));
        _watchedTexts = now;
        // Sliders say their value; text fields echo the typing themselves (WatchInput).
        if (changed.Count > 0 && _slider == null && _input == null)
            ScreenReader.SayTransient(string.Join(", ", changed));
    }

    private static List<string> WatchedTexts(GameObject selected)
    {
        List<string> texts = UiText.CollectTexts(selected);
        foreach (string sibling in UiText.SiblingTexts(selected))
        {
            if (!texts.Contains(sibling))
                texts.Add(sibling);
        }

        return texts;
    }

    /// <summary>
    /// Text fields in Unity give the screen reader no echo: this says each typed or deleted
    /// character, and the whole text when it changes in other ways (pasted, cleared, selected away).
    /// </summary>
    private static void WatchInput()
    {
        if (_input == null)
            return;
        // The game adds and removes an invisible zero-width space at the end now and then.
        string text = (_input.text ?? string.Empty).Replace("\u200B", string.Empty);
        string last = _lastInputText ?? string.Empty;
        if (text == last)
            return;
        _lastInputText = text;

        string said;
        if (text.Length == last.Length + 1 && text.StartsWith(last))
            said = Character(text[text.Length - 1]);
        else if (last.Length == text.Length + 1 && last.StartsWith(text))
            said = Localization.Loc.Get("input.deleted", Character(last[last.Length - 1]));
        else
            said = text.Length == 0 ? Localization.Loc.Get("input.empty") : text;
        ScreenReader.SayTransient(said);
    }

    private static string Character(char c) => c == ' ' ? Localization.Loc.Get("input.space") : c.ToString();

    private static readonly System.Text.RegularExpressions.Regex NonWord = new(@"[^\p{L}\p{N}]+");

    /// <summary>Lower-case words without punctuation, to compare texts that were joined differently.</summary>
    private static string Words(string text) =>
        text == null ? string.Empty : NonWord.Replace(text.ToLowerInvariant(), " ").Trim();

    private static string CurrentValue()
    {
        if (_toggle != null)
            return UiText.ToggleState(_toggle);
        if (_slider != null)
            return UiText.SliderValue(_slider);
        if (_dropdown != null && _dropdown.captionText != null)
            return UiText.Clean(_dropdown.captionText.text);
        return null;
    }
}
