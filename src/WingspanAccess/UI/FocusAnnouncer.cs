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
    private static bool? _lastGamepadMode;
    private static int _guardFrame = -1;

    private static UnityEngine.UI.Toggle _toggle;
    private static UnityEngine.UI.Slider _slider;
    private static TMPro.TMP_Dropdown _dropdown;
    private static string _lastValue;

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

        int screenId = top == null ? 0 : top.GetInstanceID();
        bool screenChanged = screenId != _lastScreenId;
        if (screenChanged)
        {
            _lastScreenId = screenId;
            _popupId = 0; // the game reuses popups (e.g. the turn popup); compare texts per appearance
            Plugin.Logger.LogInfo($"[Screen] top={top?.GetIl2CppType().Name ?? "<none>"} stack=[{Screens.DescribeStack()}]");
            if (top != null)
                ScreenReader.Say(DescribeScreen(top), history: IsGameEvent(top));
        }

        if (screenChanged || modeChanged)
            _guardFrame = Time.frameCount + SelectionGraceFrames;
        if (_guardFrame >= 0 && Time.frameCount >= _guardFrame)
        {
            _guardFrame = -1;
            if (gamepadMode && top != null)
                KeepSelectionInMenu(top);
        }

        TrackSelection(top, interrupt: !screenChanged);
        WatchValue();
        WatchPopupTexts(top);
        DecisionAnnouncer.Tick(top);
        GoalMatDescriber.Tick(top);
        Game.GameStateCache.Update(top);
        Game.ActionAnnouncer.Tick(top);
        Game.OpponentWatcher.Tick();
        Game.PlayBirdAnnouncer.Tick(top);
        Game.ChosenDiceAnnouncer.Tick(top);
        Game.EggDecisionAnnouncer.Tick(top);
        Game.DrawAnnouncer.Tick(top);
        Game.SpecialPowersAnnouncer.Tick(top);
        Game.DiscardAnnouncer.Tick(top);
    }

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

        string hint = "hint." + typeName;
        if (Localization.Loc.Has(hint))
            parts.Add(Localization.Loc.Get(hint));
        return UiText.JoinSentences(parts);
    }

    /// <summary>Screens whose announcement should only contain these texts (by object name, in this order).</summary>
    private static readonly Dictionary<string, string[]> SelectedTexts = new()
    {
        // The round goal track in this popup is read with the goals key instead.
        ["NewTurnAnnouncement_Popup"] = new[] { "RoundText", "TurnText", "TapText" },
        // Another player's power gives you an extra action: which bird and what it does.
        ["IncomingInterjectionTurnAnnouncement_Popup"] = new[] { "CentralBirdPowerText", "TapText" },
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
            if (selected == null || !selected.activeInHierarchy)
            {
                Plugin.Logger.LogInfo($"[Focus] nothing selected in {top.GetIl2CppType().Name}, enabling gamepad interaction and selecting its default");
                top.SetGamepadInteractive(true);
                top.TrySelectDefault();
                GameObject now = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
                Plugin.Logger.LogInfo($"[Focus] after default selection: {(now == null ? "<none>" : UiText.PathOf(now))}");
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

    private static void TrackSelection(GamepadScreen top, bool interrupt)
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

        string description = UiText.Describe(selected);
        Transform parent = selected.transform.parent;
        int groupId = parent == null ? 0 : parent.GetInstanceID();
        if (groupId != _lastGroupId)
        {
            _lastGroupId = groupId;
            string group = UiText.GroupLabel(selected);
            if (group != null && !description.StartsWith(group))
                description = $"{group} {description}";
        }

        Plugin.Logger.LogInfo($"[Focus] {UiText.PathOf(selected)} => {description}");
        ScreenReader.SayTransient(description, interrupt);
    }

    private static void WatchValue()
    {
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
        if (changed.Count > 0 && _slider == null)
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
