using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WingspanAccessInstaller;

/// <summary>
/// Command line options. Without any, the installer shows its window. The mod starts it with
/// "--update --restart --game &lt;folder&gt;", the uninstall entry with "--uninstall --game &lt;folder&gt;".
/// "--silent" runs without any window (for scripts and tests).
/// </summary>
internal sealed class Options
{
    public const string Usage =
        "WingspanAccessInstaller [--game <folder>] [--zip <package>] [--lang de|en]\n" +
        "  --update [--restart]   update while or after the game runs (started by the mod)\n" +
        "  --uninstall            remove the mod, asks about BepInEx\n" +
        "  --silent --install | --uninstall | --detect | --check\n" +
        "     [--remove-bepinex] [--replace-bepinex] [--no-registry] [--repo <owner/name>]";

    private readonly List<string> _unknown = new();

    public bool Silent { get; private set; }
    public bool Install { get; private set; }
    public bool Uninstall { get; private set; }
    public bool Detect { get; private set; }
    public bool Check { get; private set; }
    public bool Update { get; private set; }
    public bool Restart { get; private set; }
    public bool Relaunched { get; private set; }
    public bool NoSelfUpdate { get; private set; }
    public bool RemoveBepInEx { get; private set; }
    public bool ReplaceBepInEx { get; private set; }
    public bool NoRegistry { get; private set; }
    public string GameDir { get; private set; }
    public string ZipPath { get; private set; }
    public string Repository { get; private set; } = BuildInfo.RepositorySlug;
    public string Language { get; private set; }

    /// <summary>Arguments the installer did not understand; they are logged and ignored.</summary>
    public IReadOnlyList<string> Unknown => _unknown;

    public static Options Parse(string[] args)
    {
        var options = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string name = args[i];
            string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{name} needs a value.");
            switch (name.ToLowerInvariant())
            {
                case "--silent": options.Silent = true; break;
                case "--install": options.Install = true; break;
                case "--uninstall": options.Uninstall = true; break;
                case "--detect": options.Detect = true; break;
                case "--check": options.Check = true; break;
                case "--update": options.Update = true; break;
                case "--restart": options.Restart = true; break;
                case "--relaunched": options.Relaunched = true; break;
                case "--no-self-update": options.NoSelfUpdate = true; break;
                case "--remove-bepinex": options.RemoveBepInEx = true; break;
                case "--replace-bepinex": options.ReplaceBepInEx = true; break;
                case "--no-registry": options.NoRegistry = true; break;
                case "--game": options.GameDir = Value(); break;
                case "--zip": options.ZipPath = Path.GetFullPath(Value()); break;
                case "--repo": options.Repository = Value(); break;
                case "--lang": options.Language = Value(); break;
                default: options._unknown.Add(name); break;
            }
        }

        return options;
    }

    /// <summary>
    /// The options as arguments again, with full paths, for another copy of the installer (from
    /// %TEMP%, a newer one, or one with administrator rights). <paramref name="gameDir"/> replaces
    /// the game folder if given.
    /// </summary>
    public List<string> ToArguments(string gameDir, params string[] extra)
    {
        var list = new List<string>();
        void Flag(bool on, string flag)
        {
            if (on)
                list.Add(flag);
        }
        void Value(string value, string flag)
        {
            if (!string.IsNullOrEmpty(value))
                list.AddRange(new[] { flag, value });
        }

        Flag(Silent, "--silent");
        Flag(Install, "--install");
        Flag(Uninstall, "--uninstall");
        Flag(Detect, "--detect");
        Flag(Check, "--check");
        Flag(Update, "--update");
        Flag(Restart, "--restart");
        Flag(NoSelfUpdate, "--no-self-update");
        Flag(RemoveBepInEx, "--remove-bepinex");
        Flag(ReplaceBepInEx, "--replace-bepinex");
        Flag(NoRegistry, "--no-registry");
        Value(gameDir ?? GameDir, "--game");
        Value(ZipPath, "--zip");
        Value(Repository == BuildInfo.RepositorySlug ? null : Repository, "--repo");
        Value(Language, "--lang");
        list.AddRange(extra);
        return list;
    }

    public static string Join(IEnumerable<string> arguments) => string.Join(" ", arguments.Select(Quote));

    /// <summary>Quotes one argument so that Windows programs split the command line back into it.</summary>
    public static string Quote(string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
            return argument;

        var quoted = new StringBuilder("\"");
        int backslashes = 0;
        foreach (char c in argument)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            // Backslashes count only before a quote: double them there, and escape the quote.
            quoted.Append('\\', c == '"' ? backslashes * 2 + 1 : backslashes);
            quoted.Append(c);
            backslashes = 0;
        }

        quoted.Append('\\', backslashes * 2);
        quoted.Append('"');
        return quoted.ToString();
    }
}
