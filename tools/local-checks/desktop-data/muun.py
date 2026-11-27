#!/usr/bin/env python3
"""Decodes Muun companion tables from a Season 9 desktop client Data folder."""

from __future__ import annotations

import argparse
import struct
import sys
from pathlib import Path

from bmd_common import cstr, find_file, i32, u32, write_csv, xor3
from items_options import decode_items


def decode_muun_info(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/MuunInfo.bmd (76 rows of 56 bytes)."""
    path = find_file(data_dir, "Local/MuunInfo.bmd")
    if path is None:
        return []
    raw = path.read_bytes()
    count = u32(raw, 0)
    names = {(it["group"], it["number"]): it["name"] for it in decode_items(data_dir)}
    rows: list[dict[str, object]] = []
    for i in range(count):
        r = xor3(raw[4 + i * 56:4 + (i + 1) * 56])
        vals = struct.unpack("<14i", r)
        mid, item_idx, flags, opt_idx, add_type, add_val, sy, sm, sd, ey, em, ed, evol_idx, skill_delay = vals
        grp, num = item_idx // 512, item_idx % 512
        mtype, max_rank = flags & 0xFF, flags >> 8
        eg, en = (evol_idx // 512, evol_idx % 512) if evol_idx >= 0 else (-1, -1)
        rows.append({
            "source": path.name,
            "record": i,
            "id": mid,
            "itemIndex": item_idx,
            "group": grp,
            "number": num,
            "name": names.get((grp, num), ""),
            "muunType": mtype,
            "maxRank": max_rank,
            "optionIndex": opt_idx,
            "addOptionType": add_type,
            "addOptionValue": add_val,
            "startDate": f"{sy:04d}-{sm:02d}-{sd:02d}",
            "endDate": f"{ey:04d}-{em:02d}-{ed:02d}",
            "evolutionItemGroup": eg,
            "evolutionItemNumber": en,
            "skillDelayMs": skill_delay,
        })
    return rows


def decode_muun_options(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/Eng/MuunOption_eng.bmd (37 rows of 560 bytes)."""
    path = find_file(data_dir, "Local/Eng/MuunOption_eng.bmd")
    if path is None:
        return []
    raw = path.read_bytes()
    count = u32(raw, 0)
    rows: list[dict[str, object]] = []
    for i in range(count):
        r = xor3(raw[4 + i * 560:4 + (i + 1) * 560])
        idx = i32(r, 0)
        opt_type = r[4]
        name = cstr(r[5:260]).strip()
        evo, lv1, lv2, lv3, lv4, lv5 = struct.unpack("<6i", r[264:288])
        cond_text = cstr(r[289:544]).strip()
        _, cv1, cv2, ctype_raw = struct.unpack("<4i", r[544:560])
        rows.append({
            "source": path.name,
            "record": i,
            "index": idx,
            "optionType": opt_type,
            "valueEvolved": evo,
            "valueLv1": lv1,
            "valueLv2": lv2,
            "valueLv3": lv3,
            "valueLv4": lv4,
            "valueLv5": lv5,
            "conditionType": ctype_raw & 0xFF,
            "conditionVal1": cv1,
            "conditionVal2": cv2,
            "name": name,
            "conditionText": cond_text,
        })
    return rows


def decode_muun_exchange(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/MuunExchange.bmd (required materials and bag rewards)."""
    path = find_file(data_dir, "Local/MuunExchange.bmd")
    if path is None:
        return []
    raw = path.read_bytes()
    c1, c2 = struct.unpack("<2I", raw[:8])
    names = {(it["group"], it["number"]): it["name"] for it in decode_items(data_dir)}
    rows: list[dict[str, object]] = []
    off = 8
    for i in range(c1):
        r = xor3(raw[off + i * 16:off + (i + 1) * 16])
        item_idx, item_cnt, bag_idx, mat_idx = struct.unpack("<4i", r)
        g, n = item_idx // 512, item_idx % 512
        rows.append({
            "source": path.name,
            "table": "requirement",
            "record": i,
            "index": mat_idx,
            "group": g,
            "number": n,
            "itemName": names.get((g, n), ""),
            "count": item_cnt,
            "bagIndex": bag_idx,
            "inGroup": -1,
            "inNumber": -1,
            "inName": "",
        })
    off += c1 * 16
    for j in range(c2):
        r = xor3(raw[off + j * 16:off + (j + 1) * 16])
        bag_idx, in_idx, out_idx, res_type = struct.unpack("<4i", r)
        ig, inum = in_idx // 512, in_idx % 512
        og, onum = out_idx // 512, out_idx % 512
        rows.append({
            "source": path.name,
            "table": "exchange",
            "record": j,
            "index": bag_idx,
            "group": og,
            "number": onum,
            "itemName": names.get((og, onum), ""),
            "count": res_type,
            "bagIndex": bag_idx,
            "inGroup": ig,
            "inNumber": inum,
            "inName": names.get((ig, inum), ""),
        })
    return rows


def decode_muun_tooltip_info(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/MuunTooltipInfo.bmd (82 rows of 32 bytes)."""
    path = find_file(data_dir, "Local/MuunTooltipInfo.bmd")
    if path is None:
        return []
    raw = path.read_bytes()
    count = u32(raw, 0)
    rows: list[dict[str, object]] = []
    for i in range(count):
        r = xor3(raw[4 + i * 32:4 + (i + 1) * 32])
        item_idx = i32(r, 0)
        vals = struct.unpack("<7f", r[4:32])
        rows.append({
            "source": path.name,
            "record": i,
            "group": item_idx // 512,
            "number": item_idx % 512,
            "f1": round(vals[0], 4),
            "f2": round(vals[1], 4),
            "f3": round(vals[2], 4),
            "f4": round(vals[3], 4),
            "f5": round(vals[4], 4),
            "f6": round(vals[5], 4),
            "f7": round(vals[6], 4),
        })
    return rows


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Decode Season 9 Muun BMDs from a desktop Data folder.")
    parser.add_argument("data_dir", type=Path)
    parser.add_argument("--out", type=Path, default=None)
    args = parser.parse_args(argv)
    mi = decode_muun_info(args.data_dir)
    mo = decode_muun_options(args.data_dir)
    mx = decode_muun_exchange(args.data_dir)
    mt = decode_muun_tooltip_info(args.data_dir)
    if args.out:
        for fname, rows in [
            ("muun_info.csv", mi),
            ("muun_options.csv", mo),
            ("muun_exchange.csv", mx),
            ("muun_tooltip_info.csv", mt),
        ]:
            if rows:
                write_csv(args.out / fname, list(rows[0].keys()), rows)
    print(f"muuns={len(mi)} options={len(mo)} exchange={len(mx)} tooltipInfo={len(mt)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
