import json
data = json.load(open('docs/audit/coverage.json'))
for u in data['units']:
    if u.get('status') == 'Not checked' and 'Server rules' in u.get('kind', ''):
        print(f"{u['id']}: {u['files']} files, {u['lines']} lines")