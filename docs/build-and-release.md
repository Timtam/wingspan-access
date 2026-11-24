# Build, release and installer

Notes for working on the build, the installer and the release workflows. User-facing instructions
are in the README.

## Building without the game

- The mod compiles against the game's IL2CPP interop assemblies, which BepInEx generates on the
  first start. The CI has no game, so `lib/interop` holds reference assemblies of the ten
  assemblies the mod uses: ACTk.Runtime, I2Loc, Il2Cppmscorlib, Rewired_Core, ScriptAssembly,
  Unity.TextMeshPro, UnityEngine.CoreModule, UnityEngine.InputLegacyModule, UnityEngine.UI and
  UnityEngine.UIModule. They contain signatures only, no method bodies (about 9.5 MB).
- `tools/make-reference-assemblies.ps1` makes them with JetBrains Refasmer (a local dotnet tool,
  see `.config/dotnet-tools.json`). It copies the DLLs first, because the running game locks them.
  Refasmer fails on some Unity modules with `--omit-non-api-members=true` ("Read out of bounds");
  the script retries those with `false`. If the mod starts using another game assembly, add it to
  the script's list.
- The reference CoreModule lacks the compiler's nullable attributes, so
  `src/WingspanAccess/Compat/NullableAttributes.cs` declares them.
- The BepInEx core DLLs come from the pinned be.793 download (`tools/fetch-bepinex.ps1`, checked
  against its SHA-256) in `third_party/bepinex`. The release ZIP ships the same files.
- `WingspanAccess.csproj` uses the game folder when `BepInEx\interop\ScriptAssembly.dll` exists
  there; `-p:UseGameReferences=false` forces the CI references.
- `Directory.Build.props` holds the version (`ModVersion`) of the mod and the installer and
  generates a `BuildInfo` class with `Version`, `RepositorySlug` and `ProblemReports`. The CI passes
  `-p:RepositorySlug=<owner/name>`. Local builds have no slug, so they do not check for updates.
  `ProblemReports` is true when the build has the Static Forms key (see "Problem reports").

## Prism

Prism releases have no 32-bit build. `tools/build-prism-x86.cmd` builds v0.18.3 with the Visual
Studio x86 tools, CMake and Ninja. The workflows cache `third_party/prism/x86`, keyed on that
script, so it is built only when the script changes.

## Release packages

`tools/build-release.ps1` downloads BepInEx, builds Prism if needed, builds the mod (always against
`lib/interop`) and the installer, builds and runs the installer's tests
(`installer/WingspanAccessInstaller.Tests`), and runs `tools/package.ps1`, which writes:

- `dist/WingspanAccess-<version>.zip`: `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`,
  `dotnet/` and `BepInEx/core/` from BepInEx (without its changelog), `BepInEx/config/BepInEx.cfg`
  from `packaging/`, and `BepInEx/plugins/WingspanAccess/` with the mod, `prism.dll` (checked to be
  x86), the installer, `README.txt`, `LICENSE.txt` (the project's GPL, from `LICENSE`),
  `THIRD-PARTY-NOTICES.txt` and the license texts of BepInEx and Prism (downloaded once to
  `third_party/licenses`).
- `dist/WingspanAccessInstaller.exe`, always with this name, so that
  `releases/latest/download/WingspanAccessInstaller.exe` is the newest installer.
- `dist/SHA256SUMS.txt`.

`tools/release-prepare.ps1` sets the version and rolls the changelog (see "Releasing" in the
README). `-Root` lets it work on a copy, which is how it was tested.

## Installer

.NET Framework 4.8 and WinForms, one EXE of about 75 KB, no administrator rights: Steam libraries
are writable for the user, and the uninstall entry is per user (HKCU). Only standard controls. The
window has the dialog role, so screen readers read its texts when it opens. Progress goes to a
label that is a UIA live region. Questions and results are message boxes.

Game folder: `--game`, else the folder of the last installation (registry value `WingspanFolder` of
the uninstall entry), else the Steam libraries (registry, `libraryfolders.vdf`,
`appmanifest_1054490.acf`), else the user selects `Wingspan.exe`.

Installing:

1. The package is the `--zip` file, else the newer of the latest release's ZIP (downloaded) and a
   `WingspanAccess-*.zip` next to the installer.
2. BepInEx is copied only if it is missing or incomplete. Another BepInEx version is replaced only
   after asking; then `BepInEx/core`, `BepInEx/interop`, `BepInEx/cache` and `dotnet` are deleted
   first.
3. The plugin folder is replaced.
4. `BepInEx.cfg` gets `[Logging.Console] Enabled = false`, `[Logging.Disk] WriteUnityLog = true`
   and `InstantFlushing = true`. Everything else stays as it is.
5. The installer copies itself into the plugin folder (unless the package has a newer one) and
   writes the uninstall entry.

Uninstalling removes the plugin folder and, if asked, `BepInEx/`, `dotnet/`, `winhttp.dll`,
`doorstop_config.ini`, `.doorstop_version`, and `changelog.txt` if it is BepInEx's.

The copy in the plugin folder cannot replace or delete itself. It copies itself to
`%TEMP%\WingspanAccessInstaller\` and starts that copy with the same arguments plus `--relaunched`.
If the latest release has a newer installer, the installer downloads it and hands over
(`--no-self-update` prevents loops). The game counts as running only if a `Wingspan.exe` from the
chosen folder runs.

Updating from the game: F5 twice makes the mod (`src/WingspanAccess/Updates/UpdateChecker.cs`)
start `WingspanAccessInstaller.exe --update --restart --game <folder> --lang <code>` and quit the
game. The installer downloads the release, waits until Wingspan has exited (the *Close Wingspan*
button helps), installs, and starts the game with `steam://rungameid/1054490`. If something fails,
it asks whether to start the game anyway.

Command line:

| Option | Meaning |
| --- | --- |
| `--game <folder>` | The Wingspan folder |
| `--zip <file>` | Install this package instead of the release download |
| `--lang de\|en` | Language of the installer (default: Windows display language) |
| `--update [--restart]` | Update window as started by the mod; `--restart` starts the game afterwards |
| `--uninstall` | Uninstall with two questions (mod, BepInEx), used by Installed apps |
| `--report` | The *Report a problem* window (the mod starts it with F4 twice, the Start menu entry too) |
| `--silent --install` / `--uninstall` / `--detect` / `--check` | Without a window, output on stdout |
| `--remove-bepinex`, `--replace-bepinex` | Answers for `--silent` |
| `--no-registry` | No uninstall entry (tests) |
| `--repo <owner/name>` | Another repository for the release check |

Silent exit codes: 0 done, 1 failed, 2 wrong arguments or no game folder, 3 the game is running.
A silent run of the copy in the plugin folder returns at once, because the copy in `%TEMP%` does
the work. The log is `%TEMP%\WingspanAccessInstaller.log`.

Testing without the game: make a folder with an empty `Wingspan.exe` and run
`--silent --install --game <folder> --zip dist\WingspanAccess-<version>.zip --no-registry`, then
`--detect`, `--uninstall` and `--uninstall --remove-bepinex`. The window itself (focus, live region
announcements with NVDA, message boxes) needs a person with a screen reader to test it.

## Problem reports

Built like the report function of OP1 Access (github.com/Timtam/op1-access).

The installer's Static Forms key comes from the environment variable `WA_STATICFORMS_KEY` at build
time, never from a file of the repository: set it on your own computer (user environment
variables) to build with it; the release and CI workflows set it from the repository secret
`STATICFORMS_KEY` (pull requests from forks get no secrets, so their builds have reports off).
`WingspanAccessInstaller.csproj` writes it, scrambled, into `obj\...\ReportKey.g.cs` (ignored by
git with all of `obj`); the build log only says "Problem reports: form key built in" or "no form
key". Without it the report function is off: the installer's button is greyed out with a line
saying why, `--report` says so in a message box, and the mod's `BuildInfo.ProblemReports` is
false, so F4 says so too.

Tests: `tools/build-release.ps1` builds and runs `installer\WingspanAccessInstaller.Tests` (a
console program: the ALTCHA solver, the Static Forms client against a stand-in server on
127.0.0.1, finding, cleaning and packing of the attachments, the windows built but not shown).
Run in the same environment as the build, its key test checks that the built-in key matches
`WA_STATICFORMS_KEY` and is not readable as plain text in the EXE (it never prints the key).

The report: `--report` (F4 twice in the game; the installer's *Report a problem* button; the Start
menu entry, written on installation by builds with the key) opens `ReportForm`: a description, an
optional e-mail address (sent as `replyTo`), a checked list of attachments, a note on privacy, the
system information that goes along (versions of the mod, the installer, BepInEx and the game,
Windows, languages, the speech output from the log) and *Send*. Standard controls; progress in a
live region.

- Attachments (`ProblemReport.Collect`): `BepInEx\LogOutput.log` (readable while the game runs:
  `FileShare.ReadWrite`), the mod's `logs\session-1..3.log` without the one of the same session as
  `LogOutput.log` (both have the line "Session log started <time>"), Unity's `Player.log` and
  `Player-prev.log` from `AppData\LocalLow\Monster Couch\Wingspan`,
  `%TEMP%\WingspanAccessInstaller.log` (a report run adds to it instead of starting it over), and
  Unity's crash reports (`%TEMP%\Monster Couch\Wingspan\Crashes\Crash_*\crash.dmp` with
  `error.log` and `Player.log`) newer than `logs\last-report.txt` (else of the last 14 days), the
  newest three, not checked at first.
- Session logs (`src/WingspanAccess/Diagnostics/SessionLog.cs`): at the start of every game the
  mod moves `session-1` to `-2` and `-2` to `-3`, starts `session-1.log` with the line "Wingspan
  Access session log, started <time>" and what BepInEx has logged so far, then adds every further
  line (BepInEx log listener, written at once). The installer keeps the `logs` folder on updates.
- Cleaning (`Scrubber`): profile paths `X:\Users\<name>` in every spelling, the user name and the
  computer name as words, Steam IDs (17 digits starting with 7656119; Steam's crash handler writes
  them into `BepInEx\ErrorLog.log`, which is not attached). Dumps can't be cleaned. The names of
  other players in online games stay; the window says so.
- Packing (`ProblemReport.Build`): `report.txt` (everything of the report) and the attachments in
  one ZIP of at most 4,000,000 bytes (Static Forms takes 4.5 MB per submission). A long log keeps
  its first 128 KB and as much of its end as fits (from 6 MB down to 160 KB, with a note where
  lines are missing); then the oldest attachments are left out, and the report says which.
- Sending (`StaticFormsClient`): the challenge from
  `https://www.staticforms.dev/api/altcha/challenge?apiKey=<key>`, solved by `Altcha` (classic
  ALTCHA: the number n up to `maxnumber` with hex(SHA-256(salt + n)) = `challenge`, on all cores),
  then `multipart/form-data` to `https://api.staticforms.dev/submit` with `apiKey`, `subject`,
  `message`, `replyTo`, `system`, `attachments`, `altchaToken` and the ZIP as `report`, with
  `Accept: application/json`. Errors come as `{"error": ...}`: 400 refused, 401 wrong key, 403 spam
  protection or domain restriction (keep the form's domain restriction off: a desktop program sends
  no Origin), 413 too large, 429 too many. A form without ALTCHA gets the report without a token.
- After sending, `logs\last-report.txt` holds the time, so the next report offers only newer
  crashes.

The mod's side: F4 twice (`src/WingspanAccess/Diagnostics/ProblemReportKey.cs`) starts
`WingspanAccessInstaller.exe --report --game <folder> --lang <code>` from the plugin folder, and the
game keeps running. A real report needs the key and lands in the developer's mailbox: send one only
on purpose. The tests cover the client without it; the window itself (focus, live region, message
boxes) needs a person with a screen reader.
