# SPDX-License-Identifier: AGPL-3.0-only
"""Generate stable per-user MSI components and full application-license RTF."""
import argparse,hashlib,uuid,json
from pathlib import Path
from xml.etree.ElementTree import Element,SubElement,tostring
ROOT=Path(__file__).resolve().parents[1]
NS='http://wixtoolset.org/schemas/v4/wxs'

def identity(prefix,path):
    """Keep IDs and component GUIDs stable across published builds."""
    return prefix+hashlib.sha256(path.encode()).hexdigest()[:24]

def generate(payload,out):
    """Harvest only the staged release; each component owns its own HKCU key."""
    out.mkdir(parents=True,exist_ok=True)
    root=Element('Wix',xmlns=NS);fragment=SubElement(root,'Fragment');group=SubElement(fragment,'ComponentGroup',Id='Payload')
    dirs={'':SubElement(fragment,'DirectoryRef',Id='INSTALLFOLDER')}
    paths=sorted(p for p in payload.rglob('*') if p.is_dir())
    for p in paths:
        rel=p.relative_to(payload).as_posix();parent=p.parent.relative_to(payload).as_posix();parent='' if parent=='.' else parent
        dirs[rel]=SubElement(dirs[parent],'Directory',Id=identity('D',rel),Name=p.name)
    for p in sorted(p for p in payload.rglob('*') if p.is_file()):
        rel=p.relative_to(payload).as_posix();parent=p.parent.relative_to(payload).as_posix();parent='' if parent=='.' else parent
        cid=identity('C',rel)
        comp=SubElement(dirs[parent],'Component',Id=cid,Guid=str(uuid.uuid5(uuid.NAMESPACE_URL,'https://github.com/CGameDev/MeshMe/msi/'+rel)),Bitness='always64')
        SubElement(comp,'File',Id='AppExe' if rel=='MeshMe360.exe' else identity('F',rel),Source=str(p),KeyPath='no')
        SubElement(comp,'RegistryValue',Root='HKCU',Key='Software\\MeshMe360\\Installer\\Files',Name=cid,Type='integer',Value='1',KeyPath='yes')
        SubElement(group,'ComponentRef',Id=cid)
    # Only empty application-owned directories are removed; user data lives elsewhere.
    for rel,directory in dirs.items():
        cid=identity('R',rel);comp=SubElement(directory,'Component',Id=cid,Guid=str(uuid.uuid5(uuid.NAMESPACE_URL,'https://github.com/CGameDev/MeshMe/msi/dir/'+rel)),Bitness='always64')
        SubElement(comp,'RemoveFolder',Id=cid,On='uninstall')
        SubElement(comp,'RegistryValue',Root='HKCU',Key='Software\\MeshMe360\\Installer\\Directories',Name=cid,Type='integer',Value='1',KeyPath='yes')
        SubElement(group,'ComponentRef',Id=cid)
    (out/'Payload.wxs').write_bytes(tostring(root,encoding='utf-8',xml_declaration=True))
    license=(ROOT/'LICENSE').read_text(encoding='utf-8')
    intro='Mesh Me360 0.1.0 preview\nDone by Computer Universal Technology Systems\n\nApplication license: GNU AGPL version 3.\nOriginal BumpMesh engine: CNC Kitchen / Stefan Hermann and contributors.\nSource: https://github.com/CGameDev/MeshMe\n\nThird-party components retain their separate licenses in the installed Licenses folder and THIRD_PARTY_NOTICES.md.\n\n'
    escaped=(intro+license).replace('\\','\\\\').replace('{','\\{').replace('}','\\}').replace('\n','\\par\n')
    (out/'License.rtf').write_text('{\\rtf1\\ansi\\deff0{\\fonttbl{\\f0 Segoe UI;}}\\f0\\fs18\n'+escaped+'}',encoding='ascii')
    manifest={p.relative_to(payload).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(payload.rglob('*')) if p.is_file()}
    (out/'payload-sha256.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    print(f'Harvested {len(manifest)} files')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('payload',type=Path);p.add_argument('output',type=Path);a=p.parse_args();generate(a.payload.resolve(),a.output.resolve())
