"""Integer-grid SVG authoring; the current exporter rasterizes with local resvg."""
from pathlib import Path
from html import escape
import json
import math
import numpy as np

ROOT = Path(__file__).resolve().parent
PALETTE = {
    'ink':'#192829', 'dark':'#253b3b', 'shadow':'#344a43',
    'steel':'#536a68', 'light':'#8ba39a', 'cream':'#ddd5ac',
    'white':'#eee9d3', 'soil':'#503e32', 'brown':'#76533b',
    'tan':'#aa8054', 'sand':'#c4ab75', 'rust':'#9b5338',
    'amber':'#e59a50', 'gold':'#edc566', 'red':'#b75446',
    'rose':'#d68169', 'leaf':'#527445', 'green':'#739650',
    'olive':'#81824a', 'teal':'#80d8bc', 'cyan':'#4c9e9b',
    'ice':'#aed5d2', 'blue':'#497b86', 'purple':'#796175',
}
NAMES=list(PALETTE)
RGB=[tuple(bytes.fromhex(PALETTE[n][1:]))+(255,) for n in NAMES]

class Sprite:
    def __init__(self, name, label, outline=True):
        self.name,self.label,self.outline=name,label,outline
        self.p=np.full((32,32),-1,dtype=np.int16)
    def rect(self,x,y,w,h,c):
        assert all(isinstance(v,int) for v in (x,y,w,h)), (self.name,x,y,w,h)
        assert w>0 and h>0 and x>=0 and y>=0 and x+w<=32 and y+h<=32,(self.name,x,y,w,h)
        self.p[y:y+h,x:x+w]=NAMES.index(c) if c else -1
        return self
    def dot(self,x,y,c): return self.rect(x,y,1,1,c)
    def line(self,x,y,xx,yy,c,width=1):
        # Stair steps are written as integer pixel rectangles in the SVG source.
        steps=max(abs(xx-x),abs(yy-y),1)
        for i in range(steps+1):
            px=round(x+(xx-x)*i/steps);py=round(y+(yy-y)*i/steps)
            self.rect(px,py,width,width,c)
        return self
    def oval(self,x,y,w,h,c):
        # Deliberately rasterized orthogonal contour; never an SVG ellipse/curve.
        for j in range(h):
            yy=(j+.5-h/2)/(h/2)
            span=math.sqrt(max(0,1-yy*yy))*w/2
            lo=math.ceil(w/2-span-.5); hi=math.floor(w/2+span-.5)
            if hi>=lo:self.rect(x+lo,y+j,hi-lo+1,1,c)
        return self
    def block(self,rows,x,y,c):
        for j,row in enumerate(rows):
            for i,ch in enumerate(row):
                if ch!=' ':self.dot(x+i,y+j,c)
        return self
    def final(self):
        out=self.p.copy()
        if self.outline:
            mask=out>=0
            assert not (mask[0].any() or mask[-1].any() or mask[:,0].any() or mask[:,-1].any()), self.name+' clipped outline'
            grown=np.zeros_like(mask)
            for dx,dy in ((0,0),(0,1),(0,-1),(1,0),(-1,0)):
                grown|=np.roll(np.roll(mask,dy,axis=0),dx,axis=1)
            out[grown&~mask]=NAMES.index('ink')
        return out

def build_svg(stem,sprites,cols=16):
    rows=math.ceil(len(sprites)/cols);w=cols*36;h=rows*36
    parts=[f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}" shape-rendering="crispEdges">',
           '<style>'+''.join('.c-'+n+'{fill:'+v+'}' for n,v in PALETTE.items())+'</style>']
    atlas={'width':w,'height':h,'sprites':[]}; catalog=[]
    expected=np.zeros((h,w,4),dtype=np.uint8)
    for k,s in enumerate(sprites):
        x=(k%cols)*36+2;y=(k//cols)*36+2;a=s.final()
        parts.append(f'<g id="{s.name}" transform="translate({x} {y})"><title>{escape(s.label)}</title>')
        for row in range(32):
            col=0
            while col<32:
                val=int(a[row,col]);end=col+1
                while end<32 and a[row,end]==val:end+=1
                if val>=0:
                    parts.append(f'<rect class="c-{NAMES[val]}" x="{col}" y="{row}" width="{end-col}" height="1"/>')
                    expected[y+row,x+col:x+end]=RGB[val]
                col=end
        parts.append('</g>')
        atlas['sprites'].append({'name':s.name,'x':x,'y':y,'w':32,'h':32})
        catalog.append({'name':s.name,'label':s.label,'pivot':[.5,.5],'pixelsPerUnit':32})
    parts.append('</svg>')
    return '\n'.join(parts), atlas, catalog


def export_svg(stem,sprites,cols=16):
    source,atlas,catalog=build_svg(stem,sprites,cols)
    sources=ROOT/'Sources';sources.mkdir(exist_ok=True)
    (sources/(stem+'.svg')).write_text(source,encoding='utf-8')
    (sources/(stem+'.layout.json')).write_text(json.dumps(atlas,indent=2),encoding='utf-8')
    (sources/(stem+'.catalog.json')).write_text(json.dumps(catalog,indent=2),encoding='utf-8')
    return {'atlas':stem,'sprites':len(sprites),'width':atlas['width'],'height':atlas['height']}
