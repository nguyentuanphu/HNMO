#!/usr/bin/env python3
"""Decodes monsters, monster skills, gates, warps, mixes, and maps from a desktop Data folder."""

from __future__ import annotations

import argparse
import re
import struct
import sys
from pathlib import Path

from bmd_common import cstr, find_file, i32, u16, u32, write_csv, xor3


def decode_npc_names(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/Eng/Npcname_eng.bmd (S9) or Local/Eng/Npcname(eng).txt (S6)."""
    bmd = find_file(data_dir, "Local/Eng/Npcname_eng.bmd")
    if bmd is not None:
        raw = bmd.read_bytes()
        stride = 260
        rows: list[dict[str, object]] = []
        for i in range(len(raw) // stride):
            r = xor3(raw[i * stride:(i + 1) * stride])
            mid, val = u16(r, 0), u16(r, 2)
            name = cstr(r[4:])
            if name:
                rows.append({"source": bmd.name, "record": i, "id": mid, "param": val, "name": name})
        return rows

    txt = find_file(data_dir, "Local/Eng/Npcname(eng).txt")
    if txt is not None:
        rows = []
        for ln, line in enumerate(txt.read_text(encoding="cp1252", errors="replace").splitlines(), 1):
            s = line.strip()
            if not s or s.startswith("//") or s == "end":
                continue
            m = re.match(r'^(\d+)\s+(\d+)\s+"([^"]*)"', s)
            if m:
                rows.append({"source": txt.name, "record": ln, "id": int(m.group(1)), "param": int(m.group(2)), "name": m.group(3)})
        return rows
    return []


def decode_monster_skills(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/MonsterSkill.bmd."""
    path = find_file(data_dir, "Local/MonsterSkill.bmd")
    if path is None:
        return []
    raw = path.read_bytes()
    count = u32(raw, 0)
    rows: list[dict[str, object]] = []
    for i in range(count):
        if 4 + (i + 1) * 48 > len(raw):
            break
        r = xor3(raw[4 + i * 48:4 + (i + 1) * 48])
        vals = struct.unpack("<12i", r)
        skills = [s for s in vals[1:11] if s >= 0]
        rows.append({
            "source": path.name,
            "record": i,
            "monsterId": vals[0],
            "skills": ";".join(str(s) for s in skills),
        })
    return rows


def decode_warps(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/Eng/movereq_eng.bmd."""
    path = find_file(data_dir, "Local/Eng/movereq_eng.bmd", "Local/Movereq.bmd")
    if path is None:
        return []
    raw = path.read_bytes()
    count = u32(raw, 0)
    body_len = len(raw) - 4
    rec = 84 if body_len % 84 == 0 else 80
    rows: list[dict[str, object]] = []
    for i in range(count):
        if 4 + (i + 1) * rec > len(raw):
            break
        r = xor3(raw[4 + i * rec:4 + (i + 1) * rec])
        idx = i32(r, 0)
        main_name = cstr(r[4:36])
        sub_name = cstr(r[36:68])
        req_lvl = i32(r, 68)
        max_lvl = i32(r, 72) if rec >= 84 else 400
        zen = i32(r, 76 if rec >= 84 else 72)
        gate = i32(r, 80 if rec >= 84 else 76)
        if idx > 0 or main_name:
            rows.append({
                "source": path.name,
                "record": i,
                "index": idx,
                "mainName": main_name,
                "subName": sub_name,
                "reqLevel": req_lvl,
                "maxLevel": max_lvl,
                "zen": zen,
                "gate": gate,
            })
    return rows


def decode_gates(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/Gate.bmd or Gate.bmd."""
    path = find_file(data_dir, "Local/Gate.bmd", "Gate.bmd")
    if path is None:
        return []
    raw = path.read_bytes()
    rows: list[dict[str, object]] = []
    for i in range(len(raw) // 14):
        r = xor3(raw[i * 14:(i + 1) * 14])
        flag, map_num, x1, y1, x2, y2 = r[0], r[1], r[2], r[3], r[4], r[5]
        target, direction, level = struct.unpack("<HHH", r[6:12])
        if flag or map_num or target or level:
            rows.append({
                "source": path.name,
                "record": i,
                "index": i,
                "flag": flag,
                "map": map_num,
                "x1": x1,
                "y1": y1,
                "x2": x2,
                "y2": y2,
                "target": target,
                "direction": direction,
                "reqLevel": level,
            })
    return rows


def decode_mixes(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/Mix.bmd (656-byte S6 or 660-byte S9 records across 14 mix types)."""
    path = find_file(data_dir, "Local/Mix.bmd", "Local/mix.bmd")
    if path is None or path.stat().st_size < 56:
        return []
    raw = path.read_bytes()
    counts = struct.unpack("<14i", xor3(raw[:56]))
    total = sum(c for c in counts if 0 <= c <= 500)
    rec = 660 if total > 0 and len(raw) == 56 + total * 660 else 656
    rows: list[dict[str, object]] = []
    off = 56
    for mtype, cnt in enumerate(counts):
        if cnt <= 0 or cnt > 500:
            continue
        for i in range(cnt):
            if off + rec > len(raw):
                break
            r = xor3(raw[off:off + rec])
            off += rec
            mix_idx = i32(r, 0)
            mix_id = i32(r, 4)
            req_lvl = i32(r, 44)
            zen = i32(r, 52)
            rate = i32(r, 468)
            rows.append({
                "source": path.name,
                "record": len(rows),
                "mixType": mtype,
                "slotIndex": mix_idx,
                "mixId": mix_id,
                "reqLevel": req_lvl,
                "zen": zen,
                "baseRate": rate,
            })
    return rows


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Decode NPCs, gates, warps, and mixes from a desktop Data folder.")
    parser.add_argument("data_dir", type=Path)
    parser.add_argument("--out", type=Path, default=None)
    args = parser.parse_args(argv)
    npcs = decode_npc_names(args.data_dir)
    mskills = decode_monster_skills(args.data_dir)
    warps = decode_warps(args.data_dir)
    gates = decode_gates(args.data_dir)
    mixes = decode_mixes(args.data_dir)
    if args.out:
        for fname, rows in [
            ("npcs.csv", npcs),
            ("monster_skills.csv", mskills),
            ("warps.csv", warps),
            ("gates.csv", gates),
            ("mixes.csv", mixes),
        ]:
            if rows:
                write_csv(args.out / fname, list(rows[0].keys()), rows)
    print(f"npcs={len(npcs)} monsterSkills={len(mskills)} warps={len(warps)} gates={len(gates)} mixes={len(mixes)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
