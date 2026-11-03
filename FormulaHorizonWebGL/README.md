# Formula Horizon WebGL build

## Run locally

Do not open `index.html` directly. Browsers block the Unity build files when the page is opened as a `file://` URL, and the compressed `.unityweb` files also need HTTP response headers.

1. Install Python 3 if it is not already installed.
2. Double-click `start_webgl.bat`, or run it from Visual Studio's terminal.
3. Keep the server window open while playing. It opens the game at `http://127.0.0.1:8000/`.
4. Press Ctrl+C in the server window when finished.

## Publish on GitHub Pages

For a public playable link, upload the entire Formula Horizon project folder (the parent folder containing `.github`, `scripts`, and `FormulaHorizonWebGL`) to a GitHub repository. Then select **Settings > Pages > GitHub Actions** in that repository and push to `main` or `master`. The included workflow prepares and deploys this WebGL build. Share the Pages URL shown in the workflow's deployment summary; the repository URL itself is not a playable game link.
