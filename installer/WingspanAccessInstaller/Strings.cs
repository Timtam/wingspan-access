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

        ["report_button"] = ("Report a pro&blem ...", "&Problem melden …"),
        ["report_unavailable"] = ("Problem reports are not available in this build: it was built without a form key.",
                                  "Problemberichte sind in diesem Build nicht verfügbar: Er wurde ohne Formularschlüssel gebaut."),
        ["report_title"] = ("Report a problem - Wingspan Access", "Problem melden – Wingspan Access"),
        ["report_shortcut"] = ("Wingspan Access - Report a problem", "Wingspan Access – Problem melden"),
        ["report_intro"] = ("Describe what happened and what you did just before. The report goes by e-mail to the developer of Wingspan Access, through the form service Static Forms (staticforms.dev).",
                            "Beschreibe, was passiert ist und was du kurz vorher gemacht hast. Der Bericht geht per E-Mail an den Entwickler von Wingspan Access, über den Formulardienst Static Forms (staticforms.dev)."),
        ["report_description"] = ("&What happened?", "&Was ist passiert?"),
        ["report_email"] = ("Your &e-mail address for questions (optional)", "Deine &E-Mail-Adresse für Rückfragen (freiwillig)"),
        ["report_files"] = ("&Attachments (Space checks or unchecks one)", "&Anhänge (die Leertaste wählt einen aus oder ab)"),
        ["report_privacy"] = ("Before sending, your Windows user name, your computer name and your Steam ID are replaced in the logs. The logs of online games contain the names of the other players. Crash dumps can't be cleaned: they hold part of the game's memory at the moment of the crash and can contain paths and names. So they only go along when you check them.",
                              "Vor dem Senden werden in den Logs dein Windows-Benutzername, dein Computername und deine Steam-ID ersetzt. Die Logs von Online-Spielen enthalten die Namen der anderen Spieler. Absturz-Speicherabbilder lassen sich nicht bereinigen: Sie enthalten einen Teil des Spielspeichers im Moment des Absturzes und können Pfade und Namen enthalten. Darum gehen sie nur mit, wenn du sie ankreuzt."),
        ["report_system"] = ("&System information that goes along", "&Systemangaben, die mitgeschickt werden"),
        ["report_send"] = ("&Send", "&Senden"),
        ["report_collecting"] = ("Looking for logs and crash reports ...", "Suche Logs und Absturzberichte …"),
        ["report_found"] = ("Attachments found: {0}.", "Gefundene Anhänge: {0}."),
        ["report_dumps_found"] = ("Crash dumps among them, not checked yet: {0}.", "Davon Absturz-Speicherabbilder, noch nicht ausgewählt: {0}."),
        ["report_nothing_found"] = ("No logs or crash reports found; the report goes without attachments.",
                                    "Keine Logs oder Absturzberichte gefunden; der Bericht geht ohne Anhänge."),
        ["report_no_game"] = ("The game folder was not found, so the game's logs can't be attached.",
                              "Der Spielordner wurde nicht gefunden, darum können die Logs des Spiels nicht angehängt werden."),
        ["report_collect_failed"] = ("The logs could not be gathered: {0}", "Die Logs konnten nicht gesammelt werden: {0}"),
        ["report_need_description"] = ("Please describe the problem first.", "Bitte beschreibe zuerst das Problem."),
        ["report_bad_email"] = ("That doesn't look like an e-mail address. Correct it or leave the field empty.",
                                "Das sieht nicht nach einer E-Mail-Adresse aus. Korrigiere sie oder lass das Feld leer."),
        ["report_discard"] = ("Close without sending the report?", "Schließen, ohne den Bericht zu senden?"),
        ["report_packing"] = ("Packing the report ...", "Packe den Bericht …"),
        ["report_left_out"] = ("Too big for one report, left out: {0}", "Zu groß für einen Bericht, weggelassen: {0}"),
        ["report_protecting"] = ("Solving the spam protection ...", "Löse den Spamschutz …"),
        ["report_sending"] = ("Sending the report ({0}) ...", "Sende den Bericht ({0}) …"),
        ["report_sent"] = ("The report was sent. Thank you!", "Der Bericht wurde gesendet. Danke!"),
        ["report_altcha_failed"] = ("The spam protection could not be solved: {0}", "Der Spamschutz ließ sich nicht lösen: {0}"),
        ["report_failed_network"] = ("No connection to Static Forms ({0}). Check the internet connection and send again.",
                                     "Keine Verbindung zu Static Forms ({0}). Prüfe die Internetverbindung und sende noch einmal."),
        ["report_failed_rejected"] = ("Static Forms refused the report: {0}", "Static Forms hat den Bericht abgelehnt: {0}"),
        ["report_failed_key"] = ("Static Forms does not accept the form key of this build ({0}). Please report the problem another way.",
                                 "Static Forms nimmt den Formularschlüssel dieses Builds nicht an ({0}). Bitte melde das Problem auf anderem Weg."),
        ["report_failed_forbidden"] = ("Static Forms turned the report away: {0}. This can be the spam protection; send again.",
                                       "Static Forms hat den Bericht abgewiesen: {0}. Das kann am Spamschutz liegen; sende noch einmal."),
        ["report_failed_too_large"] = ("The report is too big. Check fewer attachments and send again.",
                                       "Der Bericht ist zu groß. Wähle weniger Anhänge aus und sende noch einmal."),
        ["report_failed_rate"] = ("Too many reports were sent just now. Please try again in a few minutes.",
                                  "Gerade wurden zu viele Berichte gesendet. Bitte versuche es in ein paar Minuten noch einmal."),
        ["report_failed_server"] = ("Static Forms reported an error ({0}: {1}). Please try again later.",
                                    "Static Forms hat einen Fehler gemeldet ({0}: {1}). Bitte versuche es später noch einmal."),
        ["report_item_bepinex"] = ("Log of the current or last game session: BepInEx and Wingspan Access ({0}, last written {1})",
                                   "Log der laufenden oder letzten Spielsitzung: BepInEx und Wingspan Access ({0}, zuletzt geschrieben {1})"),
        ["report_item_session"] = ("Mod log of an earlier game session ({0}, until {1})", "Mod-Log einer früheren Spielsitzung ({0}, bis {1})"),
        ["report_item_session_from"] = ("Mod log of the game session from {1} to {2} ({0})", "Mod-Log der Spielsitzung von {1} bis {2} ({0})"),
        ["report_item_unity"] = ("Unity log of the current or last game session ({0}, last written {1})",
                                 "Unity-Log der laufenden oder letzten Spielsitzung ({0}, zuletzt geschrieben {1})"),
        ["report_item_unity_prev"] = ("Unity log of the game session before ({0}, last written {1})",
                                      "Unity-Log der Spielsitzung davor ({0}, zuletzt geschrieben {1})"),
        ["report_item_installer"] = ("Log of the installer's last runs ({0})", "Log der letzten Installer-Läufe ({0})"),
        ["report_item_crash"] = ("Crash on {0}: crash dump ({1})", "Absturz am {0}: Speicherabbild ({1})"),
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
