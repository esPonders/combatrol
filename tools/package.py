#!/usr/bin/env python3
"""Build local-manager packages; never bundle game, BepInEx or Unity assemblies."""
import json
from pathlib import Path
import struct
from zipfile import ZipFile, ZIP_DEFLATED
import zlib

ROOT = Path(__file__).resolve().parents[1]


def icon():
    # Simple code-drawn shield icon, 256x256 RGBA, no external imaging dependency.
    def chunk(kind, payload):
        return struct.pack('>I', len(payload)) + kind + payload + struct.pack('>I', zlib.crc32(kind + payload) & 0xffffffff)
    rows = []
    for y in range(256):
        row = bytearray(b'\0')
        for x in range(256):
            inside = 54 <= x < 202 and 28 <= y < 166 or 166 <= y < 230 and abs(x - 128) < (230-y)*1.15625
            color = (50, 116, 148, 255) if inside else (23, 30, 40, 255)
            if inside and (abs(x-128) < 7 or 79 < y < 93):
                color = (225, 191, 114, 255)
            row.extend(color)
        rows.append(bytes(row))
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>2I5B', 256, 256, 8, 6, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(b''.join(rows))) + chunk(b'IEND', b'')


def package(name, version, folder, description, readme, suffix=''):
    dll = ROOT / f'src/{folder}/bin/Release/netstandard2.1/Valmods.{name}.dll'
    if not dll.is_file():
        raise SystemExit(f'Compila antes {dll.name}')
    manifest = dict(name=f'Valmods_{name}', version_number=version, website_url='https://github.com/esPonders/combatrol',
                    description=description, dependencies=['denikson-BepInExPack_Valheim-5.4.2202'])
    target = ROOT / f'dist/Valmods.{name}-{version}{suffix}.zip'
    target.parent.mkdir(exist_ok=True)
    with ZipFile(target, 'w', ZIP_DEFLATED) as z:
        z.writestr('manifest.json', json.dumps(manifest, ensure_ascii=False, indent=2))
        z.writestr('icon.png', icon())
        z.write(ROOT / readme, 'README.md')
        z.write(dll, f'plugins/{name}/Valmods.{name}.dll')
    with ZipFile(target) as z:
        assert z.testzip() is None
        assert len([n for n in z.namelist() if n.endswith('.dll')]) == 1
        assert z.read(f'plugins/{name}/Valmods.{name}.dll') == dll.read_bytes()
    print(target)


if __name__ == '__main__':
    package('RolCombat', '1.0.1', 'RolCombat', 'Combat roles: tower defense, agile one-handed dodging, and elite balance.', 'README.md')
