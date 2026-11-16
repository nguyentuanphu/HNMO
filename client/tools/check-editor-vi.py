#!/usr/bin/env python3
"""Validate Vietnamese editor catalogs, formatting tokens and hidden widget IDs."""
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
CATALOGS = ROOT / 'src/Localization'
FORMAT = re.compile(r'%%|(?<![\d.%])%(?:\d+\$)?[-+ #0]*(?:\d+|\*)?(?:\.(?:\d+|\*))?(?:hh|ll|I(?:32|64)|[hljztL])?[diuoxXfFeEgGaAcCsSpn]|\{[^{}]+\}')
NUMBERS = re.compile(r'(?<![\w\d])[+-]\d+(?:[.,]\d+)?|\d+(?:[.,]\d+)?|%')


def read(path, allow_duplicates=False):
    values = {}
    for entry in ET.parse(path).getroot().findall('data'):
        key = entry.attrib['name']
        value = entry.findtext('value', '')
        if key in values and not allow_duplicates:
            raise ValueError(f'{path.name}: duplicate key {key!r}')
        # Match ResxGen's existing last-entry-wins behavior for neutral catalogs.
        values[key] = value
    return values


def is_format(match, text):
    token = match.group()
    # A sentence such as "100 % of" is a percentage, not a space-flag printf token.
    return token == '%%' or not (token.startswith('%') and re.search(r'\d[\d.,]*\s*$', text[:match.start()]))


def format_tokens(text):
    return [match.group() for match in FORMAT.finditer(text) if is_format(match, text)]


def number_tokens(text):
    masked = FORMAT.sub(lambda match: '' if is_format(match, text) else match.group(), text)
    return NUMBERS.findall(masked)


def check():
    errors = []
    total = 0
    for group in ['Editor', 'Metadata']:
        english = read(CATALOGS / f'{group}.en.resx', allow_duplicates=True)
        vietnamese = read(CATALOGS / f'{group}.vi.resx')
        for key in english.keys() ^ vietnamese.keys():
            errors.append(f'{group}: key mismatch: {key}')
        for key, original in english.items():
            translated = vietnamese.get(key, '')
            total += 1
            if original.strip() and not translated.strip():
                errors.append(f'{group}: missing Vietnamese: {key}')
            if format_tokens(original) != format_tokens(translated):
                errors.append(f'{group}: formatting placeholders changed: {key}')
            if number_tokens(original) != number_tokens(translated):
                errors.append(f'{group}: numbers/percentages changed: {key}')
            if '##' in original and original[original.index('##'):] != translated[translated.find('##'):]:
                errors.append(f'{group}: hidden widget ID changed: {key}')
            if 'ZXQ' in translated:
                errors.append(f'{group}: unresolved translation marker: {key}')
    print(f'Checked {total} Vietnamese editor/metadata entries; {len(errors)} errors.')
    for error in errors:
        print(error, file=sys.stderr)
    return bool(errors)


if __name__ == '__main__':
    sys.exit(check())
