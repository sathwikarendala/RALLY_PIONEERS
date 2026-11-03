# Formula Horizon — updated Unity source

Start with [START_HERE.md](START_HERE.md) for Unity Hub setup, opening the game and troubleshooting.
Target editor: **Unity 2021.3.45f2 LTS**, Built-in rendering, legacy Input Manager.
Open **Assets/Scenes/RallyMain.unity**, press Play and Start Race.

The current game has Desert Horizon and Forest Run, five AI rivals, three laps, Formula-style cars, reverse, grass run-off, perimeter fences, animated spectators, steering buttons, driving pedals, a speed dial and minimap.
Blue nitrogen and yellow boost are removed entirely from gameplay.

Documentation/ contains historical reference material from the original project, not instructions or feature promises for this procedural edition.
Unity editor compilation and play remain unverified here; see START_HERE.md for the tested scope.

## Play in a browser

The Unity WebGL export is in `FormulaHorizonWebGL/`. To publish it as a playable GitHub Pages site:

1. Upload this entire project folder to a GitHub repository, including `.github/workflows/deploy-pages.yml` and the `FormulaHorizonWebGL/Build/` files.
2. In the repository, open **Settings > Pages** and set the build and deployment source to **GitHub Actions**.
3. Push to the `main` or `master` branch, or run **Deploy Formula Horizon to GitHub Pages** from the **Actions** tab.
4. Once the workflow succeeds, open the Pages URL shown in the workflow's deployment summary. It has the form `https://<your-github-username>.github.io/<repository-name>/`.

Share that Pages URL to let people play. The GitHub repository URL shows the project files; it does not run the game. The workflow prepares the Unity export for GitHub Pages automatically.

## Images
- `Documentation/images/detect.gif` — AI rivals: 6-car grid, live race positions and overtakes.
- `Documentation/images/friction.gif` — Cornering grip, braking and racing lines on the circuit.
- `Documentation/images/main_screen.png` — Formula Horizon main menu (track select, start race, controls).
