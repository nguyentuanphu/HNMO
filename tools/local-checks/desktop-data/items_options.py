#!/usr/bin/env python3
"""Decodes items and item option tables from a desktop client Data folder."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from bmd_common import cstr, find_file, i32, u16, u32, write_csv, xor3


def decode_items(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/Eng/Item_eng.bmd (both 84-byte S6 and 628-byte S9 layouts)."""
    path = find_file(data_dir, "Local/Eng/Item_eng.bmd", "Local/Eng/item_eng.bmd", "Local/Item.bmd")
    if path is None:
        return []
    raw = path.read_bytes()
    rows: list[dict[str, object]] = []

    if len(raw) == 8192 * 84 + 4:
        for idx in range(8192):
            r = xor3(raw[idx * 84:(idx + 1) * 84])
            name = cstr(r[:30])
            two_hand = r[30]
            level = u16(r, 32)
            slot = u16(r, 34)
            skill = u16(r, 36)
            width = r[38]
            height = r[39]
            dmg_min, dmg_max = r[40], r[41]
            def_rate, defense, magic_res = r[42], r[43], r[44]
            atk_speed, walk_speed = r[45], r[46]
            durability, magic_dur, magic_pwr = r[47], r[48], r[49]
            req_str = u16(r, 50)
            req_agi = u16(r, 52)
            req_ene = u16(r, 54)
            req_vit = u16(r, 56)
            req_cmd = u16(r, 58)
            req_lvl = u16(r, 60)
            item_val = r[62]
            zen = u32(r, 64)
            att_type = r[68]
            classes = [r[69 + i] for i in range(7)]
            res = [r[76 + i] for i in range(8)]
            if not name and width == 0 and height == 0:
                continue
            rows.append({
                "source": path.name,
                "record": idx,
                "index": idx,
                "group": idx // 512,
                "number": idx % 512,
                "name": name,
                "level": level,
                "slot": slot,
                "twoHand": two_hand,
                "width": width,
                "height": height,
                "skill": skill,
                "dmgMin": dmg_min,
                "dmgMax": dmg_max,
                "defRate": def_rate,
                "defense": defense,
                "magicRes": magic_res,
                "atkSpeed": atk_speed,
                "walkSpeed": walk_speed,
                "durability": durability,
                "magicDurability": magic_dur,
                "magicPower": magic_pwr,
                "reqLvl": req_lvl,
                "reqStr": req_str,
                "reqAgi": req_agi,
                "reqVit": req_vit,
                "reqEne": req_ene,
                "reqCmd": req_cmd,
                "itemValue": item_val,
                "zen": zen,
                "attType": att_type,
                "clsDW": classes[0],
                "clsDK": classes[1],
                "clsFE": classes[2],
                "clsMG": classes[3],
                "clsDL": classes[4],
                "clsSUM": classes[5],
                "clsRF": classes[6],
                "resIce": res[0],
                "resPoison": res[1],
                "resLightning": res[2],
                "resFire": res[3],
                "resEarth": res[4],
                "resWind": res[5],
                "resWater": res[6],
            })
        return rows

    count = u32(raw, 0)
    if len(raw) == 4 + count * 628 + 4:
        for i in range(count):
            r = xor3(raw[4 + i * 628:4 + (i + 1) * 628])
            idx = u32(r, 0)
            grp = u16(r, 4)
            num = u16(r, 6)
            name = cstr(r[528:558])
            two_hand = r[558]
            drop = r[560]
            level = u16(r, 562)
            slot = u16(r, 564)
            skill = u16(r, 566)
            width, height = r[568], r[569]
            dmg_min = u16(r, 570)
            dmg_max = u16(r, 572)
            def_rate = u16(r, 574)
            defense = u16(r, 576)
            magic_res = u16(r, 578)
            atk_speed = u16(r, 580)
            durability = u16(r, 582)
            magic_dur = u16(r, 584)
            req_str = u16(r, 586)
            req_agi = u16(r, 588)
            req_ene = u16(r, 590)
            req_vit = u16(r, 592)
            req_cmd = u16(r, 594)
            req_lvl = u16(r, 596)
            item_val = u16(r, 598)
            zen_cost = u32(r, 600)
            att_type = r[604]
            classes = [r[605 + c] for c in range(7)]
            res = [r[612 + c] for c in range(8)]
            rows.append({
                "source": path.name,
                "record": i,
                "index": idx,
                "group": grp,
                "number": num,
                "name": name,
                "level": level,
                "slot": slot,
                "twoHand": two_hand,
                "width": width,
                "height": height,
                "skill": skill,
                "dmgMin": dmg_min,
                "dmgMax": dmg_max,
                "defRate": def_rate,
                "defense": defense,
                "magicRes": magic_res,
                "atkSpeed": atk_speed,
                "walkSpeed": 0,
                "durability": durability,
                "magicDurability": magic_dur,
                "magicPower": 0,
                "reqLvl": req_lvl,
                "reqStr": req_str,
                "reqAgi": req_agi,
                "reqVit": req_vit,
                "reqEne": req_ene,
                "reqCmd": req_cmd,
                "itemValue": item_val,
                "zen": zen_cost,
                "attType": att_type,
                "clsDW": classes[0],
                "clsDK": classes[1],
                "clsFE": classes[2],
                "clsMG": classes[3],
                "clsDL": classes[4],
                "clsSUM": classes[5],
                "clsRF": classes[6],
                "resIce": res[0],
                "resPoison": res[1],
                "resLightning": res[2],
                "resFire": res[3],
                "resEarth": res[4],
                "resWind": res[5],
                "resWater": res[6],
            })
        return rows

    raise ValueError(f"Unsupported Item BMD size {len(raw)}")


def decode_ancient_sets(data_dir: Path) -> list[dict[str, object]]:
    """Decodes itemsetoption_eng.bmd."""
    path = find_file(data_dir, "Local/Eng/itemsetoption_eng.bmd", "Local/ItemSetOption.bmd")
    if path is None:
        return []
    raw = path.read_bytes()
    stride = 110
    rows: list[dict[str, object]] = []
    for i in range(len(raw) // stride):
        r = xor3(raw[i * stride:(i + 1) * stride])
        name = cstr(r[:64])
        if not name:
            continue
        rows.append({
            "source": path.name,
            "record": i,
            "setId": i,
            "name": name,
            "optionCount": r[92],
            "classesHex": r[93:110].hex(),
        })
    return rows


def decode_ancient_item_types(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/itemsettype.bmd."""
    path = find_file(data_dir, "Local/itemsettype.bmd")
    if path is None:
        return []
    raw = path.read_bytes()
    rec = 6 if len(raw) >= 8192 * 6 else 4
    rows: list[dict[str, object]] = []
    for idx in range(min(8192, len(raw) // rec)):
        r = xor3(raw[idx * rec:(idx + 1) * rec])
        sets = [r[0], r[1], r[2]] if rec == 6 else [r[0], r[1]]
        mix_lv = [r[3], r[4], r[5]] if rec == 6 else [r[2], r[3]]
        if any(sets):
            rows.append({
                "source": path.name,
                "record": idx,
                "group": idx // 512,
                "number": idx % 512,
                "sets": ";".join(str(v) for v in sets if v),
                "mixLevels": ";".join(str(v) for v in mix_lv),
            })
    return rows


def decode_socket_options(data_dir: Path) -> list[dict[str, object]]:
    """Decodes SocketItem_eng.bmd."""
    path = find_file(data_dir, "Local/Eng/SocketItem_eng.bmd", "Local/Eng/socketitem_eng.bmd")
    if path is None:
        return []
    raw = path.read_bytes()
    stride = 104
    rows: list[dict[str, object]] = []
    for i in range(len(raw) // stride):
        r = xor3(raw[i * stride:(i + 1) * stride])
        opt_id = i32(r, 0)
        cat = i32(r, 4)
        name = cstr(r[8:72])
        if not name:
            continue
        vals = [i32(r, 72 + 4 * k) for k in range(5)]
        rows.append({
            "source": path.name,
            "record": i,
            "id": opt_id,
            "category": cat,
            "name": name,
            "val1": vals[0],
            "val2": vals[1],
            "val3": vals[2],
            "val4": vals[3],
            "val5": vals[4],
        })
    return rows


def decode_harmony_options(data_dir: Path) -> list[dict[str, object]]:
    """Decodes JewelOfHarmonyOption_eng.bmd."""
    path = find_file(data_dir, "Local/Eng/JewelOfHarmonyOption_eng.bmd")
    if path is None:
        return []
    raw = path.read_bytes()
    stride = 180
    rows: list[dict[str, object]] = []
    for i in range(len(raw) // stride):
        r = xor3(raw[i * stride:(i + 1) * stride])
        opt_type = i32(r, 0)
        name = cstr(r[4:64])
        if opt_type <= 0 or not name:
            continue
        vals = [i32(r, 64 + 4 * k) for k in range(15)]
        zen = [i32(r, 124 + 4 * k) for k in range(14)]
        rows.append({
            "source": path.name,
            "record": i,
            "type": opt_type,
            "name": name,
            "values": ";".join(str(v) for v in vals),
            "zenCosts": ";".join(str(v) for v in zen),
        })
    return rows


def decode_item_add_options(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/ItemAddOption.bmd."""
    path = find_file(data_dir, "Local/ItemAddOption.bmd")
    if path is None:
        return []
    dec = xor3(path.read_bytes())
    rows: list[dict[str, object]] = []
    for idx in range(min(8192, len(dec) // 16)):
        o = idx * 16
        op1, val1 = dec[o], u16(dec, o + 2)
        op2, val2 = dec[o + 4], u16(dec, o + 6)
        opt_type = dec[o + 8]
        duration = u32(dec, o + 12)
        if op1 or op2 or duration:
            rows.append({
                "source": path.name,
                "record": idx,
                "group": idx // 512,
                "number": idx % 512,
                "op1": op1,
                "val1": val1,
                "op2": op2,
                "val2": val2,
                "type": opt_type,
                "timeSeconds": duration,
            })
    return rows


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Decode item and option BMDs from a desktop Data folder.")
    parser.add_argument("data_dir", type=Path)
    parser.add_argument("--out", type=Path, default=None)
    args = parser.parse_args(argv)
    items = decode_items(args.data_dir)
    sets = decode_ancient_sets(args.data_dir)
    set_types = decode_ancient_item_types(args.data_dir)
    sockets = decode_socket_options(args.data_dir)
    harmony = decode_harmony_options(args.data_dir)
    add_opts = decode_item_add_options(args.data_dir)
    if args.out:
        for fname, rows in [
            ("items.csv", items),
            ("ancient_sets.csv", sets),
            ("ancient_item_types.csv", set_types),
            ("socket_options.csv", sockets),
            ("harmony_options.csv", harmony),
            ("item_add_options.csv", add_opts),
        ]:
            if rows:
                write_csv(args.out / fname, list(rows[0].keys()), rows)
    print(f"items={len(items)} sets={len(sets)} set_items={len(set_types)} sockets={len(sockets)} harmony={len(harmony)} add={len(add_opts)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
