Wingspan Access {VERSION}
====================

A screen reader accessibility mod for the Steam version of Wingspan.
Instructions, keys and updates: {PROJECT_URL}

Files in this folder
- WingspanAccess.dll: the mod.
- prism.dll: speech and braille output through your screen reader (Prism).
- WingspanAccessInstaller.exe: installs, updates and removes the mod. The mod starts it when you
  press F5 twice to install an update.
- LICENSE.txt: the license of Wingspan Access, the GNU General Public License.
- THIRD-PARTY-NOTICES.txt and the other LICENSE files: licenses of the software that comes with
  the mod.

Manual installation
Extract WingspanAccess-{VERSION}.zip into the Wingspan folder, the one with Wingspan.exe. In Steam,
open the context menu of Wingspan and choose Manage, Browse local files to find it. If BepInEx 6
is installed already, extracting the folder BepInEx\plugins\WingspanAccess is enough.
The first start takes 1 to 3 minutes while BepInEx prepares the game. When the mod is ready, you
hear "Wingspan Access loaded. F1 for help."

Updates
The mod tells you when a new version is out. Press F5 twice to install it: the game closes, the
installer updates the mod and starts the game again.

Uninstalling
Use "Wingspan Access" in Installed apps, or start WingspanAccessInstaller.exe and choose Uninstall.
By hand: delete the folder BepInEx\plugins\WingspanAccess. To remove BepInEx as well, delete
winhttp.dll, doorstop_config.ini, .doorstop_version and the folders BepInEx and dotnet from the
game folder.

License
Copyright (C) 2026 Toni Barth. Wingspan Access is free software under the GNU General Public
License, version 3 or (at your option) any later version, without any warranty; see LICENSE.txt.
The source code is at {PROJECT_URL}.
