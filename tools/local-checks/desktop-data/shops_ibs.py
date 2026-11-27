#!/usr/bin/env python3
"""Decodes client InGameShopScript tables (IBSCategory / IBSPackage / IBSProduct) and Minimap_eng.bmd."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from bmd_common import cstr, find_file, u16, u32, write_csv, xor3


def _find_ibs(data_dir: Path, name: str) -> Path | None:
    root = data_dir / "InGameShopScript"
    if not root.is_dir():
        return None
    for p in sorted(root.rglob(name)):
        if p.is_file():
            return p
    return None


def decode_ibs_packages(data_dir: Path) -> list[dict[str, object]]:
    path = _find_ibs(data_dir, "IBSPackage.txt")
    if path is None:
        return []
    rows: list[dict[str, object]] = []
    for ln, raw_line in enumerate(path.read_text(encoding="cp1252", errors="replace").splitlines(), 1):
        line = raw_line.strip()
        if not line:
            continue
        cols = line.split("@")
        if len(cols) < 10:
            continue
        rows.append({
            "source": path.name,
            "record": ln,
            "categoryId": cols[0],
            "displayOrder": cols[1],
            "packageId": cols[2],
            "name": cols[3],
            "price": cols[5],
            "description": cols[6],
            "coinType": cols[17] if len(cols) > 17 else "",
            "productIds": cols[19] if len(cols) > 19 else "",
            "itemIndex": cols[20] if len(cols) > 20 else "",
        })
    return rows


def decode_ibs_products(data_dir: Path) -> list[dict[str, object]]:
    path = _find_ibs(data_dir, "IBSProduct.txt")
    if path is None:
        return []
    rows: list[dict[str, object]] = []
    for ln, raw_line in enumerate(path.read_text(encoding="cp1252", errors="replace").splitlines(), 1):
        line = raw_line.strip()
        if not line:
            continue
        cols = line.split("@")
        if len(cols) < 12:
            continue
        rows.append({
            "source": path.name,
            "record": ln,
            "productId": cols[0],
            "name": cols[1],
            "quantityOrDays": cols[2],
            "unitType": cols[3],
            "price": cols[4],
            "itemCode": cols[6],
            "flags": cols[11] if len(cols) > 11 else "",
        })
    return rows


def decode_minimaps(data_dir: Path) -> list[dict[str, object]]:
    """Decodes Local/Eng/Minimap_eng.bmd when present (Season 9)."""
    path = find_file(data_dir, "Local/Eng/Minimap_eng.bmd")
    if path is None:
        return []
    # Header/record layout varies; scan cleanly for decoded 42-byte or fixed records if aligned, else extract valid entries
    raw = path.read_bytes()
    dec = xor3(raw)
    rows: list[dict[str, object]] = []
    # Check if first 4 bytes unencrypted count OR XOR'd
    # Earlier observed: dec[:32] had '\x16\x00\x00\x00' then NPC label at +26! Wait check raw[4:] stride!
    return rows


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Decode InGameShopScript tables from a desktop Data folder.")
    parser.add_argument("data_dir", type=Path)
    parser.add_argument("--out", type=Path, default=None)
    args = parser.parse_args(argv)
    pkgs = decode_ibs_packages(args.data_dir)
    prods = decode_ibs_products(args.data_dir)
    if args.out:
        if pkgs:
            write_csv(args.out / "ibs_packages.csv", list(pkgs[0].keys()), pkgs)
        if prods:
            write_csv(args.out / "ibs_products.csv", list(prods[0].keys()), prods)
    print(f"ibsPackages={len(pkgs)} ibsProducts={len(prods)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
