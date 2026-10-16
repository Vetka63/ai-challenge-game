"""Register, palette-finish and package image-generated frames. No artwork drawing."""
import json, subprocess, sys, shutil
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parent
SKILL=Path.home()/'.codex/skills/generate2dsprite/scripts'
JOBS=json.loads((ROOT/'sources.json').read_text(encoding='utf-8'))
DIRECTIONS=['south','north','west','east']
ACTIONS=['idle','walk','cast']

def run(script,args,log):
    p=subprocess.run([sys.executable,'-X','utf8',str(SKILL/script),*map(str,args)],capture_output=True,text=True,encoding='utf-8')
    (ROOT/'logs'/log).write_text(p.stdout+p.stderr,encoding='utf-8')
    if p.returncode: raise RuntimeError(p.stdout+p.stderr)
    print(script,p.stdout.strip(),flush=True)

registered=ROOT/'registered-v2'
registration=[]
for j in JOBS:
    source=ROOT/'processed-v3'/j['id']
    meta=json.loads((source/'pipeline-meta.json').read_text())
    offset=(0,108-round(meta['output_origin'][1]))
    aligned=ROOT/'aligned-v2'/j['id']
    if not aligned.exists():
        run('scale_frames.py',['--frames',*[source/f'{label}.png' for label in meta['frame_labels']],'--output-dir',aligned,'--scale-from',1,'--resampler','nearest','--root-lock','none' if j['action']=='cast' else 'torso-x','--lock','feet','--canvas','128,128','--anchor-px','64,108','--alpha-threshold',128,'--no-loop' if j['action']=='cast' else '--loop'],f'align-{j["id"]}.txt')
    aligned_files=sorted((aligned/'frames').glob('*.png'))
    assert len(aligned_files)==j['frames']
    dest=registered/j['id']; dest.mkdir(parents=True,exist_ok=True)
    for i,label in enumerate(meta['frame_labels']):
        frame=Image.open(aligned_files[i]).convert('RGBA')
        arr=np.asarray(frame); ys,xs=np.where(arr[:,:,3]>=128)
        assert xs.min()>0 and xs.max()<127 and ys.min()>0 and ys.max()<127
        frame.save(dest/f'{j["id"]}-{i:02d}.png')
    registration.append({'id':j['id'],'translation_px':offset,'final_root':[64,108],'source_to_output_scale':meta['geometry']['scale']})
(ROOT/'registration.json').write_text(json.dumps(registration,indent=2),encoding='utf-8')
palette=ROOT/'palette'
if not palette.exists():
    run('palette_tool.py',['build','--input',*[registered/j['id'] for j in JOBS],'--colors',32,'--balance','--reserve','#080c15','#fff1b2','#00dcf5','#ee20eb','--name','Runes-and-Hex','--output-dir',palette],'palette-build.txt')
for j in JOBS:
    dest=ROOT/'on-palette-v2'/j['id']
    if not dest.exists():
        run('palette_tool.py',['quantize-seq','--input',registered/j['id'],'--palette',palette/'palette.json','--loop-policy','oneshot' if j['action']=='cast' else 'cycle','--no-despeckle','--orphans',0,'--output-dir',dest],f'palette-{j["id"]}.txt')

# Do not assume how the installed skill names its output folder; inspect its published PNGs.
def frame_files(j):
    out=ROOT/'on-palette-v2'/j['id']
    files=sorted(out.glob('*.png'))
    files=[p for p in files if Image.open(p).size==(128,128)]
    if not files:
        files=sorted(p for p in out.rglob('*.png') if Image.open(p).size==(128,128) and 'indexed' not in p.parts)
    assert len(files)==j['frames'],(j['id'],files)
    return files

pack=ROOT/'runtime'; pack.mkdir(exist_ok=True)
preview=ROOT/'previews'; preview.mkdir(exist_ok=True)
frame_lookup={}
unity={'schema':'roster-unity.v1','cellSize':128,'pixelsPerUnit':64,'rootTopLeft':[64,108],'characters':[]}
for character in ['runescribe','hex-cultist']:
    folder=pack/character; (folder/'frames').mkdir(parents=True,exist_ok=True); (folder/'sheets').mkdir(exist_ok=True)
    frames=[]; clips={}; atlas=Image.new('RGBA',(1024,1024)); atlas_meta=[]; clip_array=[]
    for action in ACTIONS:
        for facing in DIRECTIONS:
            j=next(j for j in JOBS if j['character']==character and j['action']==action and j['facing']==facing)
            name=f'{action}_{facing}'; indices=[]; strip=Image.new('RGBA',(128*j['frames'],128))
            for i,file in enumerate(frame_files(j)):
                frame=Image.open(file).convert('RGBA'); label=f'{name}_{i:02d}'
                output=folder/'frames'/f'{label}.png'; frame.save(output)
                index=len(frames); indices.append(index); frames.append({'name':label,'file':f'frames/{label}.png'})
                x=index%8*128; y=index//8*128; atlas.paste(frame,(x,y)); strip.paste(frame,(i*128,0))
                atlas_meta.append({'name':label,'index':index,'rectTopLeft':[x,y,128,128]})
                frame_lookup[(character,action,facing,i)]=frame
            strip.save(folder/'sheets'/f'{name}.png')
            ticks=12 if action=='idle' else [6,6,6,6,6,12] if action=='cast' else 6
            clip={'frames':indices,'ticks':ticks,'loop_policy':'oneshot' if action=='cast' else 'cycle','role':'player'}
            if action=='walk': clip['events']=[{'at':0,'name':'step_l'},{'at':3,'name':'step_r'}]
            if action=='cast':
                clip['events']=[{'at':3,'name':'custom:spell_release'},{'at':5,'name':'end'}]
                clip['keys']={'wind_start':1,'wind_peak':2,'strike':3,'recover':4,'end':5}
            clips[name]=clip
            durations=[200]*4 if action=='idle' else [100,100,100,100,100,200] if action=='cast' else [100]*6
            clip_array.append({'name':name,'indices':indices,'durationsMs':durations,'loop':action!='cast'})
    atlas.save(folder/'atlas.png')
    (folder/'atlas.json').write_text(json.dumps({'cell':[128,128],'rootTopLeft':[64,108],'frames':atlas_meta,'clips':clip_array},indent=2),encoding='utf-8')
    manifest={'schema':'generate2dsprite.animation_clips.v2','frames':frames,'anchor_px':[64,108],'art_source':'host_image','pixel_art':True,'sampling':'nearest','body_height_px':72,'clips':clips,'states':{name:name for name in clips}}
    (folder/'clips-input.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    built=ROOT/'clips-v2'/character
    if not built.exists(): run('build_animation_clips.py',['--manifest',folder/'clips-input.json','--output-dir',built,'--preview-scale',3,'--preview-background','all','--strict'],f'clips-{character}.txt')
    unity['characters'].append({'id':character,'frames':[f['name'] for f in frames],'clips':clip_array})
(pack/'unity-manifest.json').write_text(json.dumps(unity,indent=2),encoding='utf-8')

font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',16)
for action in ACTIONS:
    count=4 if action=='idle' else 6
    boards=[]
    for i in range(count):
        board=Image.new('RGBA',(1024,572),'#111824'); draw=ImageDraw.Draw(board)
        for c,character in enumerate(['runescribe','hex-cultist']):
            for d,facing in enumerate(DIRECTIONS):
                frame=frame_lookup[(character,action,facing,i)].resize((256,256),Image.Resampling.NEAREST)
                board.alpha_composite(frame,(256*d,286*c+24))
                draw.text((256*d+16,286*c+8),f'{character} / {facing}',font=font,fill='#9ce4f0' if c==0 else '#ef9aea')
        boards.append(board.convert('RGB'))
    durations=[200]*4 if action=='idle' else [100,100,100,100,100,500] if action=='cast' else [100]*6
    boards[0].save(preview/f'{action}.gif',save_all=True,append_images=boards[1:],duration=durations,loop=0,disposal=2)
    boards[0].save(preview/f'{action}.png')
print('Runtime pack complete: 128 frames, 24 clips, two atlases.',flush=True)
