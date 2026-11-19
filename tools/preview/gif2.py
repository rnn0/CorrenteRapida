from PIL import Image, ImageDraw
import glob, os, collections, sys
tex={}
for p in glob.glob('tex/*.raw'):
    k,i,s=map(int,os.path.basename(p)[:-4].split('_'))
    tex[(k,i)]=Image.frombytes('RGBA',(s,s),open(p,'rb').read()).transpose(Image.FLIP_TOP_BOTTOM)
frames=collections.defaultdict(list)
for line in open('frames.txt'):
    f,k,lv,e,ti,x,y,w,h,rot,a=line.split()
    frames[(int(f),int(k),int(lv),float(e))].append((int(ti),float(x),float(y),float(w),float(h),float(rot),float(a)))
names=["Gelo","Caos","Tempestade","Corrompida","Luz","Sangue"]
lvn=["Bruta","Lapidada","Lustrosa","Lendaria"]
INNER=int(sys.argv[1]); CELL=int(INNER*3.1); BG=(20,15,19)
cache={}
def get(k,ti,w,h,rot):
    key=(k,ti,w,h,int(rot)%360)
    if key not in cache:
        im=tex[(k,ti)].resize((w,h),Image.LANCZOS)
        if rot: im=im.rotate(rot,resample=Image.BICUBIC,expand=True)
        cache[key]=im
    return cache[key]
def draw(cv,ox,oy,sprites,k):
    for ti,x,y,w,h,rot,a in sprites:
        W=max(1,round(w*INNER)); H=max(1,round(h*INNER))
        im=get(k,ti,W,H,round(rot))
        if a<0.999:
            im=im.copy(); im.putalpha(im.split()[3].point(lambda v,a=a:int(v*a)))
        cv.alpha_composite(im,(int(ox+x*INNER-im.width/2),int(oy-y*INNER-im.height/2)))
def frame(f, rows):
    cv=Image.new('RGBA',(CELL*6+90, CELL*len(rows)+20),BG+(255,)); d=ImageDraw.Draw(cv)
    for r,(lv,e,label) in enumerate(rows):
        d.text((6,r*CELL+CELL//2+20),label,fill=(200,180,150,255))
        for k in range(6):
            draw(cv,90+k*CELL+CELL//2, 20+r*CELL+int(CELL*0.55), frames[(f,k,lv,e)],k)
    for k in range(6): d.text((90+k*CELL+CELL//2-25,4),names[k],fill=(230,210,180,255))
    return cv
import os
rows=[(0,1.0,"Bruta"),(1,1.0,"Lapidada"),(2,1.0,"Lustrosa"),(3,1.0,"Lendaria"),(3,0.0,"Lendaria\n(inativa)")] if os.environ.get("ROWS")!="lite" else [(3,1.0,"Lendaria"),(3,0.0,"Lendaria\n(inativa)")]
which=sys.argv[2]
if which=='still':
    frame(int(sys.argv[3]),rows).convert('RGB').save(sys.argv[4])
else:
    out=[frame(f,rows).convert('RGB').convert('P',palette=Image.ADAPTIVE,colors=255) for f in range(60)]
    out[0].save(sys.argv[3],save_all=True,append_images=out[1:],duration=50,loop=0)
print('ok')
