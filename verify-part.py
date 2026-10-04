import argparse,json,pathlib,xml.etree.ElementTree as ET
base=pathlib.Path(__file__).parent
part=json.loads((base/'assets/hydropneumaticSuspensionPart.json').read_text(encoding='utf-8'))
parser=argparse.ArgumentParser()
parser.add_argument('--game-dir',default=r'C:\Program Files (x86)\Steam\steamapps\common\Sprocket')
game=pathlib.Path(parser.parse_args().game_dir)/'Sprocket_Data/StreamingAssets/Parts'
assert part['guid']=='75f3d8e2-6c19-4b27-9f50-481c6a8d02b4'
assert part['type']=='suspension' and 'suspension' in part['tags']
schema_reference=json.loads((game/'hvssPart.json').read_text(encoding='utf-8-sig'))
assert part['v']==schema_reference['v'], 'Use tested native part schema version'
assert set(schema_reference).issubset(part), 'Missing stock part schema fields'
assert part['name']=='hydropneumaticSuspension'
assert ET.parse(base/'assets/hydropneumaticSuspension.xml').findtext('name')=='Hydropneumatic'
components=part['components']+[c for o in part['transform']['objects'] for c in o['components']]
ids=[c['fileID'] for c in components]
assert len(ids)==len(set(ids)), 'Duplicate component IDs'
objects={o['fileID'] for o in part['transform']['objects']}
for obj in part['transform']['objects']:
 assert obj['parentFileID'] is None or obj['parentFileID'] in objects
def walk(value):
 if isinstance(value,dict):
  for key,item in value.items():
   if key.endswith('FileID') and isinstance(item,str):assert item in ids,(key,item)
   if key.endswith('FileIDs'):
    for ref in item:assert ref in ids,(key,ref)
   yield from walk(item)
 elif isinstance(value,list):
  for item in value:yield from walk(item)
 elif isinstance(value,str):yield value
list(walk(part))
assert len([c for c in components if c['type']=='suspensionArm'])==1
assert len([c for c in components if c['type']=='axle'])==1
assert not any(c['type'] in ('torsionBar','coilSpring') for c in components)
native_types=set();native_assets=set()
for path in game.glob('*.json'):
 try:data=json.loads(path.read_text(encoding='utf-8-sig'))
 except (ValueError,UnicodeError):continue
 if not isinstance(data,dict):continue
 if path.name!='hydropneumaticSuspensionPart.json':assert data.get('guid')!=part['guid'],'GUID collision'
 for obj in [data]+list(data.get('components') or []):
  if 'type' in obj:native_types.add(obj['type'])
 def references(v):
  if isinstance(v,dict):
   for k,item in v.items():
    if k=='meshGuid':native_assets.add(item)
    if k=='materialGuids':native_assets.update(item)
    references(item)
  elif isinstance(v,list):
   for item in v:references(item)
 references(data)
 for obj in ((data.get('transform') or {}).get('objects') or []):
  native_types.update(c['type'] for c in (obj.get('components') or []))
for c in components:assert c['type'] in native_types,c['type']
def check_assets(v):
 if isinstance(v,dict):
  for k,item in v.items():
   if k=='meshGuid':assert item in native_assets,item
   if k=='materialGuids':assert all(x in native_assets for x in item)
   check_assets(item)
 elif isinstance(v,list):
  for item in v:check_assets(item)
check_assets(part)
print('PASS: independent part identity, suspension selector tag, display name, complete component graph, one-wheel topology, existing native factories and stock mesh/material references.')
