#!/usr/bin/env python3
"""Builds public/data/lang/<code>.json for the languages of the desktop client's texts
(client/src/Localization/<Group>.<locale>.resx): each English text with its translation, the same
shape as vi.json (build-vi.py). Writes public/data/lang/index.json, the list the language picker shows.
Usage: build-from-resx.py
"""
import json
import xml.etree.ElementTree as ET
from pathlib import Path

here = Path(__file__).resolve().parent
web = here.parent.parent
source = web.parent / 'src' / 'Localization'
out = web / 'public' / 'data' / 'lang'
GROUPS = ('Game', 'Dialog')
NAMES = {
    'en': 'English', 'vi': 'Tiếng Việt', 'de': 'Deutsch', 'es': 'Español', 'pt': 'Português',
    'pl': 'Polski', 'ru': 'Русский', 'uk': 'Українська', 'id': 'Bahasa Indonesia', 'tl': 'Tagalog',
    'zh-TW': '繁體中文', 'zh-CN': '简体中文', 'ja': '日本語',
}

def read(path):
    values = {}
    if path.exists():
        for entry in ET.parse(path).getroot().findall('data'):
            value = entry.find('value')
            values[entry.get('name')] = (value.text or '') if value is not None else ''
    return values

english = {group: read(source / f'{group}.en.resx') for group in GROUPS}
total = sum(len(values) for values in english.values())
languages = [{'code': 'en', 'name': NAMES['en'], 'texts': total}]
for code in sorted({p.name.split('.')[1] for p in source.glob('Game.*.resx')} - {'en'}):
    texts = {}
    for group in GROUPS:
        translated = read(source / f'{group}.{code}.resx')
        for key, text in english[group].items():
            value = translated.get(key, '').strip()
            if value and text.strip() and value != text.strip():
                texts.setdefault(text.strip(), value)
    (out / f'{code}.json').write_text(json.dumps({'language': code, 'name': NAMES.get(code, code), 'texts': dict(sorted(texts.items()))}, ensure_ascii=False, indent=0), encoding='utf-8')
    languages.append({'code': code, 'name': NAMES.get(code, code), 'texts': len(texts)})
    print(f'{code}: {len(texts)} texts')
vi = out / 'vi.json'
if vi.exists():
    languages.append({'code': 'vi', 'name': NAMES['vi'], 'texts': len(json.loads(vi.read_text(encoding='utf-8'))['texts'])})
order = ['en', 'vi'] + sorted(NAMES)
languages.sort(key=lambda language: order.index(language['code']) if language['code'] in order else 99)
(out / 'index.json').write_text(json.dumps({'languages': [{'code': l['code'], 'name': l['name'], 'texts': l['texts']} for l in languages]}, ensure_ascii=False, indent=1), encoding='utf-8')
print('index:', [l['code'] for l in languages])
