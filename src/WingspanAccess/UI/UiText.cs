using System;
using System.Collections.Generic;
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
            string special = SetupDescriber.Describe(target) ?? CardDescriber.Describe(target) ?? SaveDescriber.Describe(target) ??
                NewsDescriber.Describe(target);
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
            var parts = name != null ? SiblingTexts(target) : ContextTexts(target);
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
    /// "2 of 5" among the interactive siblings of the same kind (toggle, button, slider ...),
    /// if there are at least two.
    /// </summary>
    public static string Position(GameObject target)
    {
        Transform parent = target.transform.parent;
        UnityEngine.UI.Selectable own = target.GetComponent<UnityEngine.UI.Selectable>();
        if (parent == null || own == null)
            return null;

        string kind = own.GetIl2CppType().Name;
        int index = 0, count = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            UnityEngine.UI.Selectable selectable = child.GetComponent<UnityEngine.UI.Selectable>();
            if (selectable == null || !child.gameObject.activeInHierarchy || !selectable.interactable ||
                selectable.GetIl2CppType().Name != kind)
                continue;
            count++;
            if (child == target.transform)
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
        return food.Success && Loc.Has("name." + food.Groups[1].Value) ? Loc.Get("name." + food.Groups[1].Value) : null;
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
        // Repeated icons count ("[egg][egg]" reads "2 eggs"); icon names are marked so that an icon
        // repeating the words right before it ("play a bird [play a bird]") can be dropped.
        text = RepeatedSprite.Replace(text, match => $" {SpriteTag.Matches(match.Value).Count} {SpritePlural(match.Groups[1].Value)} ");
        text = SpriteTag.Replace(text, match => $" {IconStart}{SpriteName(match.Groups[1].Value)}{IconEnd} ");
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
    private const char IconStart = '\u0001';
    private const char IconEnd = '\u0002';

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
            string name = text.Substring(start + 1, end - start - 1);
            string before = result.ToString().TrimEnd();
            bool repeats = name.Length > 0 && before.EndsWith(name, StringComparison.OrdinalIgnoreCase) &&
                (before.Length == name.Length || !char.IsLetter(before[before.Length - name.Length - 1]));
            if (!repeats)
                result.Append(name);
            position = end + 1;
        }

        return result.ToString().Replace(IconStart.ToString(), string.Empty).Replace(IconEnd.ToString(), string.Empty);
    }

    /// <summary>Plural name of an icon for counted repeats ("icon.egg.plural"), else its name.</summary>
    private static string SpritePlural(string sprite)
    {
        string plural = "icon." + sprite + ".plural";
        return Loc.Has(plural) ? Loc.Get(plural) : SpriteName(sprite);
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
    public static string GroupLabel(GameObject element)
    {
        List<string> siblings = SiblingTexts(element);
        return siblings.Count > 0 ? siblings[0] : null;
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
