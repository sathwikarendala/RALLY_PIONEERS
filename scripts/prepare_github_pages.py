import gzip
import shutil
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
WEBGL_BUILD = ROOT / "FormulaHorizonWebGL"
ASSETS = {
    "FormulaHorizonWebGL.data.unityweb": "FormulaHorizonWebGL.data",
    "FormulaHorizonWebGL.framework.js.unityweb": "FormulaHorizonWebGL.framework.js",
    "FormulaHorizonWebGL.wasm.unityweb": "FormulaHorizonWebGL.wasm",
}


def prepare_site(destination: Path) -> None:
    if not (WEBGL_BUILD / "index.html").is_file():
        raise FileNotFoundError(f"WebGL index.html not found in {WEBGL_BUILD}")

    destination.mkdir(parents=True, exist_ok=True)
    shutil.copytree(WEBGL_BUILD / "TemplateData", destination / "TemplateData", dirs_exist_ok=True)
    build_output = destination / "Build"
    build_output.mkdir(parents=True, exist_ok=True)
    shutil.copy2(WEBGL_BUILD / "Build" / "FormulaHorizonWebGL.loader.js", build_output / "FormulaHorizonWebGL.loader.js")
    for compressed_name, output_name in ASSETS.items():
        source = WEBGL_BUILD / "Build" / compressed_name
        with gzip.open(source, "rb") as compressed:
            (build_output / output_name).write_bytes(compressed.read())

    html = (WEBGL_BUILD / "index.html").read_text(encoding="utf-8")
    for compressed_name, output_name in ASSETS.items():
        if html.count(compressed_name) != 1:
            raise ValueError(f"Expected exactly one {compressed_name!r} reference in index.html")
        html = html.replace(compressed_name, output_name)

    (destination / "index.html").write_text(html, encoding="utf-8")
    (destination / ".nojekyll").touch()


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: python scripts/prepare_github_pages.py <output-directory>")
    prepare_site(Path(sys.argv[1]).resolve())
