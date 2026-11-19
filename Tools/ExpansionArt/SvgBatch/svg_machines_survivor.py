"""Expansion requested for direct world rendering: machines/nodes/survivor.

Machine ordinals are DemoCatalog.BuildNames. The new survivor is redrawn at
32px from the project's existing rust cap/coat, steel armour, teal visor/core,
radio pack identity, with empty hands for runtime equipment overlays;
original character files remain untouched.
"""
import numpy as np
from pixel_svg import Sprite

MACHINES=['Wall','Generator','Miner','Belt','Smelter','Assembler','Turret','Chest','PowerPole','SolarPanel','Workbench','StoneFurnace','Waterwheel','Shaft','MechanicalCrusher','Boiler','SteamPipe','SteamMiner','SteamFurnace','SteamTurbine','CircuitAssembler','DieselGenerator','ChemicalPlant','MediumPole','Transformer','HighGenerator','HighPole','PrecisionAssembler','NuclearProcessor','Reactor','CoolingTower']
MACHINE_ZH=['防护墙','燃料发电机','采矿机','传送带','熔炉','装配机','机枪塔','储物箱','电线杆','太阳能板','工作台','石炉','水车','传动轴','机械粉碎机','锅炉','蒸汽管','蒸汽采矿机','蒸汽高炉','蒸汽轮机','电路装配机','柴油发电机','化工厂','中压电杆','变压器','高压发电机','高压电杆','精密装配机','核燃料处理机','核反应堆','冷却塔']
NODES=['Tree','Ore','Scrap','Crate','Wall','CopperOre','TinOre','UraniumOre']

def base(s,x=3,y=26,w=26):
    s.rect(x,y,w,3,'dark').rect(x+1,y,w-2,1,'light')
def vent(s,x,y,w,h):
    s.rect(x,y,w,h,'dark')
    for yy in range(y+1,y+h,3):s.rect(x+1,yy,w-2,1,'light')
def pipe(s,x,y,w,h):
    s.rect(x,y,w,h,'steel')
    if w>h:s.rect(x,y,w,1,'light')
    else:s.rect(x,y,1,h,'light')
def tank(s,x,y,w,h,c='steel'):
    s.oval(x,y,w,5,'light').rect(x,y+2,w,h-4,c).oval(x,y+h-5,w,5,c).rect(x+2,y+3,1,h-6,'light')
    return s
def wheel(s,x,y,z,c='brown'):
    s.oval(x,y,z,z,c).oval(x+3,y+3,z-6,z-6,'dark')
    mid=z//2
    s.rect(x+mid-1,y,3,z,c).rect(x,y+mid-1,z,3,c)
    s.line(x+3,y+3,x+z-4,y+z-4,c).line(x+z-4,y+3,x+3,y+z-4,c)
    s.rect(x+mid-1,y+mid-1,3,3,'light')

def machine(i):
    s=Sprite('machine_'+str(i),MACHINE_ZH[i]+' / '+MACHINES[i])
    if i==0:
        s.rect(2,9,28,18,'steel').rect(2,8,28,4,'light').rect(2,24,28,4,'dark')
        s.rect(5,7,4,3,'steel').rect(23,7,4,3,'steel')
        for x,y,w in [(3,16,10),(15,16,14),(3,22,17),(22,22,7)]:s.rect(x,y,w,1,'dark')
        s.rect(13,12,1,10,'dark').rect(21,18,1,6,'dark')
    elif i==1:
        base(s);s.rect(5,10,22,16,'steel').rect(8,8,16,3,'light').rect(23,3,4,8,'dark').rect(24,3,2,2,'light')
        vent(s,7,13,9,10);s.rect(19,13,6,7,'brown').rect(20,14,4,3,'amber').rect(20,22,4,2,'teal')
        s.rect(6,25,5,3,'soil').rect(22,25,5,3,'soil')
    elif i==2:
        base(s);s.rect(5,6,4,19,'rust').rect(23,6,4,19,'rust').rect(5,5,22,4,'light').rect(13,10,8,6,'steel').rect(15,16,4,7,'light')
        s.rect(12,21,10,3,'steel').rect(14,24,6,3,'amber').rect(3,23,6,2,'steel').rect(25,23,4,2,'steel')
    elif i==3:
        s.rect(2,4,28,25,'steel').rect(4,6,24,21,'dark')
        for y in [7,12,17,22]:s.rect(5,y,22,3,'light').rect(7,y+1,18,1,'steel')
        s.rect(3,4,2,25,'rust').rect(27,4,2,25,'rust').rect(13,12,6,6,'tan').rect(15,11,2,3,'amber')
    elif i==4:
        base(s);s.rect(6,9,20,17,'rust').rect(5,7,22,4,'light').rect(8,3,5,5,'dark').rect(7,3,7,2,'steel')
        s.rect(10,13,13,11,'dark').rect(12,17,9,5,'amber').rect(15,14,4,8,'gold').rect(9,24,15,2,'steel').rect(4,15,3,7,'steel')
    elif i==5:
        base(s);s.rect(7,20,19,6,'steel').rect(4,9,4,17,'rust').rect(4,7,22,3,'light').rect(23,7,4,10,'steel')
        s.rect(12,10,6,9,'steel').rect(15,17,7,3,'light').rect(19,19,3,5,'dark').rect(9,22,10,3,'tan').rect(25,16,4,8,'brown').rect(26,17,2,3,'teal')
    elif i==6:
        s.rect(5,24,23,5,'dark').rect(7,20,19,5,'steel').oval(8,10,18,14,'shadow').rect(16,3,3,15,'light').rect(21,3,3,15,'steel').rect(15,2,10,3,'dark')
        s.rect(8,16,6,7,'rust').rect(10,17,2,4,'amber').rect(25,15,4,6,'dark')
    elif i==7:
        s.rect(3,9,26,19,'brown').rect(3,7,26,7,'tan').rect(3,13,26,2,'soil').rect(7,8,3,20,'steel').rect(23,8,3,20,'steel').rect(14,13,6,9,'dark').rect(16,16,2,3,'gold')
        s.rect(11,8,11,2,'sand').rect(4,24,24,2,'tan')
    elif i==8:
        s.rect(14,7,4,22,'brown').rect(4,7,24,3,'tan').rect(6,4,3,9,'light').rect(23,4,3,9,'light').rect(13,3,6,5,'steel').rect(10,17,12,3,'steel')
        s.rect(9,15,3,7,'cream').rect(21,15,3,7,'cream').rect(12,28,9,2,'dark')
    elif i==9:
        s.rect(7,24,3,5,'steel').rect(23,24,3,5,'steel').rect(2,6,28,20,'dark').rect(4,8,24,15,'blue')
        for x in [10,18,26]:s.rect(x,8,1,15,'light')
        for y in [12,18]:s.rect(4,y,24,1,'ice')
        s.rect(3,6,26,2,'steel').rect(6,9,3,2,'teal')
    elif i==10:
        s.rect(2,10,28,8,'tan').rect(4,18,4,11,'brown').rect(24,18,4,11,'brown').rect(5,11,22,2,'sand').rect(9,6,9,5,'steel').rect(12,4,3,4,'light').rect(22,5,3,10,'dark').rect(20,5,7,2,'amber').rect(9,23,15,3,'brown').rect(5,19,21,2,'soil')
    elif i==11:
        s.rect(6,10,21,18,'steel').rect(8,6,17,7,'light').rect(11,4,11,3,'steel').rect(11,17,12,10,'dark').rect(13,21,8,4,'rust').rect(15,18,4,7,'gold')
        for x,y,w in [(9,10,12),(7,14,9),(18,14,7),(6,24,4),(24,23,3)]:s.rect(x,y,w,1,'dark')
    elif i==12:
        # Water channel behind a large open wheel, rim paddles and stone bearings.
        s.rect(2,22,28,7,'blue').rect(2,26,28,1,'cyan').rect(4,16,4,12,'steel').rect(24,16,4,12,'steel')
        wheel(s,6,3,21,'brown')
        for x,y,w,h in [(8,2,4,3),(20,2,4,3),(26,8,3,4),(26,19,3,4),(8,23,4,3),(20,23,4,3),(3,8,3,4),(3,19,3,4)]:s.rect(x,y,w,h,'tan')
        s.rect(15,11,4,7,'light')
    elif i==13:
        s.rect(3,14,26,5,'tan').rect(7,10,4,13,'steel').rect(23,10,4,13,'steel').rect(2,13,4,7,'brown').rect(28,13,2,7,'brown').rect(12,13,9,2,'sand')
        s.rect(6,23,6,3,'dark').rect(22,23,6,3,'dark').rect(8,12,2,9,'light').rect(24,12,2,9,'light')
    elif i==14:
        base(s);s.rect(5,11,23,15,'steel').rect(3,5,26,4,'light').rect(5,9,22,4,'rust').rect(10,9,12,5,'dark')
        wheel(s,7,13,10,'tan');wheel(s,18,13,10,'steel')
        s.rect(11,4,5,3,'brown').rect(18,3,4,4,'steel').rect(2,20,4,4,'brown')
    elif i==15:
        base(s);tank(s,6,8,17,17,'rust').rect(9,3,6,7,'dark').rect(8,2,8,3,'steel')
        pipe(s,23,9,5,4);pipe(s,25,12,3,12);s.rect(11,12,7,6,'dark').rect(13,13,3,2,'cream').rect(13,20,7,4,'amber').rect(3,17,4,5,'brown')
    elif i==16:
        pipe(s,4,12,21,6);pipe(s,20,12,6,15)
        for x in [6,15]:s.rect(x,10,3,10,'light')
        s.rect(18,22,10,3,'light').rect(3,11,3,8,'steel').rect(21,8,4,5,'brown').rect(18,7,10,2,'rust').rect(21,5,4,6,'rust')
    elif i==17:
        base(s);s.rect(6,15,22,10,'rust').rect(8,11,7,5,'steel').rect(18,6,8,12,'steel').rect(20,3,4,6,'light').rect(19,10,6,2,'light').rect(21,17,3,8,'light')
        s.rect(17,23,10,3,'dark').rect(6,5,4,9,'brown').rect(4,4,8,3,'steel');pipe(s,3,15,4,10);s.rect(9,17,6,5,'dark')
    elif i==18:
        s.rect(6,24,22,5,'dark').rect(10,7,14,18,'rust').rect(9,6,16,4,'steel').rect(12,3,10,4,'steel').rect(14,2,6,2,'dark')
        s.rect(11,16,12,8,'dark').rect(14,19,6,4,'amber').rect(17,17,3,6,'gold');pipe(s,4,11,4,16);pipe(s,4,10,8,4);s.rect(24,14,4,10,'brown').rect(25,16,2,6,'light')
    elif i==19:
        base(s);tank(s,8,8,16,15,'steel').rect(4,10,5,11,'brown').rect(23,12,6,7,'light').rect(3,8,3,15,'light')
        wheel(s,12,10,11,'light');s.rect(16,11,2,9,'cyan').rect(26,9,3,13,'steel');pipe(s,5,22,4,5);pipe(s,10,24,16,3)
    elif i==20:
        base(s);s.rect(4,8,4,18,'steel').rect(24,8,4,18,'steel').rect(4,6,24,3,'light').rect(9,19,15,7,'leaf').rect(12,20,8,5,'dark').rect(14,21,4,3,'teal')
        s.rect(14,9,5,7,'steel').rect(16,15,1,5,'gold').rect(9,12,4,5,'brown').rect(21,11,3,6,'brown').rect(5,12,2,5,'amber')
    elif i==21:
        base(s);s.rect(4,13,24,12,'dark').rect(4,8,12,17,'steel');vent(s,5,10,9,12)
        for x in [17,22]:s.rect(x,10,4,12,'rust').rect(x,8,4,3,'light')
        pipe(s,18,6,8,3);pipe(s,24,3,3,6);s.rect(25,2,3,2,'dark').rect(16,24,12,2,'amber')
    elif i==22:
        s.rect(3,26,26,3,'dark');tank(s,4,9,10,17,'rust');tank(s,20,4,8,22,'steel')
        pipe(s,12,17,10,4);pipe(s,14,6,4,15);pipe(s,15,5,8,3);s.rect(6,14,6,8,'amber').rect(22,10,4,9,'cyan').rect(14,23,7,4,'brown').rect(15,24,4,2,'teal')
    elif i==23:
        s.rect(8,4,3,25,'steel').rect(23,4,3,25,'steel').rect(3,7,27,3,'brown').rect(4,17,25,3,'brown')
        for x in [5,14,26]:s.rect(x,4,2,9,'cream').rect(x-1,14,4,8,'light')
        s.line(9,22,24,10,'steel').rect(6,28,8,2,'dark').rect(21,28,8,2,'dark')
    elif i==24:
        base(s);s.rect(10,18,14,8,'steel').rect(4,18,5,7,'brown').rect(25,18,4,7,'brown')
        for x in [8,20]:
            s.rect(x,7,6,13,'brown')
            for y in [8,11,14,17]:s.rect(x-1,y,8,2,'amber')
            s.rect(x+2,3,2,5,'cream')
        s.rect(8,20,17,3,'light').rect(14,7,6,3,'steel')
    elif i==25:
        base(s);s.rect(4,13,25,12,'steel');tank(s,5,7,19,17,'steel');vent(s,7,12,14,10)
        s.rect(24,10,4,15,'rust').rect(25,3,3,8,'dark').rect(8,6,3,4,'light').rect(18,6,3,4,'light').rect(25,16,2,4,'teal').rect(4,25,24,2,'amber')
    elif i==26:
        s.rect(14,2,4,26,'steel').rect(3,8,27,3,'steel').rect(5,17,23,3,'steel').rect(11,25,12,3,'steel')
        s.line(14,11,8,29,'light').line(18,11,26,29,'light').line(13,18,22,25,'light').line(20,18,12,25,'light')
        for x in [4,14,26]:s.rect(x,5,2,8,'cream').rect(x-1,5,4,1,'light')
        s.rect(5,29,7,1,'dark').rect(23,29,6,1,'dark')
    elif i==27:
        base(s);s.rect(4,21,25,5,'light').rect(10,21,15,2,'dark').rect(13,22,7,3,'purple').rect(15,22,3,2,'teal')
        s.rect(4,8,4,12,'steel').line(7,9,13,14,'light',3).line(13,14,10,19,'steel',2).rect(9,18,4,3,'gold')
        s.rect(24,5,4,15,'steel').line(25,7,18,11,'light',3).line(18,12,22,18,'steel',2).rect(20,18,4,3,'gold').rect(3,6,6,3,'cyan').rect(23,3,6,3,'cyan')
    elif i==28:
        base(s)
        for x,y,h in [(4,9,16),(13,5,20),(23,11,14)]:
            tank(s,x,y,6,h,'steel').rect(x+2,y+4,2,h-8,'teal')
        pipe(s,6,23,20,3);s.rect(7,8,7,3,'brown').rect(19,9,6,3,'brown').rect(15,3,2,3,'gold').rect(10,26,13,1,'amber')
    elif i==29:
        s.rect(4,25,25,4,'dark').oval(5,7,22,19,'steel').oval(8,5,16,16,'light').rect(14,8,5,8,'cyan').rect(15,10,3,4,'teal').rect(10,21,14,4,'steel')
        pipe(s,2,17,5,11);pipe(s,25,16,5,12);pipe(s,3,25,25,3);s.rect(10,26,12,2,'amber').rect(16,2,2,4,'steel')
    elif i==30:
        # Hyperboloid tower: wide lip/base, narrow waist, separate vapor islands.
        s.rect(5,8,23,4,'light').rect(7,12,19,5,'steel').rect(10,17,13,5,'steel').rect(8,22,17,4,'steel').rect(5,26,23,3,'light')
        s.rect(9,9,15,2,'dark').rect(10,13,1,3,'light').rect(12,18,1,3,'light').rect(10,23,1,3,'light').rect(24,12,2,3,'dark').rect(22,19,1,3,'dark')
        s.rect(11,4,7,2,'ice').rect(16,2,8,2,'light').rect(6,6,6,1,'light')
    return s

def node(i):
    s=Sprite('node_'+str(i),NODES[i]+' world resource node')
    if i==0:
        s.rect(14,15,5,14,'brown').rect(11,27,13,3,'soil').rect(15,17,1,10,'tan')
        for x,y,w,h,c in [(9,3,15,13,'leaf'),(3,10,16,13,'leaf'),(15,10,14,14,'shadow'),(9,16,17,9,'leaf')]:s.oval(x,y,w,h,c)
        s.rect(11,5,8,2,'green').rect(5,14,6,2,'green').rect(20,13,5,2,'leaf').rect(11,20,6,2,'green')
    elif i==1:
        s.rect(3,24,26,5,'dark').rect(4,17,12,9,'steel').rect(10,10,14,16,'steel').rect(21,16,8,12,'steel')
        s.rect(12,7,8,5,'light').rect(12,10,3,9,'light').rect(5,20,7,2,'light').rect(23,19,4,3,'tan').rect(16,20,4,6,'dark')
    elif i==2:
        s.rect(4,25,25,4,'soil').rect(5,16,12,9,'rust').rect(8,12,5,12,'steel').line(9,13,25,26,'light',2).rect(20,16,8,8,'brown').rect(22,18,5,4,None).rect(3,19,5,2,'brown').rect(16,7,3,16,'rust').rect(17,10,10,4,'steel')
    elif i==3:
        s.rect(4,9,24,19,'olive').rect(3,7,26,5,'tan').rect(7,9,3,19,'steel').rect(23,9,3,19,'steel').rect(11,15,11,8,'cream').rect(15,16,3,6,'rust').rect(13,18,7,2,'rust').rect(4,25,24,3,'brown')
    elif i==4:
        s.rect(3,16,26,12,'steel').rect(5,10,10,7,'light').rect(18,8,10,9,'steel').rect(3,22,16,2,'dark').rect(21,20,8,2,'dark').rect(14,17,2,6,'dark').rect(16,25,8,3,None).rect(9,14,3,2,'brown').rect(23,9,2,4,'light')
    elif i==5:
        s.rect(3,23,26,6,'dark').rect(6,15,10,12,'brown').rect(16,8,9,18,'steel').rect(12,20,15,8,'brown').rect(18,6,5,6,'rust')
        s.rect(19,10,3,10,'amber').rect(8,18,5,3,'rust').rect(15,24,9,2,'amber').rect(26,19,3,7,'rust').rect(5,25,5,2,'light')
    elif i==6:
        s.rect(3,25,27,4,'dark').rect(4,20,24,6,'steel').rect(8,14,19,7,'light').rect(12,9,13,6,'steel').rect(16,6,7,4,'light')
        s.rect(9,17,15,2,'cream').rect(5,23,18,1,'light').rect(18,11,5,1,'cream').rect(24,21,4,2,'brown')
    elif i==7:
        s.rect(3,25,26,4,'dark').rect(6,15,7,12,'steel').rect(15,8,6,19,'steel').rect(23,18,5,9,'shadow').rect(17,4,3,7,'teal')
        s.rect(8,16,2,10,'teal').rect(17,10,2,14,'green').rect(24,20,2,6,'teal').rect(12,21,4,6,'olive').rect(5,26,6,2,'light')
    return s

def survivor(direction,frame):
    s=Sprite(f'survivor_{direction}_{frame}',f'survivor {"ENWS"[direction]} gait {frame}')
    bob=[0,-1,0,-1][frame]
    # Distinct contact/pass poses; near leg steel, far leg dark. Never recolor-only frames.
    poses=[(10,22,5,6,18,22,5,4),(12,22,4,4,19,22,4,6),
           (12,22,4,4,20,22,5,6),(11,22,4,6,17,22,4,4)]
    xa,ya,wa,ha,xb,yb,wb,hb=poses[frame]
    if direction in (0,2):
        s.rect(xa,ya,wa,ha,'dark').rect(xa,ya+ha-1,wa+1,2,'ink').rect(xa+1,ya+ha-1,2,1,'amber')
        s.rect(xb,yb,wb,hb,'steel').rect(xb,yb+hb-1,wb+1,2,'ink').rect(xb+1,yb+hb-1,2,1,'teal')
        s.rect(8,13+bob,4,10,'steel').rect(9,14+bob,2,2,'teal').rect(8,10+bob,1,4,'dark').rect(7,10+bob,3,1,'amber')
        s.rect(12,14+bob,10,10,'rust').rect(11,16+bob,3,5,'steel').rect(12,16+bob,2,1,'light').rect(12,22+bob,11,2,'dark')
        s.rect(16,15+bob,4,7,'dark').rect(17,16+bob,2,3,'teal').dot(18,17+bob,'cream')
        s.rect(13,6+bob,8,8,'cream').rect(12,6+bob,10,3,'rust').rect(13,6+bob,7,1,'amber').rect(16,4+bob,3,2,'dark').dot(17,4+bob,'teal')
        s.rect(18,10+bob,5,4,'dark').rect(19,11+bob,4,1,'teal').rect(12,11+bob,2,3,'steel').rect(21,9+bob,2,1,'amber')
        # Empty lowered hand; weapon/tool is supplied by the main renderer.
        s.rect(21,16+bob,3,5,'rust').rect(22,21+bob,3,3,'cream').dot(24,22+bob,'tan')
        if direction==2:s.p=np.fliplr(s.p).copy()
    else:
        # Root stays (16,16); depth alternates per leg without shifting the whole frame.
        a=[(11,23,6),(12,22,4),(11,22,4),(11,23,5)][frame]
        b=[(19,22,4),(19,23,6),(19,23,6),(18,22,4)][frame]
        s.rect(a[0],a[1],4,a[2],'dark').rect(a[0]-1,a[1]+a[2]-1,6,1,'amber')
        s.rect(b[0],b[1],4,b[2],'steel').rect(b[0],b[1]+b[2]-1,6,1,'teal')
        s.rect(10,14+bob,14,11,'rust').rect(8,15+bob,3,8,'steel').rect(24,15+bob,3,8,'steel').rect(11,22+bob,12,3,'dark')
        s.rect(12,6+bob,11,9,'cream' if direction==3 else 'steel').rect(11,6+bob,13,3,'rust').rect(13,6+bob,8,1,'amber').rect(16,4+bob,4,2,'dark').dot(17,4+bob,'teal')
        if direction==1:
            s.rect(11,15+bob,12,8,'dark').rect(13,16+bob,8,4,'steel').rect(14,17+bob,6,1,'teal').rect(13,22+bob,8,1,'amber')
            s.rect(17,10+bob,3,2,'dark').dot(18,11+bob,'teal')
            s.rect(8,23+bob,3,2,'cream').rect(24,23+bob,3,2,'cream')
        else:
            s.rect(12,10+bob,11,4,'dark').rect(13,11+bob,9,1,'teal').dot(14,11+bob,'cream')
            s.rect(14,15+bob,7,7,'dark').rect(15,16+bob,5,3,'teal').rect(16,17+bob,3,1,'steel').dot(17,20+bob,'cream')
            s.rect(8,23+bob,3,2,'cream').rect(24,23+bob,3,2,'cream')
    return s

def all_extended():
    return [machine(i) for i in range(31)]+[node(i) for i in range(8)]+[survivor(d,f) for d in range(4) for f in range(4)]
