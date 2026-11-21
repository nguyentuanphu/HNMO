#!/usr/bin/env python3
"""Writes an HNMO folder's version.json and manifest.json (owner, 2026-11-21: updates by file).

manifest.json lists every file of the folder (not the host's save folder) with its size and SHA-256. The updater
compares it with the one of the folder it runs in and downloads only the files whose hash changed; the files are
stored by hash (files/<sha256>), so a file that didn't change between versions is stored and downloaded once.

Usage: write-update-manifest.py <HNMO folder> <version> <build stamp>
"""
import hashlib
import json
import os
import sys

folder, version, stamp = sys.argv[1], sys.argv[2], sys.argv[3]
SKIP_TOP = {'save'}
SKIP_FILES = {'manifest.json'}

files = []
for root, dirs, names in os.walk(folder):
    rel_root = os.path.relpath(root, folder)
    if rel_root == '.':
        dirs[:] = [d for d in dirs if d not in SKIP_TOP]
    for name in names:
        rel = os.path.normpath(os.path.join(rel_root, name)).replace(os.sep, '/')
        if rel in SKIP_FILES:
            continue
        path = os.path.join(root, name)
        digest = hashlib.sha256()
        with open(path, 'rb') as f:
            for chunk in iter(lambda: f.read(1 << 20), b''):
                digest.update(chunk)
        mode = os.stat(path).st_mode
        files.append({'path': rel, 'size': os.path.getsize(path), 'sha256': digest.hexdigest(), 'exec': bool(mode & 0o111)})
files.sort(key=lambda f: f['path'])

with open(os.path.join(folder, 'version.json'), 'w', encoding='utf-8') as f:
    json.dump({'version': version, 'build': stamp}, f)
# version.json is part of the folder too: hash it after writing it.
with open(os.path.join(folder, 'version.json'), 'rb') as f:
    data = f.read()
files = [f for f in files if f['path'] != 'version.json']
files.append({'path': 'version.json', 'size': len(data), 'sha256': hashlib.sha256(data).hexdigest(), 'exec': False})
files.sort(key=lambda f: f['path'])

with open(os.path.join(folder, 'manifest.json'), 'w', encoding='utf-8') as f:
    json.dump({'version': version, 'build': stamp, 'files': files}, f, separators=(',', ':'))
print(f'manifest.json: version {version}, {len(files)} files, {sum(f["size"] for f in files) // (1 << 20)} MB')
