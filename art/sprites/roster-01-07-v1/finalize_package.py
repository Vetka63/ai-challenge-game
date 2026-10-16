import hashlib, json, tarfile, zipfile
from collections import Counter
from pathlib import Path
ROOT=Path(__file__).resolve().parent
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
job=read(ROOT/'job.json')
job.setdefault('initial_plan',{'cell':[96,96],'anchor':[48,84]})
job.update(cell=[128,128],anchor=[64,108],user_scope='Two user-selected characters and four directions. Idle/walk/cast chosen by the assistant within the requested animation task.',workflow='ImageGen cel sheets from reference masters, followed by Sprite Forge technical slicing, fixed clip scale, registration, palette and packaging. Video route unavailable; this is an explicitly disclosed deviation from the skill default.')
(ROOT/'job.json').write_text(json.dumps(job,indent=2,ensure_ascii=False),encoding='utf-8')
latest={j['id']:j for j in read(ROOT/'sources.json')}
for file in ['corrections.json','walk-corrections.json']:
    for correction in read(ROOT/file):latest[correction['id']].update(correction)
for j in latest.values():
    j['adopted_file']='raw/'+j['id']+'.png'
    j['sha256']=hashlib.sha256((ROOT/j['adopted_file']).read_bytes()).hexdigest()
(ROOT/'sources-latest.json').write_text(json.dumps(list(latest.values()),indent=2,ensure_ascii=False),encoding='utf-8')
reports=[(p,read(p)) for p in (ROOT/'qa').glob('frames-v2-*/sheet-qc.json')]
counts=Counter(d['status'] for _,d in reports)
summary={'runtime':read(ROOT/'runtime-validation.json')['status'],'unity':read(ROOT/'unity-validation.json')['status'],'art_heuristics':dict(counts),'remaining_limits':['Small rune and hood-outline variation between frames','Hex Cultist east walk still flagged for leg alternation','Arena scene integration and GPU Play Mode rendering are outside this asset-pack verification'],'inspection':'All generated sheets, both final atlases and game-size contact boards inspected; motion poses and alpha checked. No claim of a successful live GPU playback test.'}
(ROOT/'delivery-status.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
lines=['# Итог проверки набора','','Технический экспорт и Unity-выборка 128 кадров: PASS.','',f"Художественные эвристики последних 24 наборов: {counts['pass']} PASS, {counts['warn']} WARN, {counts['fail']} FAIL.",'','FAIL не скрыты:','']
for p,d in reports:
    for c in d['checks']:
        if c['status']=='fail': lines.append('- '+p.parent.name+': `'+c['id']+'`; value `'+json.dumps(c['value'])+'`; threshold `'+json.dumps(c['threshold'])+'`.')
lines.extend(['','Все WARN и FAIL, включая прошлые дубли, дословно сохранены в QA-DETAILS.md.','Фронтальная ходьба не проверяется эвристикой NEAR/FAR по оси X; её проекция другая.','Набор пригоден для импорта и визуальной проверки; это не утверждение о финальной художественной готовности.'])
(ROOT/'QA-SUMMARY.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
with tarfile.open(ROOT/'Roster0107.unitypackage','r:gz') as tar:
    paths=[tar.extractfile(m).read().decode('utf-8') for m in tar.getmembers() if m.name.endswith('/pathname')]
    assert sum(p.endswith('.anim') for p in paths)==24
    assert sum(p.endswith('.prefab') for p in paths)==2
    assert sum(p.endswith('/atlas.png') for p in paths)==2
selected=['README.md','QA-SUMMARY.md','QA-DETAILS.md','qa-issues.json','runtime-validation.json','unity-validation.json','delivery-status.json','sources-latest.json','Roster0107.unitypackage']
files=[ROOT/p for p in selected]
for folder in ['runtime','previews','palette']:
    files.extend(p for p in (ROOT/folder).rglob('*') if p.is_file())
for character in ['runescribe','hex-cultist']:
    files.extend(p for p in (ROOT/'clips-v2'/character).rglob('*') if p.is_file() and ('review' not in p.parts) and p.name!='contact-sheet.png')
with zipfile.ZipFile(ROOT/'Roster0107-ready-sprites.zip','w',zipfile.ZIP_DEFLATED,compresslevel=7) as z:
    for p in files:z.write(p,p.relative_to(ROOT).as_posix())
with zipfile.ZipFile(ROOT/'Roster0107-ready-sprites.zip') as z:assert z.testzip() is None
print(json.dumps({'zip_bytes':(ROOT/'Roster0107-ready-sprites.zip').stat().st_size,'zip_entries':len(files),'unity_package_paths':len(paths),'status':summary},ensure_ascii=False))
