using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.UI;

/// <summary>
/// The bird atlas ("Birds" in the main menu) is a wall of cards. The game moves a frame over it with
/// Left and Right (bird by bird, across the rows), without a UI selection; this reads the framed
/// bird. Up and Down read it line by line, as in the zones. Birds not unlocked yet lie face down;
/// like the game, the mod does not name them. Its filters say how many birds they leave.
/// </summary>
internal static class AtlasDescriber
{
    public const string ScreenType = "AtlasGamepadScreen";
    private const string FiltersType = "AtlasFiltersGamepadScreen";

    private static int _index = int.MinValue;
    private static int _bird = int.MinValue;
    private static bool _open;
    private static int _line = -1;
    private static int _filteredCount = -1;

    /// <summary>What the bird at the frame's place was looked up for (the place, the game's bird, the order and filters).</summary>
    private static (int, int, int, SortType, bool, int, bool) _wall;
    private static bool _placedOk;
    private static int _placed;
    private static bool _placedUnlocked;

    /// <summary>The framed bird's id while the atlas is shown and the bird is unlocked (0: none), for its call and trivia (S, V).</summary>
    public static int CurrentBirdId { get; private set; }

    /// <summary>Whether the atlas comes back from its filters (it stayed open under them), not opens anew.</summary>
    public static bool Returning(GamepadScreen screen) =>
        screen.GetIl2CppType().Name == ScreenType && _index != int.MinValue;

    /// <summary>Whether the atlas wall is shown, whose Up and Down read the framed bird instead of moving by rows.</summary>
    public static bool TakesArrows(string topType) => topType == ScreenType;

    public static void Tick(GamepadScreen top)
    {
        string type = top == null ? null : top.GetIl2CppType().Name;
        if (type == FiltersType)
        {
            WatchFilters();
            return;
        }

        _filteredCount = -1;
        AtlasGamepadScreen atlas = type == ScreenType ? top.TryCast<AtlasGamepadScreen>() : null;
        if (atlas == null)
        {
            _index = int.MinValue;
            _bird = int.MinValue;
            _wall = default;
            _placedOk = false;
            CurrentBirdId = 0;
            return;
        }

        ReleaseSelectionBehind(atlas);

        int index = atlas.AtlasBirdIndex;
        int bird = atlas.SelectedBirdID;
        bool unlocked = atlas._isUnlockedBird;
        BirdAtlasManager manager = atlas.BirdAtlasManager;
        bool open = manager != null && manager.CardSelected;

        // While the wall is laid out there is nothing to read yet. The game names the framed bird only
        // once an arrow key moves the frame: at first, and after sorting or filtering (when the frame
        // goes back to the first place), it still holds the bird framed before. So the mod takes the
        // bird at the frame's place from the wall's own order.
        int shown = Count(manager);
        if (shown <= 0)
            return;
        var wall = (index, bird, shown, manager._currentSortType, manager._sortedDescending, manager._activeFilters?.Count ?? 0, manager._unlockedBirdsFilter);
        if (!wall.Equals(_wall))
        {
            _wall = wall;
            _placedOk = BirdAt(manager, index, out _placed, out _placedUnlocked);
            if (_placedOk && _placed != bird && bird > 0)
                Plugin.Logger.LogInfo($"[Atlas] place {index} holds bird {_placed}, the game still names {bird}");
        }

        if (_placedOk)
        {
            bird = _placed;
            unlocked = _placedUnlocked;
        }
        else if (bird <= 0)
        {
            return;
        }
        if (index != _index || bird != _bird || open != _open)
        {
            bool first = _index == int.MinValue;
            bool opened = open && !_open && bird == _bird;
            _index = index;
            _bird = bird;
            _open = open;
            _line = -1;
            CurrentBirdId = unlocked ? bird : 0;
            int count = Count(manager);
            Plugin.Logger.LogInfo($"[Atlas] index {index}, bird {bird}, unlocked {unlocked}, open {open}, count {count}, " +
                $"sorted by {manager?._currentSortType}{(manager != null && manager._sortedDescending ? " descending" : string.Empty)}");

            // The screen's own announcement says the atlas opened; the framed bird follows it.
            string text = opened ? Loc.Get("atlas.opened", Name(bird, unlocked)) : Describe(bird, unlocked, index, count);
            if (text != null)
                ScreenReader.Say(text, interrupt: !first, history: false);
        }

        int step = UnityEngine.Input.GetKeyDown(KeyCode.DownArrow) ? 1 : UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 0;
        if (step != 0 && !Zones.ZoneNavigator.IsActive)
            ReadLine(bird, unlocked, step);
    }

    /// <summary>Up and Down: the framed bird line by line (name, points, habitats ... trivia).</summary>
    private static void ReadLine(int bird, bool unlocked, int step)
    {
        List<string> lines = unlocked && bird > 0
            ? CardDescriber.WithFacts(CardDescriber.BirdLines(bird, null), bird, null)
            : new List<string> { Loc.Get("atlas.locked_details") };
        if (lines.Count == 0)
            return;
        _line = Mathf.Clamp(_line < 0 ? 0 : _line + step, 0, lines.Count - 1);
        ScreenReader.SayTransient(lines[_line]);
    }

    /// <summary>While the filters are open: how many birds they leave, when that changes.</summary>
    private static void WatchFilters()
    {
        int count = Count(Screens.Find<AtlasGamepadScreen>()?.BirdAtlasManager);
        if (count < 0 || count == _filteredCount)
            return;
        bool first = _filteredCount < 0;
        _filteredCount = count;
        Plugin.Logger.LogInfo($"[Atlas] filters leave {count} birds");
        if (!first)
            ScreenReader.Say(Loc.Count("atlas.count", count), interrupt: false, history: false);
    }

    /// <summary>
    /// The bird at this place of the wall, and whether it is unlocked (locked birds have a face-down
    /// card): from the filtered birds while filters are on, else from all birds in their order.
    /// </summary>
    private static bool BirdAt(BirdAtlasManager manager, int index, out int bird, out bool unlocked)
    {
        bird = 0;
        unlocked = false;
        var sorted = manager.allBirdsSort;
        if (index < 0 || sorted == null)
            return false;
        if (Filtering(manager))
        {
            // The wall shows the filtered birds in the order of all birds; the filtered list itself
            // is in another order.
            var filtered = manager.filterdBirds;
            if (filtered == null || index >= filtered.Count)
                return false;
            var kept = new HashSet<int>();
            for (int i = 0; i < filtered.Count; i++)
                kept.Add((int)filtered[i]);
            int place = -1;
            for (int i = 0; i < sorted.Count && bird == 0; i++)
            {
                int id = sorted[i] == null ? 0 : (int)sorted[i].BirdName;
                if (kept.Contains(id) && ++place == index)
                    bird = id;
            }
        }
        else
        {
            if (index >= sorted.Count || sorted[index] == null)
                return false;
            bird = (int)sorted[index].BirdName;
        }

        var faceDown = manager._disabledCardsDict;
        unlocked = faceDown != null && !faceDown.ContainsKey(bird);
        return bird > 0;
    }

    private static bool Filtering(BirdAtlasManager manager) =>
        (manager._activeFilters?.Count ?? 0) > 0 || manager._unlockedBirdsFilter;

    /// <summary>S and V while a bird is opened: the atlas's own buttons, which load the sound first.</summary>
    public static bool TryPlay(bool song)
    {
        GamepadScreen top = Screens.Top();
        AtlasGamepadScreen atlas = top != null && top.GetIl2CppType().Name == ScreenType ? top.TryCast<AtlasGamepadScreen>() : null;
        BirdAtlasManager manager = atlas?.BirdAtlasManager;
        if (manager == null || !manager.CardSelected || manager.BirdInfo == null)
            return false;
        Plugin.Logger.LogInfo($"[Atlas] {(song ? "call" : "trivia")} of the opened bird {_bird}");
        if (song)
            manager.BirdInfo.PlayBirdSong();
        else
            manager.BirdInfo.PlayBirdVO();
        return true;
    }

    /// <summary>The birds on the wall: those the filters leave, else all of them in the current order.</summary>
    private static int Count(BirdAtlasManager manager)
    {
        if (manager == null)
            return -1;
        if (Filtering(manager))
            return manager.filterdBirds?.Count ?? 0;
        int sorted = manager.allBirdsSort?.Count ?? 0;
        return sorted > 0 ? sorted : manager.allBirds?.Count ?? -1;
    }

    private static string Name(int bird, bool unlocked) =>
        !unlocked ? Loc.Get("atlas.locked") : bird > 0 ? Game.GameReader.BirdName(bird) : null;

    private static string Describe(int bird, bool unlocked, int index, int count)
    {
        string position = index >= 0 && count > 0 ? Loc.Get("position", index + 1, count) : null;
        string name = Name(bird, unlocked);
        if (name == null)
            return position;
        return position == null ? name : $"{name}, {position}";
    }

    /// <summary>
    /// The atlas has no controls on its wall, so the selection stays in the main menu behind it, where
    /// the arrow keys would move it and Enter would press it (up to "Quit"). It is let go.
    /// </summary>
    private static void ReleaseSelectionBehind(AtlasGamepadScreen atlas)
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
        if (selected == null)
            return;
        GamepadScreen owner = selected.GetComponentInParent<GamepadScreen>();
        if (owner == null || owner.GetInstanceID() == atlas.GetInstanceID())
            return;
        Plugin.Logger.LogInfo($"[Atlas] letting go of the selection behind the atlas: {UiText.PathOf(selected)}");
        eventSystem.SetSelectedGameObject(null);
    }
}
