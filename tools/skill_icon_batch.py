"""Inventory and install built-in ImageGen outputs; never calls an image API."""

from __future__ import annotations

import argparse
import hashlib
import html
import json
import re
import shutil
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "docs/content/ui/skill_icons/manifest.json"
CATALOG = ROOT / "data/configs/engine_assets/engine_asset_catalog.tres"
PRIORITY = [
    "basic_meditation", "charge", "bow_training", "mage_arcane_missile",
    "mage_frost_bolt", "mage_fireball", "mage_chain_lightning", "mage_burning_hands",
    "mage_sleep_dust", "mage_blur", "mage_bone_chill", "warrior_toughness",
]
STYLE = (
    "Use case: stylized-concept. Asset type: ONE individual square RPG skill icon. "
    "Match the project's crisp fantasy ability symbols: bold charcoal-black contours, "
    "simple angular faceted shading, silver-gray main materials, restrained bright magic accents. "
    "One clear large centered emblem, 70 percent canvas occupancy, at least 15 percent completely empty padding on ALL FOUR sides; EVERY flame tip, beam tip and particle must end inside this padded area, "
    "readable silhouette at 48 pixels. Square 1024x1024 composition. "
    "The entire background MUST be one perfectly flat solid warm ivory color (#F3F1EB), "
    "including all negative spaces. No checkerboard, transparency grid, texture, scenery, "
    "frame, UI, letters, text, numbers, captions, watermark or photographic detail. "
    "Design the single most recognizable visual metaphor for this exact skill. "
    "Use violet/cyan for arcane power, orange/red for fire, ice-blue for frost, "
    "yellow/cyan for lightning, muted green for poison, and silver/red for martial attacks "
    "when relevant to the skill. Do not add unrelated elements. "
)


def read_manifest():
    return json.loads(MANIFEST.read_text(encoding="utf-8"))


def save_manifest(data):
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def inventory():
    if MANIFEST.exists():
        raise SystemExit("Manifest already exists; use pending/status to resume.")
    entries = []
    total = 0
    existing = []
    for path in sorted((ROOT / "data/configs/json/skills").glob("*.json")):
        document = json.loads(path.read_text(encoding="utf-8"))
        for skill in document["entries"]:
            total += 1
            if skill.get("icon_id"):
                existing.append({"skill_id": skill["skill_id"], "icon_id": skill["icon_id"]})
                continue
            sid = skill["skill_id"]
            subject = f"Skill identifier (metadata only, never draw text): {sid}. Skill name: {skill['display_name']}. Actual gameplay meaning: {skill.get('description', '')}"
            entries.append({
                "skill_id": sid,
                "display_name": skill["display_name"],
                "source": path.relative_to(ROOT).as_posix(),
                "asset_id": sid,
                "asset_path": f"assets/main/battle/skills/{sid}.png",
                "status": "pending",
                "prompt": STYLE + subject,
            })
    entries.sort(key=lambda e: (PRIORITY.index(e["skill_id"]) if e["skill_id"] in PRIORITY else len(PRIORITY), e["source"], e["skill_id"]))
    save_manifest({"schema": 1, "mode": "built-in image_gen", "inventory_date": "2026-09-13", "total_skills": total, "existing_icons": existing, "entries": entries})
    status()


def status():
    data = read_manifest()
    print(json.dumps({"total_skills": data["total_skills"], "existing_icons": len(data["existing_icons"]), "requested": len(data["entries"]), "status": dict(Counter(e["status"] for e in data["entries"]))}, ensure_ascii=False))


def install(sid, source):
    data = read_manifest()
    entry = next(e for e in data["entries"] if e["skill_id"] == sid)
    target = ROOT / entry["asset_path"]
    if target.exists():
        raise SystemExit(f"Refusing to overwrite {target}")
    source_path = Path(source)
    if source_path.read_bytes()[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit("Output is not a PNG")
    shutil.copy2(source_path, target)
    entry["status"] = "generated"
    entry["sha256"] = hashlib.sha256(target.read_bytes()).hexdigest()
    save_manifest(data)
    print(json.dumps({"skill_id": sid, "saved": str(target)}, ensure_ascii=False))


def integrate():
    data = read_manifest()
    entries = [e for e in data["entries"] if e["status"] == "reviewed"]
    if not entries:
        print("No reviewed images to integrate.")
        return
    # Read current files at mutation time, preserving unrelated shared-tree changes.
    catalog = CATALOG.read_text(encoding="utf-8")
    originals = {CATALOG: CATALOG.read_bytes()}
    planned_sources = {}
    for entry in entries:
        sid = entry["skill_id"]
        source = ROOT / entry["source"]
        originals.setdefault(source, source.read_bytes())
        raw = planned_sources.get(source, source.read_text(encoding="utf-8"))
        decoded = json.loads(raw)
        skill = next(e for e in decoded["entries"] if e["skill_id"] == sid)
        if skill.get("icon_id"):
            raise SystemExit(f"Icon changed concurrently: {sid}")
        if f'asset_id = &"{sid}"' in catalog:
            raise SystemExit(f"Asset ID already registered: {sid}")
        # Add one property at the exact top-level skill_id occurrence; no full JSON rewrite.
        pattern = r'(?m)^(\s*)"skill_id": "' + re.escape(sid) + r'",$'
        matches = list(re.finditer(pattern, raw))
        if not matches:
            raise SystemExit(f"Could not locate skill {sid}")
        top = min(matches, key=lambda match: len(match.group(1)))
        if "icon_id" in skill:
            raise SystemExit(f"Explicit empty icon_id needs a targeted edit: {sid}")
        pos = top.end()
        raw = raw[:pos] + '\n' + top.group(1) + f'"icon_id": "{sid}",' + raw[pos:]
        # Verify the insertion changed only this skill's icon metadata.
        updated = json.loads(raw)
        next(e for e in updated["entries"] if e["skill_id"] == sid).pop("icon_id")
        assert updated == decoded
        planned_sources[source] = raw
        ext = f'[ext_resource type="Texture2D" path="res://{entry["asset_path"]}" id="icon_{sid}"]\n'
        sub = f'[sub_resource type="Resource" id="TextureEntry_{sid}"]\nscript = ExtResource("2_texture")\nasset_id = &"{sid}"\ntexture = ExtResource("icon_{sid}")\n\n'
        catalog = catalog.replace('[sub_resource ', ext + '\n[sub_resource ', 1)
        catalog = catalog.replace('[resource]\n', sub + '[resource]\n', 1)
        catalog = re.sub(r'(?m)^(texture_assets = .*?)(\]\))$', lambda m: m[1] + f', SubResource("TextureEntry_{sid}")' + m[2], catalog, count=1)
        entry["status"] = "integrated"
    load_steps = len(re.findall(r'^\[(?:ext_resource|sub_resource) ', catalog, re.M)) + 1
    catalog = re.sub(r'load_steps=\d+', f'load_steps={load_steps}', catalog, count=1)
    if any(path.read_bytes() != original for path, original in originals.items()):
        raise SystemExit("Content changed during preparation; retry integration against current files.")
    for path, content in planned_sources.items():
        path.write_text(content, encoding="utf-8")
    CATALOG.write_text(catalog, encoding="utf-8")
    save_manifest(data)
    status()


def review(ids):
    data = read_manifest()
    for sid in ids:
        entry = next(e for e in data["entries"] if e["skill_id"] == sid)
        assert entry["status"] == "generated", (sid, entry["status"])
        entry["status"] = "reviewed"
    save_manifest(data)
    status()


def gallery():
    data = read_manifest()
    ready = [e for e in data["entries"] if e["status"] != "pending"]
    cards = []
    for entry in ready:
        src = "../../../../" + entry["asset_path"]
        name = html.escape(entry["display_name"])
        sid = html.escape(entry["skill_id"])
        cards.append(f'<article><img class="large" src="{src}" alt="{name}" loading="lazy" decoding="async"><div class="caption"><img class="small" src="{src}" alt="" loading="lazy" decoding="async"><span>{name}<small>{sid}</small></span></div></article>')
    document = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>技能图标制作进度</title><style>
body{margin:0;background:#20242c;color:#f3f1eb;font-family:system-ui,"Microsoft YaHei",sans-serif;padding:32px}h1{font-size:26px;margin:0 0 8px}p{color:#acb7c9}main{display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:24px;margin-top:26px}article{background:#2b313b;padding:10px;border-radius:8px}.large{display:block;width:100%;aspect-ratio:1;object-fit:contain}.caption{display:flex;align-items:center;gap:12px;margin:12px 0 2px}.small{width:48px;height:48px;flex:none}small{display:block;font-size:10px;color:#b5c0d1;margin-top:4px;overflow-wrap:anywhere}input{padding:12px;width:min(440px,90%);background:#2b313b;border:1px solid #5b677a;border-radius:6px;color:inherit}article[hidden]{display:none}
</style><h1>技能图标制作进度</h1>'''
    document += f'<p>已生成 {len(ready)} / {len(data["entries"])} · 下方同时展示原图与实际 48px 缩略图</p><input placeholder="搜索技能名称或 ID" oninput="document.querySelectorAll(\'article\').forEach(e=>e.hidden=!e.textContent.toLowerCase().includes(this.value.toLowerCase()))"><main>'
    document += "".join(cards) + "</main></html>\n"
    target = MANIFEST.with_name("gallery.html")
    target.write_text(document, encoding="utf-8")
    print(target)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["inventory", "pending", "install", "review", "integrate", "status", "gallery"])
    parser.add_argument("args", nargs="*")
    options = parser.parse_args()
    if options.command == "inventory":
        inventory()
    elif options.command == "pending":
        count = int(options.args[0]) if options.args else 12
        print(json.dumps([e for e in read_manifest()["entries"] if e["status"] == "pending"][:count], ensure_ascii=False))
    elif options.command == "install":
        install(*options.args)
    elif options.command == "review":
        review(options.args)
    elif options.command == "integrate":
        integrate()
    elif options.command == "gallery":
        gallery()
    else:
        status()
