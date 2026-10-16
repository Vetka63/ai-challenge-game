"""Technical Sprite Forge processing only: no generated or procedural drawing."""
import json, subprocess, sys
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent
SKILL = Path.home()/'.codex/skills/generate2dsprite/scripts'
jobs = json.loads((ROOT/'sources.json').read_text(encoding='utf-8-sig'))

def run(script, args, log):
    p = subprocess.run([sys.executable, '-X', 'utf8', str(SKILL/script), *map(str,args)], capture_output=True, text=True, encoding='utf-8')
    log.parent.mkdir(parents=True, exist_ok=True)
    log.write_text(p.stdout+p.stderr, encoding='utf-8')
    return p.returncode, p.stdout+p.stderr

results=[]
for j in jobs:
    raw=ROOT/'raw'/f"{j['id']}.png"
    out=ROOT/'processed-v3'/j['id']
    spill=ROOT/'qa'/f"spill-v3-{j['id']}"
    if not spill.exists():
        run('sheet_qc.py', ['spill','--input',raw,'--rows',j['rows'],'--cols',j['cols'],'--output-dir',spill],ROOT/'logs'/f"spill-{j['id']}.txt")
    a=np.asarray(Image.open(raw).convert('RGBA'))
    h,w=a.shape[:2]; cw=round(w/j['cols']); ch=round(h/j['rows'])
    ys,xs=np.where(a[:ch,:cw,3]>128)
    height=int(ys.max()-ys.min()+1)
    # Normalize different source canvas resolutions once per clip, never per pose.
    fit=72*max(cw,ch)/(128*height)
    if not out.exists():
        code,msg=run('generate2dsprite.py',['process','--input',raw,'--target','asset','--mode','sheet','--rows',j['rows'],'--cols',j['cols'],'--label-prefix',j['id'],'--output-dir',out,'--background-mode','native_alpha','--alpha-geometry-threshold',128,'--align','feet','--anchor-mode','stance','--scale-strategy','preserve','--cell-size',128,'--fit-scale',fit,'--grid-rounding','nearest','--strict-qc','--max-body-scale-cv',0.08,'--max-anchor-y-std',0.05,'--duration',200 if j['action']=='idle' else 100],ROOT/'logs'/f"process-{j['id']}.txt")
        print(j['id'],code,msg.strip(),flush=True)
    else: code=0
    results.append({'id':j['id'],'first_source_height':height,'source_cell':[cw,ch],'fit_scale':fit,'exit_code':code})
(ROOT/'processing-results.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
sys.exit(any(x['exit_code'] for x in results))
