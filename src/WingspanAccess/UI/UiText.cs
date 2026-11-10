using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using WingspanAccess.Localization;

namespace WingspanAccess.UI;

/// <summary>Builds spoken descriptions from Unity UI objects.</summary>
internal static class UiText
{
    private static readonly Regex SpriteTag = new(@"<sprite[^>]*?name=""?([^"">\s]+)""?[^>]*>", RegexOptions.IgnoreCase);
    private static readonly Regex AnyTag = new(@"<[^>]+>");
    private static readonly Regex Whitespace = new(@"\s+");
    private static readonly Regex CamelCase = new(@"(?<=[a-z])(?=[A-Z])");
    private static readonly Regex Numeric = new(@"^[\d.,\s%]+$");
    private static readonly Regex GlyphName = new(@"^glyph_(switch|xbox|xboxone|xboxseries|ps4|ps5|ps|steam|keyboard|generic)?_?", RegexOptions.IgnoreCase);

    public static string Describe(GameObject target)
    {
        try
        {
            string special = SetupDescriber.Describe(target) ?? OverviewDescriber.Describe(target) ?? CardDescriber.Describe(target) ??
                SaveDescriber.Describe(target) ?? NewsDescriber.Describe(target);
            if (special != null)
            {
                string cardPosition = Position(target);
                return cardPosition == null ? special : $"{special}, {cardPosition}";
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"Special description failed for {target.name}: {e.Message}");
        }

        UnityEngine.UI.Slider slider = target.GetComponent<UnityEngine.UI.Slider>();
        List<string> texts = CollectTexts(target);
        if (slider != null)
            texts.RemoveAll(text => Numeric.IsMatch(text));

        // A translated name for the object itself wins over whatever texts sit inside it.
        string ownNameKey = "name." + target.name;
        string label;
        if (Loc.Has(ownNameKey))
        {
            label = Loc.Get(ownNameKey);
        }
        else if (texts.Count > 0)
        {
            label = string.Join(", ", texts);
        }
        else
        {
            // Unlabeled controls (arrow buttons, bare toggles) take the texts next to them,
            // e.g. "Auflösung, 1920 x 1080" plus a name for the control itself ("previous").
            // With a known name, only direct neighbours count; texts further up are usually the
            // decision's instruction, which was announced already.
            string name = NameOf(target);
            var parts = name != null ? LabelTexts(SiblingTexts(target)) : ContextTexts(target);
            if (name != null)
                parts.Add(name);
            label = parts.Count > 0 ? string.Join(", ", parts) : HumanizeTypeName(target.name);
        }

        string chapterState = TutorialChapterState(target);
        if (chapterState != null)
            label = $"{label}, {chapterState}";

        string role = RoleOf(target, slider);
        string description = role == null ? label : $"{label}, {role}";
        string position = Position(target);
        return position == null ? description : $"{description}, {position}";
    }

    /// <summary>
    /// The texts next to a control named by its icon that label it: short ones ("Auflösung",
    /// "1920 x 1080"). Sentences and whole panels of texts (a popup's explanation, the online
    /// profile) are the screen's content, which its announcement has read already.
    /// </summary>
    private static List<string> LabelTexts(List<string> siblings)
    {
        siblings.RemoveAll(text => text.Length > 50);
        return siblings.Count > 3 ? new List<string>() : siblings;
    }

    /// <summary>"Completed" for tutorial chapters already played (the list shows them in colour with a feather).</summary>
    private static string TutorialChapterState(GameObject target)
    {
        var chapter = target.GetComponentInChildren<View.UI.MainMenu.TutorialMenu.TutorialMenuButtonDynamic>(true);
        if (chapter == null)
            return null;
        UnityEngine.UI.Image icon = chapter._chapterCompletedIcon;
        Sprite finished = chapter._chapterIconFinished;
        bool done = icon != null && finished != null && icon.sprite != null && icon.sprite.Pointer == finished.Pointer;
        return done ? Loc.Get("tutorial.finished") : null;
    }

    /// <summary>
    /// "2 of 5" among the interactive siblings (buttons, toggles, sliders ...), if there are at least two.
    /// </summary>
    public static string Position(GameObject target)
    {
        Transform parent = target.transform.parent;
        if (parent == null || target.GetComponent<UnityEngine.UI.Selectable>() == null)
            return null;

        string across = PositionAcrossPanels(target.transform);
        if (across != null)
            return across;

        var items = new List<Transform>();
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            UnityEngine.UI.Selectable selectable = child.GetComponent<UnityEngine.UI.Selectable>();
            if (selectable == null || !child.gameObject.activeInHierarchy || !selectable.interactable)
                continue;
            // A full-screen button behind a menu or popup that closes it on a click outside; the
            // arrow keys never reach it.
            if (child != target.transform && child.name.EndsWith("ClickAway"))
                continue;
            items.Add(child);
        }

        // Options side by side count from left to right, the way the arrow keys move, also where the
        // game lists them the other way round (its yes/no questions show "Nein" left of "Ja").
        if (items.Count >= 2 && SideBySide(items))
            items = items.OrderBy(item => Center(item).x).ToList();

        // Unity's == compares the game objects; IndexOf would compare the interop wrappers.
        int index = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == target.transform)
                index = i + 1;
        }

        return items.Count >= 2 && index > 0 ? Loc.Get("position", index, items.Count) : null;
    }

    /// <summary>Whether the elements lie in one row: their centers differ by less than half an element's height.</summary>
    private static bool SideBySide(List<Transform> items)
    {
        RectTransform first = items[0].GetComponent<RectTransform>();
        if (first == null)
            return false;
        float y = Center(items[0]).y;
        float tolerance = Mathf.Abs(first.rect.height * first.lossyScale.y) / 2f;
        for (int i = 1; i < items.Count; i++)
        {
            if (items[i].GetComponent<RectTransform>() == null || Mathf.Abs(Center(items[i]).y - y) > tolerance)
                return false;
        }

        return true;
    }

    private static Vector3 Center(Transform transform)
    {
        RectTransform rect = transform.GetComponent<RectTransform>();
        return rect == null ? transform.position : rect.TransformPoint(rect.rect.center);
    }

    private static readonly Regex InstanceSuffix = new(@"\s*(\(\d+\)|\(Clone\))$");

    /// <summary>
    /// Copies of one panel side by side (the player columns of the custom game setup, "Panel (1)" to
    /// "Panel (5)"): the control's position among the same control in all copies, e.g. "2 of 5",
    /// instead of its place inside its own small panel.
    /// </summary>
    private static string PositionAcrossPanels(Transform control)
    {
        Transform panel = control.parent;
        Transform row = panel == null ? null : panel.parent;
        if (row == null)
            return null;
        string family = InstanceSuffix.Replace(panel.name, string.Empty);
        if (family == panel.name)
            return null;

        int index = 0, count = 0;
        for (int i = 0; i < row.childCount; i++)
        {
            Transform sibling = row.GetChild(i);
            if (!sibling.gameObject.activeInHierarchy || InstanceSuffix.Replace(sibling.name, string.Empty) != family)
                continue;
            Transform same = sibling.Find(control.name);
            UnityEngine.UI.Selectable selectable = same == null ? null : same.GetComponent<UnityEngine.UI.Selectable>();
            if (selectable == null || !same.gameObject.activeInHierarchy)
                continue;
            count++;
            if (sibling == panel)
                index = count;
        }

        return count >= 2 && index > 0 ? Loc.Get("position", index, count) : null;
    }

    /// <summary>
    /// Spoken name for a control without text, from "name.&lt;object name&gt;" in the language files.
    /// The parent's name is tried as well (e.g. "ButtonAccept" around a generic "ButtonInPanel").
    /// </summary>
    public static string NameOf(GameObject target)
    {
        string key = "name." + target.name;
        if (Loc.Has(key))
            return Loc.Get(key);

        // The parent's name wins over a generic or wrong child name
        // (the payment panel has "Nectar/ButtonInPanel_Seed").
        Transform parent = target.transform.parent;
        string parentKey = parent == null ? null : "name." + parent.name;
        if (parentKey != null && Loc.Has(parentKey))
            return Loc.Get(parentKey);

        // Food already picked for a payment: Enter takes it out again.
        FoodButton picked = target.GetComponent<FoodButton>();
        if (picked != null)
            return Loc.Get("name.picked_food", Game.GameReader.FoodTypeName(picked.FoodType));

        // Birdfeeder dice know their face; double faces (nectar or fruit) have a single food in their object name.
        DiceUI_Image die = target.GetComponent<DiceUI_Image>() ?? (parent == null ? null : parent.GetComponent<DiceUI_Image>());
        if (die != null)
            return Game.GameReader.FoodTypeName(die.FoodType);

        // Food choice buttons are named after their food, e.g. "Dice1/ButtonInPanel_Fruit".
        Match food = FoodButton.Match(target.name);
        if (food.Success && Loc.Has("name." + food.Groups[1].Value))
            return Loc.Get("name." + food.Groups[1].Value);

        // Icon-only buttons (the yes and no of a question): the icon they show, else what a click does.
        return ButtonIcon(target) ?? ButtonAction(target);
    }

    /// <summary>A known icon on the button ("sprite.&lt;name&gt;" in the language files), e.g. a tick for yes.</summary>
    private static string ButtonIcon(GameObject target)
    {
        foreach (UnityEngine.UI.Image image in target.GetComponentsInChildren<UnityEngine.UI.Image>(false))
        {
            string sprite = image.sprite == null ? null : image.sprite.name;
            if (!string.IsNullOrEmpty(sprite) && Loc.Has("sprite." + sprite))
                return Loc.Get("sprite." + sprite);
        }

        return null;
    }

    /// <summary>The method a button calls when clicked ("name.method.Next" = continue), for buttons without a label.</summary>
    private static string ButtonAction(GameObject target)
    {
        UnityEngine.UI.Button button = target.GetComponent<UnityEngine.UI.Button>();
        var click = button == null ? null : button.onClick;
        for (int i = 0; click != null && i < click.GetPersistentEventCount(); i++)
        {
            string key = "name.method." + click.GetPersistentMethodName(i);
            if (Loc.Has(key))
                return Loc.Get(key);
        }

        return null;
    }

    /// <summary>Icons and click handlers of a control, for the screen dump (finding names for unlabeled buttons).</summary>
    public static string ControlInternals(GameObject target)
    {
        var parts = new List<string>();
        foreach (UnityEngine.UI.Image image in target.GetComponentsInChildren<UnityEngine.UI.Image>(false))
        {
            if (image.sprite != null)
                parts.Add($"sprite {image.gameObject.name}:{image.sprite.name}");
        }

        UnityEngine.UI.Button button = target.GetComponent<UnityEngine.UI.Button>();
        var click = button == null ? null : button.onClick;
        for (int i = 0; click != null && i < click.GetPersistentEventCount(); i++)
            parts.Add($"click {click.GetPersistentMethodName(i)}");
        return string.Join("; ", parts);
    }

    /// <summary>
    /// Texts that label an unlabeled control: its siblings' texts, or else the texts of the nearest
    /// ancestor (up to two levels) that are not part of another control. Limited to four texts.
    /// </summary>
    public static List<string> ContextTexts(GameObject element)
    {
        List<string> texts = SiblingTexts(element);
        Transform ancestor = element.transform.parent;
        for (int depth = 0; texts.Count == 0 && ancestor != null && depth < 2; depth++)
        {
            ancestor = ancestor.parent;
            if (ancestor == null)
                break;

            var visibility = new Visibility(ancestor);
            foreach (TMPro.TMP_Text text in ancestor.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                if (texts.Count >= 4)
                    break;
                if (text.GetComponentInParent<UnityEngine.UI.Selectable>() != null || !visibility.IsVisible(text.transform))
                    continue;
                AddDistinct(texts, TextOf(text));
            }
        }

        return texts;
    }

    /// <summary>
    /// Cleaned text of a text object; bare values get a label from "label.&lt;object name&gt;"
    /// (e.g. the counter "446" becomes "Vogelkarten: 446").
    /// </summary>
    public static string TextOf(TMPro.TMP_Text text)
    {
        string clean = Clean(text.text);
        if (clean.Length == 0)
            return clean;

        // "label.<parent>/<name>" fixes one text in one place, "label.<name>" every text of that name.
        Transform parent = text.transform.parent;
        string qualified = parent == null ? null : $"label.{parent.name}/{text.name}";
        if (qualified != null && Loc.Has(qualified))
            return Loc.Get(qualified, clean);
        string key = "label." + text.name;
        return Loc.Has(key) ? Loc.Get(key, clean) : clean;
    }

    /// <summary>Visible texts directly under the element's parent that are not interactive themselves.</summary>
    public static List<string> SiblingTexts(GameObject element)
    {
        var result = new List<string>();
        Transform parent = element.transform.parent;
        if (parent == null)
            return result;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (!child.gameObject.activeInHierarchy || child.GetComponent<UnityEngine.UI.Selectable>() != null)
                continue;
            TMPro.TMP_Text text = child.GetComponent<TMPro.TMP_Text>();
            if (text != null)
                AddDistinct(result, TextOf(text));
        }

        return result;
    }

    public static List<string> CollectTexts(GameObject root)
    {
        var result = new List<string>();
        var visibility = new Visibility(root.transform);
        foreach (TMPro.TMP_Text text in root.GetComponentsInChildren<TMPro.TMP_Text>(false))
        {
            if (visibility.IsVisible(text.transform))
                AddDistinct(result, TextOf(text));
        }

        foreach (UnityEngine.UI.Text text in root.GetComponentsInChildren<UnityEngine.UI.Text>(false))
        {
            if (visibility.IsVisible(text.transform))
                AddDistinct(result, Clean(text.text));
        }

        return result;
    }

    /// <summary>
    /// Removes rich text tags. TMP sprites become words: controller glyphs are replaced by the
    /// mod's keyboard keys, known icons by their names, everything else by [sprite name].
    /// </summary>
    public static string Clean(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        // Text fields show an invisible zero-width space when empty.
        text = text.Replace("\u200B", string.Empty);
        // Repeated icons count ("[egg][egg]" reads "2 eggs"); icon names are marked so that an icon
        // repeating the words right before it ("play a bird [play a bird]") can be dropped.
        // Icons without a spoken name (decoration around a word) are dropped with their count.
        text = RepeatedSprite.Replace(text, match =>
        {
            string name = SpritePlural(match.Groups[1].Value);
            return name.Length == 0 ? " " : $" {SpriteTag.Matches(match.Value).Count} {name} ";
        });
        // "2 [card]" and "alle [card]" read "2 cards", "all cards".
        text = CountedSprite.Replace(text, match => $"{match.Groups[1].Value.TrimEnd()} {SpritePlural(match.Groups[2].Value)} ");
        text = SpriteTag.Replace(text, match => $" {IconStart}{SpriteName(match.Groups[1].Value)}{IconPlural}{SpritePlural(match.Groups[1].Value)}{IconEnd} ");
        // Bold, colour and similar tags sit inside words or quotes („<b>bezahlen</b>“) and take no room.
        text = InlineTag.Replace(text, string.Empty);
        text = AnyTag.Replace(text, " ");
        text = DropRepeatedIconNames(text);
        text = Whitespace.Replace(text, " ").Trim();
        // "[nest icon] Nest" in game texts would otherwise read "Höhlennest Nest".
        text = DoubledNest.Replace(text, "$1");
        // German game texts use the plural for one point, too ("1 Punkte").
        if (Loc.Language != null && Loc.Language.StartsWith("de"))
            text = OnePointGerman.Replace(text, "1 Punkt");
        text = SpaceAfterBracket.Replace(text, "(");
        text = SpaceBeforeHyphen.Replace(text, "$1-$2");
        return SpaceBeforePunctuation.Replace(text, "$1");
    }

    // Icons removed from "(<icon> Züge)" or "Rundenziel<icon>-Felder" leave stray spaces.
    private static readonly Regex SpaceAfterBracket = new(@"\(\s+");
    private static readonly Regex SpaceBeforeHyphen = new(@"(\w) -(\w)");

    private static readonly Regex OnePointGerman = new(@"(?<![\d,.])1 Punkte\b");
    private static readonly Regex RepeatedSprite = new(
        @"<sprite[^>]*?name=""?([^"">\s]+)""?[^>]*>(?:\s*<sprite[^>]*?name=""?\1""?[^>]*>)+", RegexOptions.IgnoreCase);
    private static readonly Regex CountedSprite = new(
        @"(\b(?:0|[2-9]|[1-9]\d+|alle|beide|mehrere|einige|viele|mehr|weitere|all|both|several|some|many|more)\s*)<sprite[^>]*?name=""?([^"">\s]+)""?[^>]*>",
        RegexOptions.IgnoreCase);
    private const char IconStart = '\u0001';
    private const char IconEnd = '\u0002';
    private const char IconPlural = '\u0003';

    /// <summary>Removes the icon markers; an icon whose name repeats the words before it is left out.</summary>
    private static string DropRepeatedIconNames(string text)
    {
        if (text.IndexOf(IconStart) < 0)
            return text;

        var result = new StringBuilder();
        int position = 0;
        while (position < text.Length)
        {
            int start = text.IndexOf(IconStart, position);
            int end = start < 0 ? -1 : text.IndexOf(IconEnd, start);
            if (end < 0)
            {
                result.Append(text, position, text.Length - position);
                break;
            }

            result.Append(text, position, start - position);
            string[] forms = text.Substring(start + 1, end - start - 1).Split(IconPlural);
            string name = forms[0];
            string before = result.ToString().TrimEnd();
            position = end + 1;
            // "Bonuskarten [bonus card]": the icon repeats the word before it, also in its plural form.
            if (EndsWithWord(before, forms))
                continue;

            // "Höhlennest ([cavity nest])": an icon in brackets that repeats the word before them
            // is left out with its brackets.
            string inner = before.EndsWith("(") ? before.Substring(0, before.Length - 1).TrimEnd() : null;
            if (inner != null && EndsWithWord(inner, forms))
            {
                result.Length = inner.Length;
                while (position < text.Length && text[position] == ' ')
                    position++;
                if (position < text.Length && text[position] == ')')
                    position++;
                continue;
            }

            // "[bowl nest] (Kugelnest)": the game names the icon itself in brackets right after it;
            // then only the game's name is read, without the brackets.
            int open = position;
            while (open < text.Length && text[open] == ' ')
                open++;
            int close = open < text.Length && text[open] == '(' ? text.IndexOf(')', open) : -1;
            string bracketed = close > open ? text.Substring(open + 1, close - open - 1).Trim() : null;
            if (bracketed != null && bracketed.Length > 0 && bracketed.Length <= 30 && bracketed.Split(' ').Length <= 3 &&
                bracketed.IndexOf(IconStart) < 0)
            {
                result.Append(bracketed);
                position = close + 1;
                continue;
            }

            result.Append(name);
        }

        return result.ToString().Replace(IconStart.ToString(), string.Empty).Replace(IconEnd.ToString(), string.Empty)
            .Replace(IconPlural.ToString(), string.Empty);
    }

    /// <summary>Whether the text ends with one of the forms as a whole word.</summary>
    private static bool EndsWithWord(string text, string[] forms)
    {
        foreach (string form in forms)
        {
            if (form.Length > 0 && text.EndsWith(form, StringComparison.OrdinalIgnoreCase) &&
                (text.Length == form.Length || !char.IsLetter(text[text.Length - form.Length - 1])))
                return true;
        }

        return false;
    }

    /// <summary>Plural name of an icon for counted repeats ("icon.egg.plural"), else its name.</summary>
    private static string SpritePlural(string sprite)
    {
        string plural = "icon." + sprite + ".plural";
        return Loc.HasOwn(plural) ? Loc.Get(plural) : SpriteName(sprite);
    }
    private static readonly Regex InlineTag = new(
        @"</?(b|i|u|s|color|size|font|font-weight|mark|style|link|alpha|cspace|mspace|voffset|nobr|lowercase|uppercase|smallcaps|sup|sub|noparse|gradient|material)(=[^>]*)?>",
        RegexOptions.IgnoreCase);

    private static readonly Regex DoubledNest = new(@"(nest) nest\b", RegexOptions.IgnoreCase);
    private static readonly Regex FoodButton = new(@"^ButtonInPanel_(\w+)$");
    private static readonly Regex SpaceBeforePunctuation = new(@"\s+([,.;:!?)])");

    /// <summary>
    /// Whether a text is on screen: 3D texts (TextMeshPro) need a rendered mesh, UI texts a visible
    /// canvas chain (see <see cref="Visibility"/>).
    /// </summary>
    public static bool IsShown(TMPro.TMP_Text text)
    {
        if (!text.gameObject.activeInHierarchy || !text.enabled)
            return false;
        MeshRenderer renderer = text.GetComponent<MeshRenderer>();
        if (renderer != null)
            return renderer.enabled && renderer.isVisible;
        return new Visibility(text.transform.root).IsVisible(text.transform);
    }

    private static string SpriteName(string sprite)
    {
        if (GlyphName.IsMatch(sprite))
        {
            string key = "glyph." + GlyphName.Replace(sprite, string.Empty);
            if (Loc.Has(key))
                return Loc.Get(key);
        }

        string icon = "icon." + sprite;
        if (Loc.Has(icon))
            return Loc.Get(icon);

        // Families of icons (bird packs, expansions) share a name: "birdpack_bb" reads "bird pack".
        int underscore = sprite.IndexOf('_');
        string family = underscore > 0 ? "icon." + sprite.Substring(0, underscore) : null;
        return family != null && Loc.Has(family) ? Loc.Get(family) : $"[{sprite}]";
    }

    /// <summary>Spoken name of a screen: mod translation, else a title text on the screen, else its type name.</summary>
    public static string ScreenName(GamepadScreen screen)
    {
        string typeName = screen.GetIl2CppType().Name;
        string key = "screen." + typeName;
        if (Loc.Has(key))
            return Loc.Get(key);

        string title = FindTitle(screen.gameObject);
        if (title != null)
            return title;

        // Bird powers and other decisions without a translated name: their instruction, else "decision".
        if (screen.TryCast<Decision>() != null)
        {
            List<string> instructions = DecisionAnnouncer.InstructionTexts(screen);
            return instructions.Count > 0 ? instructions[0] : Loc.Get("screen.decision");
        }

        return HumanizeTypeName(typeName);
    }

    /// <summary>First visible text whose object (or parent) is named like a title or header.</summary>
    public static string FindTitle(GameObject root)
    {
        var visibility = new Visibility(root.transform);
        foreach (TMPro.TMP_Text text in root.GetComponentsInChildren<TMPro.TMP_Text>(false))
        {
            if (!LooksLikeTitle(text.transform) || !visibility.IsVisible(text.transform))
                continue;
            string clean = Clean(text.text);
            if (clean.Length > 0)
                return clean;
        }

        return null;
    }

    /// <summary>
    /// Label of the group the element belongs to: a text that is a direct sibling of the element
    /// (e.g. "Wissenswertes anzeigen:" above a set of options).
    /// </summary>
    /// <summary>
    /// For an element on one of several tabs (the tutorial's courses, the shop's pages): the tab's
    /// name and place, "Europa-Erweiterung, Reiter 2 von 5"; null elsewhere. Spoken when the
    /// selection enters the tab, so switching with Control+Tab says where it went.
    /// </summary>
    public static string TabLabel(GameObject element, Transform screen)
    {
        View.UI.Utils.Tabs.Tab tab = element.GetComponentInParent<View.UI.Utils.Tabs.Tab>();
        if (tab == null || screen == null)
            return null;

        foreach (View.UI.Utils.Tabs.TabsManager manager in screen.GetComponentsInChildren<View.UI.Utils.Tabs.TabsManager>(true))
        {
            var toggles = manager.visibleTabToggles;
            if (toggles == null || toggles.Count < 2)
                continue;
            for (int i = 0; i < toggles.Count; i++)
            {
                View.UI.Utils.Tabs.TabToggle toggle = toggles[i];
                if (toggle == null || toggle.ControlledTab == null || toggle.ControlledTab.GetInstanceID() != tab.GetInstanceID())
                    continue;
                return Loc.Get("tab.label", TabName(toggle, element), i + 1, toggles.Count);
            }
        }

        return null;
    }

    /// <summary>The text on the tab's button; for the tutorial's courses (icons only) the course's name.</summary>
    private static string TabName(View.UI.Utils.Tabs.TabToggle toggle, GameObject element)
    {
        foreach (TMPro.TMP_Text text in toggle.GetComponentsInChildren<TMPro.TMP_Text>(false))
        {
            string clean = Clean(text.text);
            if (clean.Length > 0)
                return clean;
        }

        TutorialTabContentManager course = element.GetComponentInParent<TutorialTabContentManager>();
        if (course != null && Loc.Has("course." + course.Course))
            return Loc.Get("course." + course.Course);
        return string.IsNullOrEmpty(toggle.TabName) ? Loc.Get("tab.unnamed") : toggle.TabName;
    }

    public static string GroupLabel(GameObject element, Transform screen = null)
    {
        // Groups named in the language files by their panel and its parent ("group.FilterToggles/Food":
        // the food row of the atlas filters, whose header is only icons).
        Transform group = element.transform.parent;
        string groupKey = group == null || group.parent == null ? null : $"group.{group.parent.name}/{group.name}";
        if (groupKey != null && Loc.Has(groupKey))
            return Loc.Get(groupKey);

        List<string> siblings = SiblingTexts(element);
        if (siblings.Count > 0)
            return siblings[0];

        // Panels with a header next to their buttons, e.g. the online menu's "Echtzeit, 3 Minuten pro
        // Zug" and "Asynchron, 72 Stunden pro Zug", which both offer a "Standardspiel". The search
        // stays inside the screen: the pause menu sits right on the game canvas, whose header holds
        // the opponents' speech bubbles.
        // Controls outside the screen (the starting cards of the setup, which sit on the game canvas)
        // only look at their own panel.
        int levels = screen != null && !element.transform.IsChildOf(screen) ? 1 : 2;
        Transform panel = element.transform.parent;
        for (int depth = 0; panel != null && depth < levels; depth++, panel = panel.parent)
        {
            // A root canvas is the whole interface (the game canvas with the players' headers), never
            // the panel of one group of controls.
            if (IsRootCanvas(panel))
                break;
            string header = HeaderTexts(panel, element.transform);
            if (header != null)
                return header;
            if (panel == screen)
                break;
        }

        return null;
    }

    private static bool IsRootCanvas(Transform transform)
    {
        Canvas canvas = transform.GetComponent<Canvas>();
        return canvas != null && canvas.isRootCanvas;
    }

    private static string HeaderTexts(Transform panel, Transform exclude)
    {
        for (int i = 0; i < panel.childCount; i++)
        {
            Transform child = panel.GetChild(i);
            if (child == exclude || exclude.IsChildOf(child) || !child.gameObject.activeInHierarchy || !child.name.Contains("Header"))
                continue;
            var texts = new List<string>();
            foreach (TMPro.TMP_Text text in child.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                string clean = IsShown(text) ? Clean(text.text) : string.Empty;
                if (clean.Length > 0)
                    texts.Add(clean);
            }

            if (texts.Count > 0)
                return string.Join(", ", texts);
        }

        return null;
    }

    /// <summary>Joins texts into sentences without doubling punctuation ("Text." + "More" -> "Text. More").</summary>
    public static string JoinSentences(IEnumerable<string> parts)
    {
        var builder = new StringBuilder();
        foreach (string part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
                continue;
            if (builder.Length > 0)
            {
                char last = builder[builder.Length - 1];
                builder.Append(last is '.' or '!' or '?' or ':' ? " " : ". ");
            }

            builder.Append(part.Trim());
        }

        return builder.ToString();
    }

    public static string HumanizeTypeName(string typeName)
    {
        string name = typeName.Replace("_", " ");
        return CamelCase.Replace(name, " ").Trim();
    }

    public static string PathOf(GameObject target)
    {
        var builder = new StringBuilder(target.name);
        Transform parent = target.transform.parent;
        for (int depth = 0; parent != null && depth < 8; depth++, parent = parent.parent)
            builder.Insert(0, parent.name + "/");
        return builder.ToString();
    }

    private static bool LooksLikeTitle(Transform transform)
    {
        for (int depth = 0; transform != null && depth < 2; depth++, transform = transform.parent)
        {
            string name = transform.name;
            if (name.IndexOf("title", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("header", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static string RoleOf(GameObject target, UnityEngine.UI.Slider slider)
    {
        UnityEngine.UI.Toggle toggle = target.GetComponent<UnityEngine.UI.Toggle>();
        if (toggle != null)
            return ToggleState(toggle);

        if (slider != null)
            return Loc.Get("role.slider", SliderValue(slider));

        if (target.GetComponent<TMPro.TMP_Dropdown>() != null)
            return Loc.Get("role.dropdown");

        if (target.GetComponent<TMPro.TMP_InputField>() != null)
            return Loc.Get("role.input");

        UnityEngine.UI.Selectable selectable = target.GetComponent<UnityEngine.UI.Selectable>();
        if (selectable != null && !selectable.interactable)
            return Loc.Get("state.unavailable");

        return target.GetComponent<UnityEngine.UI.Button>() != null ? Loc.Get("role.button") : null;
    }

    /// <summary>Toggles in a group behave like tabs or radio buttons: "selected"; single ones are checkboxes.</summary>
    public static string ToggleState(UnityEngine.UI.Toggle toggle)
    {
        string special = SetupDescriber.ToggleValue(toggle);
        if (special != null)
            return special;
        if (toggle.group != null)
            return Loc.Get(toggle.isOn ? "role.option.on" : "role.option.off");
        return Loc.Get(toggle.isOn ? "role.toggle.on" : "role.toggle.off");
    }

    /// <summary>Percent for 0..1 sliders, the plain value otherwise.</summary>
    public static string SliderValue(UnityEngine.UI.Slider slider)
    {
        if (slider.minValue == 0f && slider.maxValue == 1f)
            return $"{Mathf.RoundToInt(slider.value * 100f)} %";
        return slider.wholeNumbers || Mathf.Approximately(slider.value, Mathf.Round(slider.value))
            ? Mathf.RoundToInt(slider.value).ToString()
            : slider.value.ToString("0.##");
    }

    private static void AddDistinct(List<string> list, string value)
    {
        if (value.Length > 0 && !list.Contains(value))
            list.Add(value);
    }
}

/// <summary>
/// Decides whether a UI element is shown: the game hides screens by disabling their Canvas or by
/// fading a CanvasGroup to zero, so "active in hierarchy" is not enough. Results are cached per call.
/// </summary>
internal sealed class Visibility
{
    private readonly Transform _root;
    private readonly Dictionary<int, bool> _cache = new();

    public Visibility(Transform root) => _root = root;

    public bool IsVisible(Transform transform)
    {
        var path = new List<int>();
        bool visible = true;
        for (Transform current = transform; current != null; current = current.parent)
        {
            int id = current.GetInstanceID();
            if (_cache.TryGetValue(id, out bool cached))
            {
                visible = cached;
                break;
            }

            path.Add(id);
            CanvasGroup group = current.GetComponent<CanvasGroup>();
            Canvas canvas = current.GetComponent<Canvas>();
            if ((group != null && group.alpha <= 0.01f) || (canvas != null && !canvas.enabled))
            {
                visible = false;
                break;
            }

            if (current == _root)
                break;
        }

        foreach (int id in path)
            _cache[id] = visible;
        return visible;
    }
}
