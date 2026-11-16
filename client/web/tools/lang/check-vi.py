#!/usr/bin/env python3
"""Check Vietnamese resource coverage and preserve runtime formatting placeholders."""
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

WEB = Path(__file__).resolve().parents[2]
REPOSITORY = WEB.parents[1]
PRINTF = re.compile(r'%(?:I64d|lld|[-+ #0]*\d*(?:\.\d+)?[dsfuxXlc%])')
INDEXED = re.compile(r'\{\d+[^{}]*\}')


def read_resources(path):
    if not path.exists():
        return {}
    values = {}
    for entry in ET.parse(path).getroot().findall('data'):
        if entry.get('type') or entry.get('mimetype'):
            continue  # Binary/form designer resources are not interface text.
        key = entry.attrib['name']
        if key in values:
            raise ValueError(f'Duplicate resource key: {path}: {key}')
        values[key] = entry.findtext('value', '')
    return values


def check_resources():
    errors = []
    total = 0
    for source in sorted((REPOSITORY / 'server' / 'src').rglob('*.resx')):
        if '.' in source.stem:
            continue  # Neutral resources only; locale files have another suffix.
        vietnamese = source.with_name(source.stem + '.vi.resx')
        translated = read_resources(vietnamese)
        for key, english in read_resources(source).items():
            if not english.strip():
                continue  # Empty descriptions deliberately fall back to no description.
            total += 1
            target = translated.get(key, '')
            if not target.strip():
                errors.append(f'Missing Vietnamese: {source.relative_to(REPOSITORY)}: {key}')
            elif sorted(INDEXED.findall(english)) != sorted(INDEXED.findall(target)):
                errors.append(f'Changed format placeholders: {vietnamese.relative_to(REPOSITORY)}: {key}')
    return total, errors


def check_game_dictionary():
    data = json.loads((WEB / 'public/data/lang/vi.json').read_text(encoding='utf-8'))['texts']
    overrides = json.loads((WEB / 'tools/lang/vi-overrides.json').read_text(encoding='utf-8'))
    errors = []
    for english, vietnamese in data.items():
        if not vietnamese.strip():
            errors.append(f'Empty game translation: {english!r}')
        if PRINTF.findall(english) != PRINTF.findall(vietnamese):
            errors.append(f'Changed printf placeholders/order: {english!r}')
        if overrides.get(english) != vietnamese:
            errors.append(f'Translation missing from maintained overrides: {english!r}')
    for english in ['New here?', 'Read the guide', 'Page language', 'Create an account']:
        if english not in data:
            errors.append(f'Missing public-page translation: {english}')
    return len(data), errors


def main():
    resources, errors = check_resources()
    texts, dictionary_errors = check_game_dictionary()
    errors.extend(dictionary_errors)
    print(f'Checked {resources} nonempty server resource entries and {texts} game/page translations.')
    for error in errors:
        print(error, file=sys.stderr)
    print(f'{len(errors)} coverage/format errors. Proper names and technical tokens may remain unchanged.')
    return bool(errors)


if __name__ == '__main__':
    sys.exit(main())
