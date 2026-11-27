#!/usr/bin/env python3
"""Shared binary decoding helpers for desktop client Data/.bmd tables."""

from __future__ import annotations

import csv
import struct
from pathlib import Path
from typing import Iterable, Mapping, Sequence

BUX_KEY = (0xFC, 0xCF, 0xAB)


def xor3(data: bytes) -> bytes:
    """Decodes the desktop client's 3-byte XOR transform (BuxConvert)."""
    return bytes(b ^ BUX_KEY[i % 3] for i, b in enumerate(data))


def cstr(buf: bytes) -> str:
    """Reads a NUL-terminated Windows-1252/Latin-1 string from a fixed buffer."""
    raw = buf.split(b"\x00", 1)[0]
    return raw.decode("cp1252", errors="replace").strip("\r\n")


def find_file(data_dir: Path, *rel_candidates: str) -> Path | None:
    """Case-insensitive lookup across candidate paths relative to a client Data folder."""
    for rel in rel_candidates:
        direct = data_dir / rel
        if direct.is_file():
            return direct
        parts = Path(rel).parts
        cur = data_dir
        ok = True
        for part in parts:
            if not cur.is_dir():
                ok = False
                break
            match = next((p for p in cur.iterdir() if p.name.lower() == part.lower()), None)
            if match is None:
                ok = False
                break
            cur = match
        if ok and cur.is_file():
            return cur
    return None


def write_csv(path: Path, fieldnames: Sequence[str], rows: Iterable[Mapping[str, object]]) -> int:
    """Writes rows to a UTF-8 CSV file with Unix line endings and returns row count."""
    path.parent.mkdir(parents=True, exist_ok=True)
    count = 0
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(fieldnames), lineterminator="\n")
        writer.writeheader()
        for row in rows:
            writer.writerow(row)
            count += 1
    return count


def u16(buf: bytes, off: int) -> int:
    return struct.unpack_from("<H", buf, off)[0]


def i16(buf: bytes, off: int) -> int:
    return struct.unpack_from("<h", buf, off)[0]


def u32(buf: bytes, off: int) -> int:
    return struct.unpack_from("<I", buf, off)[0]


def i32(buf: bytes, off: int) -> int:
    return struct.unpack_from("<i", buf, off)[0]


def f32(buf: bytes, off: int) -> float:
    return struct.unpack_from("<f", buf, off)[0]
