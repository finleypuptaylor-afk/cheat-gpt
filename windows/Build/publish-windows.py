#!/usr/bin/env python3
"""Publish a verified Windows bootstrap/runtime update and RSA-signed feed from macOS.
Private key stays in the login Keychain, never in the repository or on GitHub runners.
"""
import argparse,hashlib,json,subprocess,tempfile,zipfile
from pathlib import Path

def run(args,**kw):return subprocess.run(args,check=True,capture_output=True,**kw)
def main():
 p=argparse.ArgumentParser();p.add_argument('--bootstrap',type=Path,required=True);p.add_argument('--patch',type=Path,required=True);p.add_argument('--version',required=True);p.add_argument('--commit',required=True);p.add_argument('--gh',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--publish',action='store_true');a=p.parse_args()
 assert len(a.version.split('.'))==3 and all(s.isdecimal() for s in a.version.split('.'))
 assert len(a.commit)==40 and all(c in '0123456789abcdef' for c in a.commit)
 for package in [a.bootstrap,a.patch]:
  with zipfile.ZipFile(package) as z:
   assert z.testzip() is None,'Archive CRC failed'
   assert 'CheatGPT-Windows/CheatGPT.exe' in z.namelist()
   assert 'CheatGPT-Windows/CheatGPT.dll' in z.namelist()
 assert a.patch.stat().st_size<=536870912
 a.output.mkdir(parents=True,exist_ok=True)
 key=run(['security','find-generic-password','-a','windows-updates','-s','CheatGPT.Windows.UpdateSigning','-w']).stdout
 # macOS security prints multiline generic passwords as hex on some releases.
 if not key.lstrip().startswith(b'-----BEGIN'):
  key=bytes.fromhex(key.decode().strip())
 public=run(['openssl','pkey','-pubout'],input=key).stdout
 assert public.strip()==(Path(__file__).parent/'update-public.pem').read_bytes().strip(),'Signing key does not match the app key'
 feed={'version':a.version,'url':'https://github.com/finleypuptaylor-afk/cheat-gpt/releases/download/windows-updates/'+a.patch.name,'sha256':hashlib.file_digest(a.patch.open('rb'),'sha256').hexdigest(),'size':a.patch.stat().st_size}
 metadata=a.output/'windows-update.json';metadata.write_bytes((json.dumps(feed,separators=(',',':'))+'\n').encode())
 signature=run(['openssl','dgst','-sha256','-sign','/dev/stdin',str(metadata)],input=key).stdout
 del key
 (a.output/'windows-update.sig').write_bytes(signature)
 run(['openssl','dgst','-sha256','-verify',str(Path(__file__).parent/'update-public.pem'),'-signature',str(a.output/'windows-update.sig'),str(metadata)])
 hashes=a.output/('Cheat-GPT-Windows-'+a.version+'-SHA256.txt')
 hashes.write_text(''.join(hashlib.file_digest(x.open('rb'),'sha256').hexdigest()+'  '+x.name+'\n' for x in [a.bootstrap,a.patch]))
 print('Verified archives and signed Windows update metadata.',flush=True)
 if not a.publish:return
 repo='finleypuptaylor-afk/cheat-gpt';cli=str(a.gh.resolve())
 release=subprocess.run([cli,'release','view','windows-updates','--repo',repo,'--json','isDraft'],capture_output=True)
 is_new=release.returncode!=0
 if is_new:
  notes=a.output/'release-notes.md';notes.write_text('Cheat GPT for Windows with automatic updates.\n\nFor Windows 1.0/1.1 users: download the full x64 ZIP once, extract it, and open CheatGPT.exe. After this version, signed updates download automatically and install when you quit. The tray menu includes Check for updates and Install update & restart. Automatic updates can be switched off in AI settings. The smaller update ZIP is for the updater; use the x64 ZIP for first installation.\n\nIncludes RAM-based parameter settings. Model inference remains local. Existing preferences and the model are preserved across runtime updates.\n')
  run([cli,'release','create','windows-updates','--repo',repo,'--draft','--target',a.commit,'--title','Cheat GPT for Windows — automatic updates','--notes-file',str(notes),'--latest=false'])
 # Payload first. Older signed feeds remain usable while the new uniquely named payload uploads.
 run([cli,'release','upload','windows-updates',str(a.bootstrap.resolve()),str(a.patch.resolve()),str(hashes.resolve()),'--repo',repo,'--clobber'])
 run([cli,'release','upload','windows-updates',str(metadata.resolve()),str((a.output/'windows-update.sig').resolve()),'--repo',repo,'--clobber'])
 if is_new or json.loads(release.stdout).get('isDraft'):run([cli,'release','edit','windows-updates','--repo',repo,'--draft=false','--latest=false'])
 print('Published: https://github.com/'+repo+'/releases/tag/windows-updates',flush=True)
if __name__=='__main__':main()
