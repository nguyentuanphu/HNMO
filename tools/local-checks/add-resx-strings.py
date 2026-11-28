#!/usr/bin/env python3
"""Adds strings to a .resx family (the neutral file, its language files) and its Designer class, keeping the existing
entries: add-resx-strings.py <neutral.resx> <strings.json>. strings.json: {"Key": {"en": "...", "vi": "...", ...}}.
A key already present is left as it is."""

import json
import re
import sys
from pathlib import Path
from xml.sax.saxutils import escape


def add_to_resx(path: Path, entries: dict) -> None:
    if not path.exists():
        return
    raw = path.read_bytes()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    blocks = "".join(
        f'  <data name="{key}" xml:space="preserve">\n    <value>{escape(value)}</value>\n  </data>\n'
        for key, value in entries.items() if f'<data name="{key}"' not in text)
    text = text.replace("</root>", blocks + "</root>", 1)
    path.write_bytes((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))


def add_to_designer(path: Path, entries: dict) -> None:
    raw = path.read_bytes()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    lines = text.split("\n")
    closing = max(i for i, line in enumerate(lines) if line.strip() == "}" and line.startswith("    }"))
    added = []
    for key, value in entries.items():
        if re.search(rf"\bstatic string {key}\b", text):
            continue
        summary = escape(value if len(value) <= 100 else value[:97] + "...")
        added += ["        /// <summary>", f"        ///   Looks up a localized string similar to {summary}.", "        /// </summary>",
                  f"        public static string {key} {{", "            get {",
                  f'                return ResourceManager.GetString("{key}", resourceCulture);', "            }", "        }"]
    lines[closing:closing] = added
    path.write_bytes((b"\xef\xbb\xbf" if bom else b"") + "\n".join(lines).encode("utf-8"))


def main() -> int:
    neutral = Path(sys.argv[1])
    strings = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
    add_to_resx(neutral, {k: v["en"] for k, v in strings.items()})
    for language in ("vi", "de", "zh-CN"):
        add_to_resx(neutral.with_name(f"{neutral.stem}.{language}.resx"), {k: v.get(language, v["en"]) for k, v in strings.items()})
    add_to_designer(neutral.with_name(f"{neutral.stem}.Designer.cs"), {k: v["en"] for k, v in strings.items()})
    return 0


if __name__ == "__main__":
    sys.exit(main())
