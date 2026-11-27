#!/usr/bin/env python3
"""Prints the desktop client's master skill tree and skill names, to compare with the server and the web.

Usage:
  master_tree.py <Data folder> [skill numbers...]

<Data folder> is a desktop client's Data folder (client/src/bin/Data, or a season's data pack). It reads
Local/MasterSkillTreeData.bmd (512 records of 24 bytes, XOR-coded, then a checksum) and Local/Skill.bmd or
Local/Eng/skill_eng.bmd (650 records). The record layout follows client/src/source/UI/HUD/MasterLevel.h:
index (2), class code (2), group (1), required points (1), max level (1), arrow (1), two required skills (4 + 4),
skill (4), value (float 4).

Without skill numbers it prints every row; with them, only those skills (and whether they have a name at all).
"""

import struct
import sys
from pathlib import Path

KEY = bytes([0xFC, 0xCF, 0xAB])


def decode(record: bytes) -> bytes:
    return bytes(b ^ KEY[i % 3] for i, b in enumerate(record))


def master_rows(data: Path):
    raw = (data / "Local" / "MasterSkillTreeData.bmd").read_bytes()[:-4]
    for i in range(len(raw) // 24):
        r = decode(raw[i * 24:(i + 1) * 24])
        index, class_code = struct.unpack_from("<HH", r, 0)
        if index == 0:
            continue
        group, points, max_level, arrow = r[4], r[5], r[6], r[7]
        req1, req2, skill = struct.unpack_from("<III", r, 8)
        value, = struct.unpack_from("<f", r, 20)
        yield {"index": index, "classCode": class_code, "group": group, "requiredPoints": points,
               "maxLevel": max_level, "arrow": arrow, "requires": [x for x in (req1, req2) if x], "skill": skill,
               "value": round(value, 4)}


def skill_names(data: Path):
    for name in ("Local/Eng/skill_eng.bmd", "Local/Skill.bmd"):
        path = data / name
        if not path.exists():
            continue
        raw = path.read_bytes()
        for extra in (4, 0):
            body = raw[:len(raw) - extra]
            if len(body) % 650 == 0:
                size = len(body) // 650
                return {i: decode(body[i * size:(i + 1) * size])[:50].split(b"\0")[0].decode("utf-8", "replace")
                        for i in range(650)}
    return {}


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    data = Path(sys.argv[1])
    wanted = {int(x) for x in sys.argv[2:]}
    names = skill_names(data)
    rows = list(master_rows(data))
    print(f"{len(rows)} master tree rows; skills {min(r['skill'] for r in rows)}-{max(r['skill'] for r in rows)}")
    for row in rows:
        if not wanted or row["skill"] in wanted:
            print({**row, "name": names.get(row["skill"], "")})
    for number in sorted(wanted):
        if all(row["skill"] != number for row in rows):
            print(f"{number}: no master tree row; skill name: {names.get(number, '') or '(empty record)'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
