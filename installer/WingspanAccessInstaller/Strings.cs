using System;
using System.Collections.Generic;
using System.Globalization;

namespace WingspanAccessInstaller;

/// <summary>The installer's texts in English and German, chosen by the Windows display language.</summary>
internal static class Strings
{
    private static bool _german = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";

    private static readonly Dictionary<string, (string En, string De)> Table = new()
    {
        ["title"] = ("Wingspan Access Installer", "Wingspan Access Installer"),
        ["intro"] = ("This installs Wingspan Access, the screen reader mod for Wingspan, together with the BepInEx mod loader.",
                     "Dies installiert Wingspan Access, den Screenreader-Mod für Wingspan, zusammen mit dem Mod-Loader BepInEx."),
        ["game_folder"] = ("&Game folder", "&Spielordner"),
        ["change"] = ("C&hoose folder...", "&Ordner wählen …"),
        ["choose_folder"] = ("Select Wingspan.exe in the game folder", "Wingspan.exe im Spielordner auswählen"),
        ["log"] = ("&Progress", "&Verlauf"),

        ["installed"] = ("Installed version: {0}", "Installierte Version: {0}"),
        ["not_installed"] = ("Wingspan Access is not installed.", "Wingspan Access ist nicht installiert."),
        ["latest"] = ("Latest version: {0}", "Neueste Version: {0}"),
        ["latest_checking"] = ("Latest version: checking ...", "Neueste Version: wird geprüft …"),
        ["latest_unknown"] = ("Latest version: unknown ({0})", "Neueste Version: unbekannt ({0})"),
        ["latest_none"] = ("Latest version: none published yet", "Neueste Version: noch keine veröffentlicht"),
        ["no_repo"] = ("this build has no update source", "dieser Build hat keine Update-Quelle"),
        ["check_failed"] = ("no connection: {0}", "keine Verbindung: {0}"),
        ["bepinex"] = ("BepInEx: {0}", "BepInEx: {0}"),
        ["bepinex_missing"] = ("BepInEx: not installed", "BepInEx: nicht installiert"),

        ["install"] = ("&Install", "&Installieren"),
        ["update"] = ("&Update to {0}", "&Aktualisieren auf {0}"),
        ["reinstall"] = ("&Reinstall", "&Neu installieren"),
        ["uninstall"] = ("U&ninstall", "&Deinstallieren"),
        ["remove_bepinex"] = ("Also remove &BepInEx when uninstalling (other BepInEx mods stop working)",
                              "Beim Deinstallieren auch &BepInEx entfernen (andere BepInEx-Mods funktionieren dann nicht mehr)"),
        ["close"] = ("&Close", "Sch&ließen"),
        ["cancel"] = ("&Cancel", "Abbre&chen"),
        ["close_game"] = ("Close &Wingspan", "&Wingspan beenden"),

        ["game_missing"] = ("Wingspan was not found. Choose the game folder (the one with Wingspan.exe).",
                            "Wingspan wurde nicht gefunden. Wähle den Spielordner (den mit Wingspan.exe)."),
        ["not_game_folder"] = ("{0} does not contain Wingspan.exe.", "{0} enthält keine Wingspan.exe."),
        ["game_running"] = ("Wingspan is running. Please close the game first.", "Wingspan läuft. Bitte beende zuerst das Spiel."),

        ["checking"] = ("Checking for the latest version ...", "Suche nach der neuesten Version …"),
        ["downloading"] = ("Downloading {0} ...", "Lade {0} herunter …"),
        ["using_package"] = ("Using the package {0}.", "Verwende das Paket {0}."),
        ["extracting"] = ("Extracting the package ...", "Entpacke das Paket …"),
        ["installing_bepinex"] = ("Installing BepInEx {0} ...", "Installiere BepInEx {0} …"),
        ["keeping_bepinex"] = ("BepInEx {0} is already installed.", "BepInEx {0} ist bereits installiert."),
        ["installing_mod"] = ("Installing Wingspan Access {0} ...", "Installiere Wingspan Access {0} …"),
        ["config"] = ("Adjusting the BepInEx settings ...", "Passe die BepInEx-Einstellungen an …"),
        ["removing"] = ("Removing {0} ...", "Entferne {0} …"),

        ["done_install"] = ("Wingspan Access {0} is installed.\n\nThe first start of Wingspan takes longer than usual, about 1 to 3 minutes, because BepInEx prepares the game. You hear \"Wingspan Access loaded\" when it is ready.",
                            "Wingspan Access {0} ist installiert.\n\nDer erste Start von Wingspan dauert länger als sonst, etwa 1 bis 3 Minuten, weil BepInEx das Spiel vorbereitet. Wenn alles bereit ist, hörst du „Wingspan Access geladen“."),
        ["installed_short"] = ("Wingspan Access {0} is installed.", "Wingspan Access {0} ist installiert."),
        ["done_uninstall"] = ("Wingspan Access was removed.", "Wingspan Access wurde entfernt."),
        ["done_uninstall_bepinex"] = ("Wingspan Access and BepInEx were removed.", "Wingspan Access und BepInEx wurden entfernt."),
        ["nothing_installed"] = ("Wingspan Access is not installed in {0}.", "Wingspan Access ist nicht in {0} installiert."),
        ["confirm_uninstall"] = ("Remove Wingspan Access from {0}?", "Wingspan Access aus {0} entfernen?"),
        ["confirm_remove_bepinex"] = ("Also remove BepInEx? Choose No if you use other BepInEx mods for Wingspan.",
                                      "Auch BepInEx entfernen? Wähle Nein, wenn du andere BepInEx-Mods für Wingspan nutzt."),

        ["error"] = ("Something went wrong: {0}\n\nThe log file is {1}", "Etwas ist schiefgegangen: {0}\n\nDie Protokolldatei ist {1}"),
        ["no_package"] = ("There is no package to install: {0}", "Es gibt kein Paket zum Installieren: {0}"),
        ["no_package_local"] = ("put the WingspanAccess ZIP next to the installer or start it with --zip <file>",
                                "lege die WingspanAccess-ZIP neben den Installer oder starte ihn mit --zip <Datei>"),
        ["not_a_package"] = ("{0} is not a Wingspan Access package.", "{0} ist kein Wingspan-Access-Paket."),
        ["write_failed"] = ("Could not write to {0}. Start the installer as administrator?",
                            "Konnte nicht nach {0} schreiben. Den Installer als Administrator starten?"),
        ["other_bepinex"] = ("BepInEx {0} is installed, Wingspan Access is made for {1}. Replace it? Choose No to keep it if other mods need it.",
                             "BepInEx {0} ist installiert, Wingspan Access ist für {1} gemacht. Ersetzen? Wähle Nein, wenn andere Mods die installierte Version brauchen."),

        ["update_waiting"] = ("The update is downloaded. Please close Wingspan; it is installed as soon as the game has closed.",
                              "Das Update ist heruntergeladen. Bitte beende Wingspan; es wird installiert, sobald das Spiel geschlossen ist."),
        ["up_to_date"] = ("Wingspan Access is up to date (version {0}).", "Wingspan Access ist aktuell (Version {0})."),
        ["start_game_question"] = ("The update is installed. Start Wingspan now?", "Das Update ist installiert. Wingspan jetzt starten?"),
        ["start_game_now"] = ("Start Wingspan now?", "Wingspan jetzt starten?"),
        ["starting_game"] = ("Starting Wingspan ...", "Starte Wingspan …"),
        ["canceled"] = ("Canceled.", "Abgebrochen."),
        ["new_installer"] = ("A newer installer (version {0}) is available. Download and start it now?",
                             "Ein neuerer Installer (Version {0}) ist verfügbar. Jetzt herunterladen und starten?"),
        ["self_updating"] = ("Starting the new installer ...", "Starte den neuen Installer …"),
    };

    /// <summary>Overrides the display language ("de" or "en").</summary>
    public static void SetLanguage(string code)
    {
        if (!string.IsNullOrEmpty(code))
            _german = code.StartsWith("de", StringComparison.OrdinalIgnoreCase);
    }

    public static string Get(string key, params object[] args)
    {
        if (!Table.TryGetValue(key, out var text))
            return key;
        string format = _german ? text.De : text.En;
        return args.Length == 0 ? format : string.Format(format, args);
    }
}
