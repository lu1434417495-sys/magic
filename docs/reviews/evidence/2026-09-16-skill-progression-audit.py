"""Read-only content inventory. Run from the repository root; not a validator.

Uses file-local template deep merge (arrays replace) and verified DTO defaults.
Flags are authoring review candidates, never automatic gameplay fixes.
"""
import collections
import hashlib
import json
import pathlib
import sys


def merge(parent, child, root=True):
    result = dict(parent)
    for key, value in child.items():
        if root and key == "template":
            continue
        result[key] = (
            merge(result[key], value, False)
            if isinstance(result.get(key), dict) and isinstance(value, dict)
            else value
        )
    return result


def has_level_fields(value):
    if isinstance(value, dict):
        return any(
            (key in {"level_overrides", "min_skill_level", "max_skill_level"}
             or "per_level" in key or "skill_level_" in key)
            or has_level_fields(child)
            for key, child in value.items()
        )
    return isinstance(value, list) and any(has_level_fields(child) for child in value)


def main():
    rows, fingerprints = [], {}
    for path in sorted(pathlib.Path("data/configs/json/skills").glob("*.json")):
        raw = path.read_bytes()
        fingerprints[path.as_posix()] = hashlib.sha256(raw).hexdigest()
        document = json.loads(raw.decode("utf-8-sig"))
        templates = document.get("templates", {})

        def expand(entry, visited=()):
            name = entry.get("template")
            if name in visited:
                raise ValueError(f"Template cycle: {path}: {name}")
            return merge(expand(templates[name], (*visited, name)) if name else {}, entry)

        for index, authored in enumerate(document["entries"]):
            entry = expand(authored)
            combat = entry.get("combat_profile", {})
            cap = entry.get("max_level", 1)
            non_core = entry.get("non_core_max_level", 0)
            curve = entry.get("mastery_curve", [])
            dynamic = entry.get("dynamic_max_level_stat_id", "")
            source = entry.get("learn_source", "book")
            descriptions = entry.get("level_description_configs", {})
            flags = []
            if cap <= 1:
                flags.append("fixed_or_single_level")
            if cap > 0 and not curve:
                flags.append("empty_mastery_curve")
            if not dynamic and len(curve) != cap:
                flags.append("mastery_curve_length_mismatch")
            if source == "book":
                if not dynamic and (non_core, cap) not in {(3, 5), (5, 7), (7, 9), (9, 10)}:
                    flags.append("outside_standard_static_cap_ladder")
                if not entry.get("growth_tier") or not entry.get("attribute_growth_progress"):
                    flags.append("missing_growth_metadata")
                if (entry.get("skill_type", "active") == "active"
                        and not has_level_fields(combat)):
                    flags.append("no_visible_combat_level_fields_check_special_runtime")
            rows.append({
                "skill_id": entry["skill_id"], "display_name": entry["display_name"],
                "file": path.as_posix(), "pointer": f"/entries/{index}",
                "learn_source": source, "skill_type": entry.get("skill_type", "active"),
                "max_level": cap, "non_core_max_level": non_core,
                "dynamic_max_level_stat_id": dynamic, "mastery_curve": curve,
                "growth_tier": entry.get("growth_tier", ""),
                "attribute_growth_progress": entry.get("attribute_growth_progress", {}),
                "mastery_sources": entry.get("mastery_sources", []),
                "mastery_trigger_mode": combat.get("mastery_trigger_mode", "skill_damage_dice_max") if combat else None,
                "mastery_amount_mode": combat.get("mastery_amount_mode", "per_target_rank") if combat else None,
                "runtime_behavior": entry.get("runtime_behavior", ""),
                "special_resolution_profile_id": combat.get("special_resolution_profile_id", ""),
                "description_levels": list(descriptions),
                "distinct_description_configs": len({json.dumps(v, sort_keys=True) for v in descriptions.values()}),
                "flags": flags,
            })
    report = {
        "basis": "Static inventory of current JSON with file-local templates and verified DTO defaults; not runtime or balance validation.",
        "total": len(rows),
        "max_level_counts": dict(collections.Counter(row["max_level"] for row in rows)),
        "source_counts": dict(collections.Counter(row["learn_source"] for row in rows)),
        "book_flag_counts": dict(collections.Counter(flag for row in rows if row["learn_source"] == "book" for flag in row["flags"])),
        "source_sha256": fingerprints,
        "rows": rows,
    }
    output = pathlib.Path(__file__).with_suffix(".json")
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    sys.stdout.reconfigure(encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k not in {"rows", "source_sha256"}}, ensure_ascii=False, indent=2))
    print(f"Written: {output}")


if __name__ == "__main__":
    main()
