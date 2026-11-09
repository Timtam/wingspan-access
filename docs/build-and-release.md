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
  generates a `BuildInfo` class with `Version` and `RepositorySlug`. The CI passes
  `-p:RepositorySlug=<owner/name>`. Local builds have no slug, so they do not check for updates.

## Prism

Prism releases have no 32-bit build. `tools/build-prism-x86.cmd` builds v0.18.3 with the Visual
Studio x86 tools, CMake and Ninja. The workflows cache `third_party/prism/x86`, keyed on that
script, so it is built only when the script changes.

## Release packages

`tools/build-release.ps1` downloads BepInEx, builds Prism if needed, builds the mod (always against
`lib/interop`) and the installer, and runs `tools/package.ps1`, which writes:

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
