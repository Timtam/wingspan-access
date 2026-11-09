using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace WingspanAccess.Localization;

/// <summary>
/// The mod's own strings in the game's current language (I2 Localization), with English as fallback.
/// Translations are embedded JSON files named after I2 language codes (en.json, de.json, ...).
/// </summary>
internal static class Loc
{
    private const string Fallback = "en";
    private static readonly Dictionary<string, Dictionary<string, string>> Tables = new(StringComparer.OrdinalIgnoreCase);
    private static string _language = Fallback;

    public static string Language => _language;

    public static void Initialize()
    {
        Assembly assembly = typeof(Loc).Assembly;
        const string prefix = "WingspanAccess.Localization.";
        foreach (string resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(prefix) || !resource.EndsWith(".json"))
                continue;

            string code = resource.Substring(prefix.Length, resource.Length - prefix.Length - ".json".Length);
            using Stream stream = assembly.GetManifestResourceStream(resource);
            Tables[code] = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
        }

        Plugin.Logger.LogInfo($"[Loc] mod languages: {string.Join(", ", Tables.Keys)}");
    }

    /// <summary>Follows the game's language; call once per frame.</summary>
    public static bool UpdateLanguage()
    {
        string code = I2.Loc.LocalizationManager.CurrentLanguageCode;
        if (string.IsNullOrEmpty(code) || string.Equals(code, _language, StringComparison.OrdinalIgnoreCase))
            return false;

        _language = code;
        Plugin.Logger.LogInfo($"[Loc] game language: {code}, mod strings: {(Resolve(code) ?? Fallback)}");
        return true;
    }

    public static string Get(string key, params object[] args)
    {
        string text = Lookup(Resolve(_language), key) ?? Lookup(Fallback, key) ?? key;
        return args.Length == 0 ? text : string.Format(text, args);
    }

    public static bool Has(string key) => Lookup(Resolve(_language), key) != null || Lookup(Fallback, key) != null;

    /// <summary>
    /// Whether the strings for the game's language (English when the mod has none for it) define the
    /// key, without falling back to English. For word forms such as "key.plural" and "key.one": a
    /// language that needs no form of its own (German "Samen") must not get the English one ("seeds").
    /// </summary>
    public static bool HasOwn(string key) => Lookup(Resolve(_language) ?? Fallback, key) != null;

    /// <summary>Uses "key.one" for a count of one when the language file has it ("1 Punkt" vs "2 Punkte").</summary>
    public static string Count(string key, int count, params object[] moreArgs)
    {
        var args = new object[moreArgs.Length + 1];
        args[0] = count;
        moreArgs.CopyTo(args, 1);
        string singular = key + ".one";
        return count == 1 && HasOwn(singular) ? Get(singular, args) : Get(key, args);
    }

    /// <summary>Maps "pt-BR" to "pt-BR" or "pt", whichever table exists.</summary>
    private static string Resolve(string code)
    {
        if (code == null)
            return null;
        if (Tables.ContainsKey(code))
            return code;
        int dash = code.IndexOf('-');
        return dash > 0 && Tables.ContainsKey(code.Substring(0, dash)) ? code.Substring(0, dash) : null;
    }

    private static string Lookup(string language, string key) =>
        language != null && Tables.TryGetValue(language, out var table) && table.TryGetValue(key, out string value) ? value : null;
}
