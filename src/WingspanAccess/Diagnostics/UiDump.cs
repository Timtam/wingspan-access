using System.Collections.Generic;
using UnityEngine;
using WingspanAccess.UI;

namespace WingspanAccess.Diagnostics;

/// <summary>Development dumps written to the BepInEx log.</summary>
internal static class UiDump
{
    /// <summary>Logs every visible text under a root with its path. Returns the number of texts.</summary>
    public static int DumpVisibleTexts(GameObject root)
    {
        var visibility = new Visibility(root.transform);
        int count = 0;
        Plugin.Logger.LogInfo($"[Dump] ---- visible texts under {UiText.PathOf(root)}");
        foreach (TMPro.TMP_Text text in root.GetComponentsInChildren<TMPro.TMP_Text>(false))
        {
            if (!visibility.IsVisible(text.transform))
                continue;
            string clean = UiText.Clean(text.text);
            if (clean.Length == 0)
                continue;
            Plugin.Logger.LogInfo($"[Dump] text {UiText.PathOf(text.gameObject)} => {clean}");
            count++;
        }

        return count;
    }

    /// <summary>Logs every visible text in all canvases (the game draws HUD, hand and decisions on separate canvases).</summary>
    public static int DumpAllVisibleTexts()
    {
        int count = 0;
        Plugin.Logger.LogInfo("[Dump] ---- visible texts in all canvases");
        foreach (TMPro.TMP_Text text in Object.FindObjectsOfType<TMPro.TMP_Text>())
        {
            if (!text.gameObject.activeInHierarchy)
                continue;
            Transform root = text.transform.root;
            if (!new Visibility(root).IsVisible(text.transform))
                continue;
            string clean = UiText.Clean(text.text);
            if (clean.Length == 0)
                continue;
            Plugin.Logger.LogInfo($"[Dump] text {UiText.PathOf(text.gameObject)} => {clean}");
            count++;
        }

        return count;
    }

    /// <summary>Logs I2 localization terms (with their translation) whose name matches a pattern.</summary>
    public static void DumpTerms(string pattern, int limit = 400)
    {
        var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var terms = I2.Loc.LocalizationManager.GetTermsList(null);
        int count = 0;
        for (int i = 0; i < terms.Count && count < limit; i++)
        {
            string term = terms[i];
            if (!regex.IsMatch(term))
                continue;
            string translation = I2.Loc.LocalizationManager.GetTranslation(term, true, 0, true, false, null, null, true);
            Plugin.Logger.LogInfo($"[Term] {term} => {translation}");
            count++;
        }

        Plugin.Logger.LogInfo($"[Term] {count} of {terms.Count} terms matched {pattern}");
    }

    /// <summary>Logs the names of all sprites in loaded TextMeshPro sprite assets (icons used in texts).</summary>
    public static void DumpSpriteNames()
    {
        var seen = new HashSet<string>();
        foreach (TMPro.TMP_SpriteAsset asset in Resources.FindObjectsOfTypeAll<TMPro.TMP_SpriteAsset>())
        {
            var names = new List<string>();
            foreach (TMPro.TMP_SpriteCharacter character in asset.spriteCharacterTable)
            {
                if (character != null && !string.IsNullOrEmpty(character.name))
                    names.Add(character.name);
            }

            if (seen.Add(asset.name))
                Plugin.Logger.LogInfo($"[Sprites] {asset.name} ({names.Count}): {string.Join(", ", names)}");
        }

        Speech.ScreenReader.Say("Sprites written to log");
    }
}
