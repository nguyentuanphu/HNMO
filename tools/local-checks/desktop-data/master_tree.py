#!/usr/bin/env python3
"""Prints and exports the desktop client's master skill tree, tooltips, and skill names.

Usage:
  master_tree.py <Data folder> [skill numbers...]

<Data folder> is a desktop client's Data folder (client/src/bin/Data, or a season's data pack). It reads
Local/MasterSkillTreeData.bmd (512 records of 24 bytes, XOR-coded, then a checksum),
Local/Eng/MasterSkillTooltip_eng.bmd (512 records of 808 bytes), and Local/Skill.bmd or
Local/Eng/skill_eng.bmd (650 records of 88, 108, or 112 bytes).
"""

from __future__ import annotations

import struct
import sys
from pathlib import Path

from bmd_common import cstr, f32, find_file, i32, u16, xor3


def decode(record: bytes) -> bytes:
    return xor3(record)


def master_rows(data: Path):
    path = find_file(data, "Local/MasterSkillTreeData.bmd")
    if path is None:
        return
    raw = path.read_bytes()[:-4]
    for i in range(len(raw) // 24):
        r = decode(raw[i * 24:(i + 1) * 24])
        index, class_code = struct.unpack_from("<HH", r, 0)
        if index == 0:
            continue
        group, points, max_level, arrow = r[4], r[5], r[6], r[7]
        req1, req2, skill = struct.unpack_from("<III", r, 8)
        value = f32(r, 20)
        yield {
            "record": i,
            "index": index,
            "classCode": class_code,
            "group": group,
            "requiredPoints": points,
            "maxLevel": max_level,
            "arrow": arrow,
            "requires": [x for x in (req1, req2) if x],
            "skill": skill,
            "value": round(value, 4),
        }


def master_tooltips(data: Path) -> list[dict[str, object]]:
    path = find_file(data, "Local/Eng/MasterSkillTooltip_eng.bmd")
    if path is None:
        return []
    raw = path.read_bytes()[:-4]
    rec_size = 808
    rows: list[dict[str, object]] = []
    for i in range(len(raw) // rec_size):
        r = decode(raw[i * rec_size:(i + 1) * rec_size])
        skill_id = i32(r, 0)
        class_code = u16(r, 4)
        if skill_id <= 0:
            continue
        info1 = cstr(r[8:72])
        info2 = cstr(r[72:328])
        info3 = cstr(r[328:360])
        info4 = cstr(r[360:424])
        info5 = cstr(r[424:488])
        info6 = cstr(r[488:552])
        info7 = cstr(r[552:616])
        rows.append({
            "record": i,
            "skillNumber": skill_id,
            "classCode": class_code,
            "info1": info1,
            "info2": info2,
            "info3": info3,
            "info4": info4,
            "info5": info5,
            "info6": info6,
            "info7": info7,
        })
    return rows


def skill_names(data: Path) -> dict[int, str]:
    path = find_file(data, "Local/Eng/skill_eng.bmd", "Local/Skill.bmd")
    if path is None:
        return {}
    raw = path.read_bytes()
    for extra in (4, 0):
        body = raw[:len(raw) - extra]
        if len(body) % 650 == 0:
            size = len(body) // 650
            return {
                i: cstr(decode(body[i * size:(i + 1) * size])[:32])
                for i in range(650)
            }
    return {}


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    data = Path(sys.argv[1])
    wanted = {int(x) for x in sys.argv[2:]}
    names = skill_names(data)
    rows = list(master_rows(data))
    tooltips = {r["skillNumber"]: r for r in master_tooltips(data)}
    print(f"{len(rows)} master tree rows; skills {min(r['skill'] for r in rows)}-{max(r['skill'] for r in rows)}; {len(tooltips)} tooltip rows")
    for row in rows:
        if not wanted or row["skill"] in wanted:
            tt = tooltips.get(row["skill"])
            print({**row, "name": names.get(row["skill"], ""), "tooltip": tt["info2"] if tt else ""})
    for number in sorted(wanted):
        if all(row["skill"] != number for row in rows):
            print(f"{number}: no master tree row; skill name: {names.get(number, '') or '(empty record)'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
