import struct,io,wave,zipfile,json,sys
from pathlib import Path
repo=Path(__file__).resolve().parents[1]
p=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else repo.parent/'fixtures'
p.mkdir(exist_ok=True)
# Caller-provided fixtures (including real RHS/JSON files) take precedence.
if not (p/'cursor.png').exists(): (p/'cursor.png').write_bytes((repo/'assets/skin/cursor.png').read_bytes())
if not (p/'Rainbow Freeze.txt').exists(): (p/'Rainbow Freeze.txt').write_bytes((repo/'assets/colorsets/Rainbow Freeze.txt').read_bytes())
if not (p/'CAMLOCKKKKKK.settings.json').exists():
 (p/'CAMLOCKKKKKK.settings.json').write_text(json.dumps(dict(half_ghost=True,approach_rate=29.1,spawn_distance=12.75,cam_unlock=False,master_volume=-6.0206,music_volume=-9.118639,hit_volume=-20)))
if not (p/'main.rhs').exists():
 with zipfile.ZipFile(p/'main.rhs','w') as z:
  z.writestr('config',json.dumps({k:{'Value':v} for k,v in dict(HalfGhost=True,NoteOpacity=.65,NoteScale=.93,CursorScale=1.1,ApproachRate=30,SpawnDistance=10,CameraFov=74,SpinCamera=False).items()}))
  z.writestr('noteSkin/test.obj','v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n')
  z.writestr('borderSkin/test.png',(p/'cursor.png').read_bytes())
a=io.BytesIO()
with wave.open(a,'wb') as w:
 w.setnchannels(1);w.setsampwidth(2);w.setframerate(8000);w.writeframes(b'\0\0'*8000*3)
audio=a.getvalue()
# Small valid PNG, source assets supplied by the test runner.
cover=(p/'cursor.png').read_bytes()
def string(s):
 b=s.encode();return struct.pack('<H',len(b))+b
def block(b):return struct.pack('<Q',len(b))+b
v1=b'SS+m'+struct.pack('<HH',1,0)+b'test-v1\nArtist - Test V1\nMapper\n'+struct.pack('<IIBB',2500,2,2,2)+block(cover)+b'\x01'+block(audio)
v1+=struct.pack('<IBBB',1000,0,0,2)+struct.pack('<IBff',2000,1,0,2)
(p/'v1.sspm').write_bytes(v1)
meta=string('test-v2')+string('Artist - Test V2')+string('Test Song')+struct.pack('<H',1)+string('Mapper')
defs=b'\x01'+string('ssp_note')+b'\x01\x07\x00'
markers=struct.pack('<IBBBB',1000,0,0,0,2)+struct.pack('<IBBff',2000,0,1,0,2)
ab=128+len(meta);cb=ab+len(audio);db=cb+len(cover);mb=db+len(defs)
header=b'SS+m'+struct.pack('<HI',2,0)+bytes(20)+struct.pack('<IIIBHBBB',2700,2,2,3,0,1,1,0)+struct.pack('<10Q',0,0,ab,len(audio),cb,len(cover),db,len(defs),mb,len(markers))
assert len(header)==128
(p/'v2.sspm').write_bytes(header+meta+audio+cover+defs+markers)
(p/'broken.sspm').write_bytes((header+meta+audio+cover+defs+markers)[:-4])
with zipfile.ZipFile(p/'unsafe.vul','w') as z:z.writestr('../escape.txt','no')
if (p/'real.sspm').exists():
 b=(p/'real.sspm').read_bytes();print('Real SSPM version',struct.unpack_from('<H',b,4)[0], 'duration/counts',struct.unpack_from('<III',b,30))
print('Generated V1, V2, truncated and traversal fixtures')
