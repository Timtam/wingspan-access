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

        // Controls outside the top screen (shared "next" buttons, HUD toggles) with their state.
        foreach (UnityEngine.UI.Selectable selectable in Object.FindObjectsOfType<UnityEngine.UI.Selectable>())
        {
            if (!selectable.gameObject.activeInHierarchy || !new Visibility(selectable.transform.root).IsVisible(selectable.transform))
                continue;
            Plugin.Logger.LogInfo($"[Dump] control {UiText.PathOf(selectable.gameObject)} type={selectable.GetIl2CppType().Name} interactable={selectable.interactable}");
        }

        return count;
    }

    /// <summary>
    /// Writes every I2 localization term with its raw translation in the current language to
    /// BepInEx/terms-&lt;language&gt;.txt, one "term => text" per line (line breaks as \n), so the
    /// game's own texts (tutorial hints, rich text tags) can be searched offline. Returns the count.
    /// </summary>
    public static int DumpAllTerms()
    {
        var terms = I2.Loc.LocalizationManager.GetTermsList(null);
        var lines = new List<string>(terms.Count);
        for (int i = 0; i < terms.Count; i++)
        {
            string term = terms[i];
            string translation = I2.Loc.LocalizationManager.GetTranslation(term, true, 0, true, false, null, null, true) ?? string.Empty;
            lines.Add($"{term} => {translation.Replace("\r", string.Empty).Replace("\n", "\\n")}");
        }

        string path = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, $"terms-{I2.Loc.LocalizationManager.CurrentLanguageCode}.txt");
        System.IO.File.WriteAllLines(path, lines);
        Plugin.Logger.LogInfo($"[Term] {lines.Count} terms written to {path}");
        return lines.Count;
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

        Speech.ScreenReader.SayTransient("Sprites written to log");
    }
}
