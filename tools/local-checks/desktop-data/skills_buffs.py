#!/usr/bin/env python3
"""Decodes skills (skill_eng.bmd / Skill.bmd) and buffs (BuffEffect_eng.bmd)."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from bmd_common import cstr, find_file, i32, u16, u32, write_csv, xor3


def decode_skills(data_dir: Path) -> list[dict[str, object]]:
    """Decodes up to 650 skill records (88-byte S6 or 108/112-byte S9 layout)."""
    path = find_file(data_dir, "Local/Eng/skill_eng.bmd", "Local/Skill.bmd")
    if path is None:
        return []
    raw = path.read_bytes()
    stride = 0
    for extra in (4, 0):
        body = raw[:len(raw) - extra]
        if len(body) % 650 == 0 and (len(body) // 650) in (88, 108, 112):
            stride = len(body) // 650
            break
    if stride == 0:
        raise ValueError(f"Unsupported skill BMD size {len(raw)} in {path.name}")

    rows: list[dict[str, object]] = []
    for sid in range(650):
        r = xor3(raw[sid * stride:(sid + 1) * stride])
        name = cstr(r[:32])
        level, damage, mana, ag = u16(r, 32), u16(r, 34), u16(r, 36), u16(r, 38)
        distance = u32(r, 40)
        delay_ms = i32(r, 44)
        energy = i32(r, 48)
        charisma = u16(r, 52)
        mastery_type = r[54]
        use_type = r[55]
        brand = u32(r, 56)
        kill_count = r[60]
        duty = [r[61], r[62], r[63]]
        classes = [r[64 + i] for i in range(7)]
        rank = r[71]
        icon = u16(r, 72)
        skill_type = r[74]
        req_str = i32(r, 76)
        req_agi = i32(r, 80)
        item_skill = r[84]
        is_damage = r[85]
        effect = u16(r, 86)
        if not name and mana == 0 and ag == 0 and delay_ms == 0 and damage == 0 and not any(classes):
            continue
        rows.append({
            "source": path.name,
            "record": sid,
            "id": sid,
            "name": name,
            "level": level,
            "damage": damage,
            "mana": mana,
            "ag": ag,
            "distance": distance,
            "delayMs": delay_ms,
            "reqEnergy": energy,
            "reqLeadership": charisma,
            "reqStrength": req_str,
            "reqAgility": req_agi,
            "masteryType": mastery_type,
            "useType": use_type,
            "brand": brand,
            "killCount": kill_count,
            "duty0": duty[0],
            "duty1": duty[1],
            "duty2": duty[2],
            "clsDW": classes[0],
            "clsDK": classes[1],
            "clsFE": classes[2],
            "clsMG": classes[3],
            "clsDL": classes[4],
            "clsSUM": classes[5],
            "clsRF": classes[6],
            "rank": rank,
            "icon": icon,
            "skillType": skill_type,
            "itemSkill": item_skill,
            "isDamage": is_damage,
            "effect": effect,
        })
    return rows


def decode_buffs(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/Eng/BuffEffect_eng.bmd."""
    path = find_file(data_dir, "Local/Eng/BuffEffect_eng.bmd", "Local/BuffEffect.bmd")
    if path is None or path.stat().st_size < 4:
        return []
    raw = path.read_bytes()
    count = u32(raw, 0)
    stride = (len(raw) - 4) // count if (len(raw) - 4) % count == 0 else (len(raw) - 8) // count
    rows: list[dict[str, object]] = []
    for i in range(count):
        off = 4 + i * stride
        if off + stride > len(raw):
            break
        r = xor3(raw[off:off + stride])
        buff_id = u16(r, 0)
        group = r[2]
        item_group = r[3]
        item_number = r[4]
        name = cstr(r[5:58])
        desc = cstr(r[58:stride]) if stride > 58 else ""
        if buff_id == 0 and not name:
            continue
        rows.append({
            "source": path.name,
            "record": i,
            "id": buff_id,
            "group": group,
            "itemGroup": item_group,
            "itemNumber": item_number,
            "name": name,
            "description": desc,
        })
    return rows


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Decode skill and buff BMDs from a desktop Data folder.")
    parser.add_argument("data_dir", type=Path)
    parser.add_argument("--out", type=Path, default=None)
    args = parser.parse_args(argv)
    skills = decode_skills(args.data_dir)
    buffs = decode_buffs(args.data_dir)
    if args.out:
        n1 = write_csv(args.out / "skills.csv", list(skills[0].keys()) if skills else ["id"], skills)
        n2 = write_csv(args.out / "buffs.csv", list(buffs[0].keys()) if buffs else ["id"], buffs)
        print(f"Wrote {n1} skills and {n2} buffs")
    else:
        print(f"skills={len(skills)}, buffs={len(buffs)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
