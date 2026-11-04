# Rally Pioneers — Unity 2021.3 LTS

This download is a complete Unity source project, not an executable or a Unity package.
Both nitrogen and boost have been removed: no power buttons, arches, pickups, speed surges or exhaust flames.

## Open the UPDATED project in Unity Hub
1. Download the new ZIP from the preview page. Extract ALL files into a NEW folder; do not overwrite the old Procedural_Update folder.
2. In Unity Hub > Installs, install Unity 2021.3.45f2. If it is not listed, use the official Unity download archive: https://unity.com/releases/editor/whats-new/2021.3.45f2 (Install in Unity Hub).
3. In Hub > Projects > Add > Add project from disk, select the inner RallyPioneersRacing folder. It must directly contain Assets, Packages and ProjectSettings. Do not select the ZIP, Assets alone, or the outer extraction folder.
4. In the project's Editor version column, choose 2021.3.45f2. If no editor version is assigned, install that editor first and select it; a listing in Hub alone does not open the game.
5. Click the newly added project and allow the initial import to complete.
6. Choose Rally Pioneers > Open Game Scene, or open Assets/Scenes/RallyMain.unity.
7. Press Play, click the Game tab, then Start Race on the desert circuit. The scene looks empty before Play because the track, cars and crowd are generated at runtime.

## If the project will not open
- 'Editor not installed' or an empty Editor version: install and assign 2021.3.45f2 in Hub.
- 'Invalid project': select the inner folder containing Assets, Packages and ProjectSettings after fully extracting.
- Existing old Hub entry: add this new extraction separately; the old entry still points to your old files.
- Package import errors: this edition uses only built-in Unity modules, with unused third-party/render-pipeline packages removed. Close Unity, delete only Library and Temp in this NEW extraction if present, and reopen it. Never delete Assets or ProjectSettings.
- An empty Game view: open RallyMain and press Play; do not rename the scene.
- Keep Built-in rendering and legacy Input Manager (Edit > Project Settings > Player > Active Input Handling: Input Manager or Both). Do not switch to Input System-only.
- Unity 6/2022 and older versions were not tested. This archive targets Unity 2021.3 LTS; changing the version text alone does not convert a project.
- For unresolved errors, send the first red Console error and the installed editor version.

## Driving
W / Up / gas pedal: accelerate. S / Down / brake pedal: brake, then reverse.
A / Left / left button: left. D / Right / right button: right.
Space: handbrake. B: recover to track. R: restart. Esc: pause.
One desert circuit, five AI rivals, three laps, safe run-off, perimeter fences, animated crowds, speed dial and minimap.

## Build for Windows
Install Windows Build Support for Unity 2021.3.45f2 in Hub, then use Rally Pioneers > Build Windows Game.
Choose an empty output folder and share the ENTIRE resulting folder, not just the EXE.
The editor helper automatically retains the Standard shader used by the generated car and scenery materials.

## Desert-only game
The game includes only the Desert Horizon — Canyon Rush circuit. The forest environment and track selection have been removed.

## Checks
The WebGL game is rebuilt from the Unity 2021.3.45f2 project after source changes.
