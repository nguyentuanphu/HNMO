#!/usr/bin/env python3
"""Decodes Pentagram, Errtel, Pentagram Mix, and Elemental AttributeVariation tables."""

from __future__ import annotations

import argparse
import struct
import sys
from pathlib import Path

from bmd_common import cstr, find_file, i32, u32, write_csv, xor3
from items_options import decode_items


def decode_pentagram_options(data_dir: Path) -> tuple[list[dict[str, object]], list[dict[str, object]]]:
    """Decodes Local/Eng/PentagramOption_eng.bmd (15 pentagrams + 7 combination options)."""
    path = find_file(data_dir, "Local/Eng/PentagramOption_eng.bmd")
    if path is None:
        return [], []
    raw = path.read_bytes()
    h1, h2 = struct.unpack("<2I", raw[:8])
    pent_rows: list[dict[str, object]] = []
    off = 8
    for i in range(h1):
        r = xor3(raw[off + i * 48:off + (i + 1) * 48])
        idx, grp, kind, num, grade = struct.unpack("<IBBHB", r[:9])
        name = cstr(r[9:41])
        pent_rows.append({
            "source": path.name,
            "record": i,
            "itemIndex": idx,
            "group": grp,
            "number": num,
            "kind": kind,
            "grade": grade,
            "name": name,
        })
    off += h1 * 48
    opt_rows: list[dict[str, object]] = []
    for j in range(h2):
        r = xor3(raw[off + j * 268:off + (j + 1) * 268])
        opt_num = struct.unpack("<H", r[:2])[0]
        name = cstr(r[2:258])
        reqs = struct.unpack("<3b3b3b", r[258:267])
        opt_rows.append({
            "source": path.name,
            "record": j,
            "optionNum": opt_num,
            "name": name,
            "reqPairs": ";".join(
                f"errtel={reqs[k]}:minRank={reqs[k + 1]}:minLv={reqs[k + 2]}"
                for k in range(0, 9, 3) if reqs[k] > 0
            ),
        })
    return pent_rows, opt_rows


def decode_errtel_option_values(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/Eng/PentagramJewelOptionValue_eng.bmd (60 rows of 372 bytes)."""
    path = find_file(data_dir, "Local/Eng/PentagramJewelOptionValue_eng.bmd")
    if path is None:
        return []
    raw = path.read_bytes()
    count = u32(raw, 0)
    rows: list[dict[str, object]] = []
    for i in range(count):
        r = xor3(raw[4 + i * 372:4 + (i + 1) * 372])
        idx, item_idx, _, rank_opt, weight, opt_num = struct.unpack("<6I", r[:24])
        lv_vals = list(struct.unpack("<11I", r[24:68]))
        mix_rates = list(struct.unpack("<11I", r[68:112]))
        name = cstr(r[112:])
        rows.append({
            "source": path.name,
            "record": i,
            "index": idx,
            "group": item_idx // 512,
            "number": item_idx % 512,
            "rank": rank_opt & 0xFF,
            "subOption": (rank_opt >> 8) & 0xFF,
            "optionRate": weight,
            "optionNum": opt_num,
            "name": name,
            **{f"lv{lv}": lv_vals[lv] for lv in range(11)},
            **{f"mixRateLv{lv}": mix_rates[lv] for lv in range(11)},
        })
    return rows


def decode_pentagram_mixes(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/PentagramMixNeedSource.bmd (30 rows of 92 bytes)."""
    path = find_file(data_dir, "Local/PentagramMixNeedSource.bmd")
    if path is None:
        return []
    raw = path.read_bytes()
    count = u32(raw, 0)
    names = {(it["group"], it["number"]): it["name"] for it in decode_items(data_dir)}
    rows: list[dict[str, object]] = []
    for i in range(count):
        r = xor3(raw[4 + i * 92:4 + (i + 1) * 92])
        v = struct.unpack("<23i", r)
        # v[0]=category(0=base/refine,2=errtel_lv,3=errtel_rank), v[1]=recipe_id, v[2]=rate_out_of_10000
        # v[3..6]=packed_item_ids, v[7..10]=groups, v[11..14]=numbers, v[15..18]=counts
        # v[19]=zen, v[20]=req_level, v[21]=unk21, v[22]=target_flag
        mats = []
        for s in range(4):
            if v[3 + s] >= 0 and v[15 + s] > 0:
                if v[7 + s] == 100:
                    mats.append(f"Rank1..5Errtel(num={v[11 + s]})x{v[15 + s]}")
                else:
                    nm = names.get((v[7 + s], v[11 + s]), f"{v[7 + s]}:{v[11 + s]}")
                    mats.append(f"{v[7 + s]}:{v[11 + s]}({nm})x{v[15 + s]}")
        rows.append({
            "source": path.name,
            "record": i,
            "category": v[0],
            "recipeId": v[1],
            "rateOutOf10000": v[2],
            "zen": v[19],
            "reqLevel": v[20],
            "flag21": v[21],
            "flag22": v[22],
            "materials": " + ".join(mats),
        })
    return rows


def decode_attribute_variation(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/AttributeVariation.bmd (38 rows of 3140 bytes)."""
    path = find_file(data_dir, "Local/AttributeVariation.bmd")
    if path is None:
        return []
    raw = path.read_bytes()
    count = u32(raw, 0)
    rows: list[dict[str, object]] = []
    for i in range(count):
        r = xor3(raw[4 + i * 3140:4 + (i + 1) * 3140])
        kind, entity_id, grp, num, variant = struct.unpack("<5I", r[:20])
        rows.append({
            "source": path.name,
            "record": i,
            "kind": kind,
            "entityId": entity_id,
            "itemGroup": grp,
            "itemNumber": num,
            "elementOrVariant": variant,
        })
    return rows


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Decode Season 9 Pentagram and Errtel BMDs from a desktop Data folder.")
    parser.add_argument("data_dir", type=Path)
    parser.add_argument("--out", type=Path, default=None)
    args = parser.parse_args(argv)
    pents, pent_opts = decode_pentagram_options(args.data_dir)
    errtel_opts = decode_errtel_option_values(args.data_dir)
    pmix = decode_pentagram_mixes(args.data_dir)
    av = decode_attribute_variation(args.data_dir)
    if args.out:
        for fname, rows in [
            ("pentagrams.csv", pents),
            ("pentagram_combo_options.csv", pent_opts),
            ("errtel_options.csv", errtel_opts),
            ("pentagram_mixes.csv", pmix),
            ("attribute_variation.csv", av),
        ]:
            if rows:
                write_csv(args.out / fname, list(rows[0].keys()), rows)
    print(f"pentagrams={len(pents)} comboOpts={len(pent_opts)} errtelOpts={len(errtel_opts)} mixes={len(pmix)} attrVar={len(av)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
