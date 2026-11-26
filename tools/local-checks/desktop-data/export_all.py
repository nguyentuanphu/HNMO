#!/usr/bin/env python3
"""Exports all decoded desktop client binary tables for a Season 6 or Season 9 Data folder.

Usage:
  export_all.py <Data folder> --out <output dir>
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from bmd_common import write_csv
from items_options import (
    decode_ancient_item_types,
    decode_ancient_sets,
    decode_harmony_options,
    decode_item_add_options,
    decode_items,
    decode_socket_options,
)
from master_tree import master_rows, master_tooltips
from muun import decode_muun_exchange, decode_muun_info, decode_muun_options, decode_muun_tooltip_info
from pentagram import (
    decode_attribute_variation,
    decode_errtel_option_values,
    decode_pentagram_mixes,
    decode_pentagram_options,
)
from shops_ibs import decode_ibs_packages, decode_ibs_products
from skills_buffs import decode_buffs, decode_skills
from world_monsters_mixes import (
    decode_gates,
    decode_mixes,
    decode_monster_skills,
    decode_npc_names,
    decode_warps,
)


def export_data(data_dir: Path, out_dir: Path) -> dict[str, int]:
    """Decodes all supported tables in data_dir and writes CSVs into out_dir."""
    counts: dict[str, int] = {}

    mt_rows = [
        {
            "source": "MasterSkillTreeData.bmd",
            **row,
            "requires": ";".join(str(x) for x in row["requires"]),
        }
        for row in master_rows(data_dir)
    ]
    tt_rows = [{"source": "MasterSkillTooltip_eng.bmd", **r} for r in master_tooltips(data_dir)]
    pents, pent_combo = decode_pentagram_options(data_dir)

    tables: list[tuple[str, list[dict[str, object]]]] = [
        ("skills.csv", decode_skills(data_dir)),
        ("buffs.csv", decode_buffs(data_dir)),
        ("master_tree.csv", mt_rows),
        ("master_tooltips.csv", tt_rows),
        ("items.csv", decode_items(data_dir)),
        ("ancient_sets.csv", decode_ancient_sets(data_dir)),
        ("ancient_item_types.csv", decode_ancient_item_types(data_dir)),
        ("socket_options.csv", decode_socket_options(data_dir)),
        ("harmony_options.csv", decode_harmony_options(data_dir)),
        ("item_add_options.csv", decode_item_add_options(data_dir)),
        ("npcs.csv", decode_npc_names(data_dir)),
        ("monster_skills.csv", decode_monster_skills(data_dir)),
        ("gates.csv", decode_gates(data_dir)),
        ("warps.csv", decode_warps(data_dir)),
        ("mixes.csv", decode_mixes(data_dir)),
        ("ibs_packages.csv", decode_ibs_packages(data_dir)),
        ("ibs_products.csv", decode_ibs_products(data_dir)),
        ("muun_info.csv", decode_muun_info(data_dir)),
        ("muun_options.csv", decode_muun_options(data_dir)),
        ("muun_exchange.csv", decode_muun_exchange(data_dir)),
        ("muun_tooltip_info.csv", decode_muun_tooltip_info(data_dir)),
        ("pentagrams.csv", pents),
        ("pentagram_combo_options.csv", pent_combo),
        ("errtel_options.csv", decode_errtel_option_values(data_dir)),
        ("pentagram_mixes.csv", decode_pentagram_mixes(data_dir)),
        ("attribute_variation.csv", decode_attribute_variation(data_dir)),
    ]

    for fname, rows in tables:
        if rows:
            n = write_csv(out_dir / fname, list(rows[0].keys()), rows)
            counts[fname] = n
    return counts


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Export all decoded desktop client BMDs to CSV.")
    parser.add_argument("data_dir", type=Path)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args(argv)
    counts = export_data(args.data_dir, args.out)
    for fname, n in counts.items():
        print(f"  {fname}: {n}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
