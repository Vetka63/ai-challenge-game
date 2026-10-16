import json, subprocess, sys
from pathlib import Path
from collections import Counter
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parent
SKILL=Path.home()/'.codex/skills/generate2dsprite/scripts'
jobs=json.loads((ROOT/'sources.json').read_text())
for j in jobs:
    out=ROOT/'qa'/('frames-v2-'+j['id'])
    if out.exists(): continue
    frames=sorted((ROOT/'runtime'/j['character']/'frames').glob(f"{j['action']}_{j['facing']}_*.png"))
    # The auto-luminance near/far gait test infers leading legs along image X.
    # It applies to side views; north/south motion projects along image Y.
    args=[sys.executable,'-X','utf8',str(SKILL/'sheet_qc.py'),'frames','--frames',*map(str,frames),'--output-dir',str(out),'--cycle','walk' if j['action']=='walk' and j['facing'] in ['east','west'] else 'none','--no-loop' if j['action']=='cast' else '--loop']
    if j['facing'] in ['east','west']: args.extend(['--facing','right' if j['facing']=='east' else 'left'])
    p=subprocess.run(args,capture_output=True,text=True,encoding='utf-8')
    (ROOT/'logs'/f"frames-{j['id']}.txt").write_text(p.stdout+p.stderr,encoding='utf-8')
    print(j['id'],p.returncode,p.stdout.strip(),flush=True)

checks=[]; colors=set(); heights=[]
for p in (ROOT/'runtime').glob('*/frames/*.png'):
    im=Image.open(p); a=np.asarray(im)
    assert im.mode=='RGBA' and im.size==(128,128),p
    assert set(np.unique(a[:,:,3])).issubset({0,255}),p
    assert not np.any(a[a[:,:,3]==0,:3]),p
    ys,xs=np.where(a[:,:,3]==255)
    assert xs.min()>0 and xs.max()<127 and ys.min()>0 and ys.max()<127,p
    colors.update(map(tuple,a[a[:,:,3]==255,:3].tolist()))
    heights.append(int(ys.max()-ys.min()+1))
    checks.append({'file':str(p.relative_to(ROOT)),'bounds':[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)]})
assert len(checks)==128 and len(colors)<=32
report={'status':'pass','frames':len(checks),'frame_size':[128,128],'root_top_left':[64,108],'visible_colors':len(colors),'partial_alpha_pixels':0,'clipped_frames':0,'visible_height_range_including_raised_staff':[min(heights),max(heights)],'frames_detail':checks}
(ROOT/'runtime-validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
issues=[]; counts=Counter()
for p in ROOT.rglob('*.json'):
    if p.name in ['qa-issues.json']: continue
    try:d=json.loads(p.read_text(encoding='utf-8-sig'))
    except Exception:continue
    if not isinstance(d,dict):continue
    env=d.get('qa',d)
    if not isinstance(env,dict):continue
    for check in env.get('checks',[]):
        if check.get('status') in ['warn','fail']:
            entry={'report':str(p.relative_to(ROOT)),**check}
            issues.append(entry);counts[check['status']+':'+check['id']]+=1
(ROOT/'qa-issues.json').write_text(json.dumps(issues,indent=2,ensure_ascii=False),encoding='utf-8')
lines=['# QA: warnings and failures, preserved verbatim','', 'Includes source diagnostics and rejected takes. Runtime technical validation is separate in runtime-validation.json and unity-validation.json.','']
for issue in issues:
    lines.extend(['## '+issue['report'], '', '```json', json.dumps({k:v for k,v in issue.items() if k!='report'},ensure_ascii=False,indent=2),'```',''])
(ROOT/'QA-DETAILS.md').write_text('\n'.join(lines),encoding='utf-8')
print(json.dumps({'runtime':{k:v for k,v in report.items() if k!='frames_detail'},'diagnostics':counts},ensure_ascii=False),flush=True)
