"""Builds manifest.csv: ~1,500 open-access paintings of the National Gallery of Art (Washington) for the archive sample data.

Source: NGA open data, published_images.csv (https://github.com/NationalGalleryOfArt/opendata) – only rows with
openaccess = 1 (CC0, public domain) and the primary view of the object. Paintings are recognised from the
image's description; nudes, ceramics, sculpture, photographs, coins and furniture are left out.

Usage (only needed to rebuild the list; the manifest is committed):
    python build_manifest.py <path to published_images.csv>

Output columns: uuid, theme, title, width, height, object_id, image_url, page_url, description
"""
import csv
import os
import random
import re
import sys

SOURCE = sys.argv[1]
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "manifest.csv")
csv.field_size_limit(10 ** 8)

# Every qualifying painting is taken (~1,500); theme recognised from the description; first matching theme wins
THEMES = [
    ("landscape", 9999, r"landscape|mountain|valley|river|field|forest|countryside|hill|meadow|lake"),
    ("seascape", 9999, r"\bsea\b|ocean|harbor|harbour|ship|boat|coast|beach|shore|waves"),
    ("city", 9999, r"city|street|town|village|building|bridge|church|square|canal"),
    ("animals", 9999, r"horse|dog|cat|bird|cow|sheep|animal|deer|lion|fish"),
    ("portrait", 9999, r"portrait|\bman\b|\bwoman\b|\bgirl\b|\bboy\b|child|lady|gentleman"),
    ("still life", 9999, r"still life|flowers|fruit|vase of|bouquet|table with|basket"),
    ("abstract", 9999, r"abstract|geometric|shapes|colou?r fields?|lines"),
]
EXCLUDE = (r"\b(jug|vase\b|sculpture|statue|photograph|coin|medal|textile|chair|bowl|nude|naked|crucifixion|corpse|dead\b|blood"
           # Index of American Design: watercolour renderings of objects, not paintings of a scene
           r"|weather ?vane|sign\b|chest\b|doorway|cabinet|quilt|carving|carved|figurehead|toy\b|doll|costume|dress\b|shoe|lamp\b"
           r"|clock|box\b|tool\b|wooden|embroider|pattern\b|ceramic|pottery|glass\b|silver|pewter|rendering|object\b|artifact|decoy"
           r"|sketch|greeting|border|wreath|marionette|puppet|carriage|stencil|label\b|lettering|text reading"
           r"|jewel|jewelry|ornate|decorative|ornament|design\b|motif|emblem|badge|plate\b|spoon|teapot|pitcher|coverlet|rug\b)\b"
           r"|this is a drawing|the drawing")


def title_from(description):
    """Short painting title from NGA's description: "This is a painting of a busy dock in the rain…" → "A Busy Dock in the Rain"."""
    d = re.sub(r"\s+", " ", description or "").strip()
    m = (re.search(r"\b(?:painting|watercolou?r|scene|image) (?:of|depicting|shows|showing) (.+?)(?:[,.;]| with | in which | that | where |$)", d, re.I)
         or re.search(r"\b(?:shows|depicts|features) (.+?)(?:[,.;]| with | in which | that | where |$)", d, re.I))
    if not m:
        return None
    words = []
    for w in m.group(1).split():
        # stop before a participle / connector so the title is a whole phrase ("A Grand City Square")
        if w.lower() in {"filled", "featuring", "set", "surrounded", "showing", "depicting", "against", "under", "beneath",
                         "positioned", "framed", "seen", "viewed", "where", "which", "who", "while", "as", "is", "are", "near", "dressed"}:
            break
        words.append(w)
        if len(words) == 7:
            break
    small = {"a", "an", "the", "of", "in", "on", "at", "and", "with", "by", "to", "for", "from"}
    # never end on a connector ("…Holding Hands and", "…Leading Down to a") nor start with one ("And Clothing")
    while words and words[-1].lower().strip(",.;") in small | {"or", "its", "their", "his", "her", "soft", "down", "up"}:
        words.pop()
    if not words or words[0].lower() in {"and", "or", "of", "with", "to", "in"}:
        return None
    title = " ".join(w if (i and w.lower() in small) else w[:1].upper() + w[1:] for i, w in enumerate(words))
    title = re.sub(r"[^A-Za-z0-9' \-]", "", title).strip()
    return title if 3 <= len(title) <= 60 else None


pools = {name: [] for name, _, _ in THEMES}
with open(SOURCE, encoding="utf-8") as f:
    for r in csv.DictReader(f):
        if r["openaccess"] != "1" or r["viewtype"] != "primary":
            continue
        text = (r["assistivetext"] or "").lower()
        if not re.search(r"\b(painting|oil on|watercolou?r)\b", text) or re.search(EXCLUDE, text):
            continue
        if min(int(r["width"] or 0), int(r["height"] or 0)) < 800:
            continue
        title = title_from(r["assistivetext"])
        if not title:
            continue
        theme = next((n for n, _, p in THEMES if re.search(p, text)), None)
        if theme:
            pools[theme].append((r, title))

print("pools", {n: len(v) for n, v in pools.items()})
random.seed(20261113)  # stable selection
rows = []
for name, quota, _ in THEMES:
    pick = random.sample(pools[name], min(quota, len(pools[name])))
    for r, title in pick:
        rows.append({
            "uuid": r["uuid"], "theme": name, "title": title,
            "width": r["width"], "height": r["height"], "object_id": r["depictstmsobjectid"],
            "image_url": r["iiifurl"] + "/full/!1200,1200/0/default.jpg",
            "page_url": f"https://www.nga.gov/collection/art-object-page.{r['depictstmsobjectid']}.html",
            "description": re.sub(r"\s+", " ", r["assistivetext"])[:400],
        })
rows.sort(key=lambda x: (x["theme"], x["uuid"]))
# Repeated titles get a number: "A Landscape", "A Landscape II", "A Landscape III"…
seen = {}
roman = ["", " II", " III", " IV", " V", " VI", " VII", " VIII", " IX", " X"]
for x in rows:
    n = seen.get(x["title"], 0)
    seen[x["title"]] = n + 1
    if n:
        x["title"] = x["title"] + (roman[n] if n < len(roman) else f" {n + 1}")
with open(OUT, "w", encoding="utf-8", newline="") as f:
    w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
    w.writeheader()
    w.writerows(rows)
print(len(rows), "paintings ->", OUT, {n: len([x for x in rows if x["theme"] == n]) for n, _, _ in THEMES})
