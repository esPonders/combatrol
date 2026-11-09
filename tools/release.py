"""Publish the built package using only the workflow's repository-scoped token."""
import json
import os
from pathlib import Path
import urllib.request
import urllib.error

root = Path(__file__).resolve().parents[1]
repo = os.environ['GITHUB_REPOSITORY']
version = '1.0.0'
asset = root / f'dist/Valmods.RolCombat-{version}.zip'
assert asset.is_file(), 'Build the package before publishing'
headers = {'Authorization': 'Bearer ' + os.environ['GITHUB_TOKEN'],
           'Accept': 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28'}


def request(url, method='GET', payload=None, binary=False):
    data = payload if binary else (json.dumps(payload).encode() if payload is not None else None)
    extra = {'Content-Type': 'application/zip' if binary else 'application/json'}
    req = urllib.request.Request(url, data=data, headers={**headers, **extra}, method=method)
    with urllib.request.urlopen(req) as response:
        return json.load(response)


api = f'https://api.github.com/repos/{repo}'
try:
    release = request(api + f'/releases/tags/v{version}')
except urllib.error.HTTPError as error:
    if error.code != 404:
        raise
    release = request(api + '/releases', 'POST', {
        'tag_name': f'v{version}', 'target_commitish': os.environ['GITHUB_SHA'],
        'name': f'RolCombat {version}', 'body': (root / 'RELEASE_NOTES.md').read_text(),
        'draft': True, 'prerelease': False})
if not any(a['name'] == asset.name for a in release.get('assets', [])):
    upload = release['upload_url'].split('{', 1)[0] + '?name=' + asset.name
    request(upload, 'POST', asset.read_bytes(), binary=True)
release = request(api + '/releases/' + str(release['id']), 'PATCH', {
    'draft': False, 'name': f'RolCombat {version}',
    'body': (root / 'RELEASE_NOTES.md').read_text()})
print(release['html_url'])
