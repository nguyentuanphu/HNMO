#!/usr/bin/env python3
"""Builds public/data/lang/vi.json: the Vietnamese of the game's English interface texts.

The desktop's English texts (client/src/Localization/{Game,Dialog}.en.resx) are matched to the old
numbered text file of a Vietnamese client through the English numbered file of the same version:
index -> English text -> Vietnamese text. Lines with another server's contact details or links, lines
whose %d / %s placeholders differ from the English, and empty lines are left out; vi-overrides.json
(written by hand: the web game's own texts and corrections) wins over the matched lines.

Usage: build-vi.py <Text_eng.bmd (English, numbered)> <Text.txt (Vietnamese, numbered, UTF-8)>
"""
import json, re, struct, sys
import xml.etree.ElementTree as ET
from pathlib import Path

here = Path(__file__).resolve().parent
web = here.parent.parent
english_bmd, vietnamese_txt = sys.argv[1], sys.argv[2]

data = Path(english_bmd).read_bytes()
key = bytes([0xFC, 0xCF, 0xAB])
offset, english = 6, {}
while offset + 8 <= len(data):
    index, length = struct.unpack_from('<ii', data, offset)
    offset += 8
    if length < 0 or offset + length > len(data):
        break
    english[index] = bytes(b ^ key[i % 3] for i, b in enumerate(data[offset:offset + length])).split(b'\0')[0].decode('latin-1').strip()
    offset += length

vietnamese = {}
for line in Path(vietnamese_txt).read_text(encoding='utf-8', errors='replace').splitlines():
    match = re.match(r'(\d+)\s+"(.*)"\s*$', line.strip())
    if match:
        vietnamese[int(match.group(1))] = match.group(2).strip()

known = set()
for group in ('Game', 'Dialog'):
    for entry in ET.parse(web.parent / 'src' / 'Localization' / f'{group}.en.resx').getroot().findall('data'):
        known.add((entry.find('value').text or '').strip())

contact = re.compile(r'\d{3,4}[. ]\d{3}[. ]\d{3}|zalo|facebook|fb\.|http|www|\.com|\.vn|viet ?nam|muvn|mu vn|admin|gamegua|mubox', re.I)
placeholder = re.compile(r'%(?:I64d|lld|[-+ #0]*\d*(?:\.\d+)?[dsfuxXlc%])')
texts = {}
for index, text in english.items():
    vi = vietnamese.get(index, '')
    if text not in known or not vi or vi == text or contact.search(vi) or contact.search(text):
        continue
    if placeholder.findall(text) != placeholder.findall(vi):
        continue
    texts[text] = vi
texts.update(json.loads((here / 'vi-overrides.json').read_text(encoding='utf-8')))

out = web / 'public' / 'data' / 'lang' / 'vi.json'
out.write_text(json.dumps({'language': 'vi', 'name': 'Tiếng Việt', 'texts': dict(sorted(texts.items()))}, ensure_ascii=False, indent=0), encoding='utf-8')
print(f'{len(texts)} texts -> {out}')
