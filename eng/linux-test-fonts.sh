#!/usr/bin/env bash
# LibreWPF's portable backend defaults every WPF element to the "Arial" font family
# (SystemParameters.CreateDefaultNonClientMetrics), and its composite-font fallback
# currently fail-fasts on Linux when no family by that name exists. Real Arial isn't
# freely installable, so clone the metric-compatible Liberation Sans into ~/.fonts
# with its family name rewritten to "Arial".
#
# Requires: python3 + fontTools (pip install fonttools), fonts-liberation2 (or
# fonts-liberation) installed.
set -euo pipefail

python3 - <<'EOF'
import os
from fontTools.ttLib import TTFont

candidates = [
    "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
    "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
]
src = next((p for p in candidates if os.path.exists(p)), None)
if src is None:
    raise SystemExit("Liberation Sans not found - install fonts-liberation2 first")

outdir = os.path.expanduser("~/.fonts")
os.makedirs(outdir, exist_ok=True)

for suffix, style in [("Regular", "Regular"), ("Bold", "Bold"), ("Italic", "Italic"), ("BoldItalic", "Bold Italic")]:
    path = src.replace("Regular", suffix)
    if not os.path.exists(path):
        continue
    font = TTFont(path)
    for rec in font["name"].names:
        if rec.nameID in (1, 16):
            rec.string = "Arial"
        elif rec.nameID in (3, 4):
            rec.string = "Arial" if style == "Regular" else f"Arial {style}"
        elif rec.nameID == 6:
            rec.string = f"Arial-{suffix}"
    out = os.path.join(outdir, f"Arial-{suffix}.ttf")
    font.save(out)
    print(f"wrote {out}")
EOF
