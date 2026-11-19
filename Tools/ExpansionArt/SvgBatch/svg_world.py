"""Original world silhouettes and periodic terrain, integer-grid SVG source."""
import numpy as np
from pixel_svg import Sprite, NAMES
from svg_icons import leaf, berry, cob

CROPS='Carrot Potato Corn Wheat Tomato Pumpkin Beans Herb'.split()
CREATURES='Hare Deer Boar Chicken Wolf Bear Fox Goat Cattle Sheep Duck Pheasant'.split()
ENEMIES='Walker Sprinter Brute Crawler Spitter Armored Burning Screamer Hazmat Frost Bomber NightElite'.split()
ENEMY_NAMES_ZH=['普通丧尸','快跑丧尸','暴徒丧尸','爬行丧尸','喷吐丧尸','装甲丧尸','燃烧丧尸','尖啸丧尸','防化感染者','冻伤丧尸','自爆丧尸','夜行精英']
ENEMY_PREFIXES=['普通','快跑','暴徒','爬行','喷吐','装甲','燃烧','尖啸','防化','冻伤','自爆','夜行']
SITES='Residence Shop Warehouse Workshop Clinic Farmhouse GasStation PoliceStation School Apartment Greenhouse Substation'.split()
SITE_NAMES_ZH=['住宅','商店','仓库','车间','诊所','农舍','加油站','警局','学校','公寓','温室','变电站']
BIOMES='Meadow DeciduousForest ConiferousForest Desert Wetland Tundra'.split()

def crop(i,stage):
    s=Sprite(f'crop_{i}_{stage}',CROPS[i]+(' seedling' if stage==0 else ' mature'))
    # Distinct leaf architectures remain readable even before fruit appears.
    if stage==0:
        s.oval(8,24,17,4,'soil')
        if i==0:
            for x in [10,15,20]:s.line(16,25,x,15,'green').rect(x,13,2,5,'leaf')
        elif i==1:
            s.rect(15,18,2,8,'leaf');leaf(s,9,17);leaf(s,17,19,'green')
        elif i==2:
            s.rect(15,13,2,13,'green').line(15,21,8,15,'leaf',2).line(17,18,23,11,'green',2)
        elif i==3:
            for x,y in [(10,17),(15,13),(20,17)]:s.line(x,25,x+1,y,'olive').rect(x+1,y,1,4,'green')
        elif i==4:
            s.rect(15,17,2,9,'leaf');leaf(s,8,17,'green');leaf(s,18,13);s.rect(14,14,3,3,'green')
        elif i==5:
            leaf(s,6,21,'olive');leaf(s,17,20,'green');s.line(12,25,22,25,'leaf',2).rect(11,19,3,5,'leaf')
        elif i==6:
            s.rect(15,16,2,10,'leaf');s.oval(8,16,8,4,'green').oval(17,16,8,4,'green').rect(16,12,2,4,'leaf')
        elif i==7:
            for x,y in [(9,22),(15,17),(21,22)]:s.rect(x,y,2,5,'leaf').rect(x-2,y,6,2,'green')
    else:
        s.oval(5,26,23,3,'soil')
        if i==0:
            s.rect(12,24,9,4,'amber')
            for x,y in [(7,13),(12,8),(18,8),(24,13)]:s.line(16,25,x,y,'green',2).rect(x,y,2,6,'leaf')
        elif i==1:
            s.rect(15,11,2,16,'leaf')
            for x,y in [(6,18),(10,11),(18,15),(19,7)]:leaf(s,x,y,'green' if y%2 else 'leaf')
            s.oval(8,25,7,4,'tan').oval(19,24,8,4,'tan')
        elif i==2:
            s.rect(15,5,2,23,'leaf').line(14,18,6,12,'leaf',2).line(17,22,25,16,'green',2).line(17,12,23,8,'leaf')
            s.oval(11,12,5,11,'gold').rect(12,15,1,6,'amber').rect(13,3,5,2,'sand').rect(14,6,3,2,'sand')
        elif i==3:
            for x,y in [(8,9),(15,4),(23,10)]:
                s.rect(x,y+4,1,19-y,'tan')
                for d in [0,4,8]:s.rect(x-2,y+d,2,3,'gold').rect(x+1,y+d+1,2,3,'amber')
        elif i==4:
            s.rect(15,7,2,21,'brown').rect(7,15,19,2,'brown')
            for x,y in [(8,8),(18,9),(8,19),(20,18)]:leaf(s,x,y)
            for x,y in [(6,14),(18,12),(13,22)]:berry(s,x,y)
        elif i==5:
            s.line(6,25,26,25,'leaf',2).line(11,23,9,15,'leaf',2);leaf(s,4,12);leaf(s,21,17,'green')
            s.oval(9,18,15,10,'rust').rect(12,19,3,8,'amber').rect(18,19,2,8,'amber').rect(16,15,2,4,'leaf')
        elif i==6:
            s.rect(9,5,2,23,'brown').rect(23,5,2,23,'brown').rect(9,6,16,2,'tan').line(12,26,22,7,'leaf',2)
            for x,y in [(8,11),(17,9),(12,17),(18,19)]:leaf(s,x,y)
            s.line(14,12,12,18,'olive',2).line(22,16,20,23,'green',2)
        elif i==7:
            for x,y in [(8,14),(15,7),(23,15)]:
                s.rect(x,y,1,13,'leaf');leaf(s,x-4,y+2,'green');s.rect(x-1,y-2,3,3,'cream')
    return s

def legs(s,coords,c='brown'):
    for x,y,h in coords:s.rect(x,y,3,h,c).rect(x,y+h-1,4,2,'dark')
def eye(s,x,y):s.dot(x,y,'ink').dot(x-1,y-1,'cream')

def creature(i):
    s=Sprite('creature_'+str(i),CREATURES[i])
    if i==0:
        s.oval(5,15,17,11,'sand').oval(17,10,10,11,'tan').rect(19,2,3,10,'sand').rect(25,4,2,9,'sand').rect(20,4,1,6,'rose')
        s.rect(7,24,8,3,'cream').rect(23,22,5,3,'tan').oval(3,18,4,4,'cream');eye(s,24,14)
    elif i==1:
        s.oval(6,14,18,10,'tan').rect(20,10,5,10,'tan').oval(20,7,9,6,'sand');legs(s,[(8,22,7),(19,22,7)])
        s.line(21,7,19,2,'brown').line(25,7,27,2,'brown').rect(17,3,3,1,'brown').rect(27,4,3,1,'brown').rect(4,13,4,3,'cream');eye(s,26,9)
    elif i==2:
        s.oval(4,10,22,16,'soil').rect(9,8,12,3,'brown').rect(23,16,6,7,'tan').rect(24,19,6,3,'rust').rect(22,10,3,6,'brown')
        legs(s,[(8,24,4),(19,24,4)],'soil');s.rect(26,23,2,2,'cream').rect(2,14,3,2,'brown');eye(s,24,17)
    elif i==3:
        s.oval(7,12,17,14,'cream').rect(20,8,5,10,'cream').rect(21,5,2,4,'red').rect(24,7,3,2,'red').rect(25,12,4,2,'gold')
        s.rect(4,12,5,8,'brown').rect(2,8,3,9,'rust').oval(11,15,10,7,'sand').rect(13,25,1,4,'amber').rect(21,24,1,5,'amber');eye(s,23,10)
    elif i==4:
        s.oval(8,13,17,10,'steel').rect(23,9,6,10,'light').rect(23,5,2,6,'steel').rect(28,7,2,7,'steel').rect(27,16,3,3,'cream')
        s.line(8,16,3,11,'steel',3);legs(s,[(10,21,7),(22,21,7)],'steel');s.rect(10,14,10,3,'dark');eye(s,27,12)
    elif i==5:
        s.oval(4,9,23,17,'brown').oval(20,7,10,13,'tan').rect(21,5,3,4,'brown').rect(27,5,3,4,'brown').rect(25,15,5,4,'sand')
        legs(s,[(7,23,6),(21,23,6)],'soil');s.rect(9,10,10,3,'tan').rect(28,16,2,2,'dark');eye(s,25,11)
    elif i==6:
        s.oval(10,14,15,9,'rust').rect(22,9,7,10,'amber').rect(22,5,2,6,'rust').rect(28,5,2,7,'rust').rect(26,16,4,3,'cream')
        s.line(10,18,3,13,'rust',4).rect(2,10,4,5,'cream');legs(s,[(12,21,6),(23,21,6)],'soil');eye(s,27,12)
    elif i==7:
        s.oval(6,13,18,11,'cream').rect(21,10,6,12,'sand').rect(22,5,2,7,'tan').rect(26,4,2,7,'tan').rect(24,21,2,5,'cream')
        legs(s,[(8,22,7),(19,22,7)],'tan');s.rect(3,10,4,4,'cream').rect(20,12,2,4,'brown');eye(s,25,13)
    elif i==8:
        s.oval(4,10,22,14,'cream').rect(22,9,7,13,'sand').rect(24,19,6,4,'rose').rect(20,7,3,3,'cream').rect(28,7,2,3,'cream')
        s.rect(8,11,6,7,'soil').rect(17,16,5,5,'soil');legs(s,[(7,22,7),(20,22,7)],'cream');s.line(4,13,2,23,'brown');eye(s,26,13)
    elif i==9:
        for x,y in [(5,12),(11,9),(18,10),(4,18),(12,15),(18,17)]:s.oval(x,y,10,9,'cream')
        s.rect(23,13,6,10,'dark').rect(26,11,4,3,'brown');legs(s,[(8,23,6),(20,23,6)],'steel');eye(s,27,16)
    elif i==10:
        s.oval(5,16,20,10,'brown').rect(22,11,5,10,'leaf').oval(20,8,8,7,'green').rect(26,12,4,3,'amber').rect(4,13,5,6,'tan')
        s.oval(9,17,11,6,'steel').rect(14,26,6,2,'amber').rect(23,24,5,2,'amber');eye(s,25,10)
    elif i==11:
        s.line(13,20,2,6,'brown',3).line(11,20,2,12,'tan',2).oval(10,13,16,12,'rust').rect(22,8,5,12,'leaf').rect(24,6,4,4,'teal').rect(27,10,3,2,'gold')
        s.oval(13,16,8,6,'tan').rect(17,24,1,5,'amber').rect(24,23,1,6,'amber');eye(s,26,9)
    return s

def humanoid(s,c='shadow',head='tan',pose=0):
    s.rect(12,5,8,7,head).rect(10,13,12,9,c).rect(11,22,4,7,'dark').rect(18,22,4,7,'steel')
    if pose==0:s.rect(7,14,3,12,c).rect(23,13,3,11,c)
    elif pose==1:s.line(10,14,5,11,c,3).line(22,16,26,22,c,3)
    else:s.rect(5,15,5,5,c).rect(22,12,5,5,c)
    s.rect(13,8,2,1,'amber').rect(18,8,2,1,'amber')

def enemy(i):
    # Ordinal is the Core ZombieState.variant contract, never an art-only order.
    s=Sprite('enemy_'+str(i),ENEMIES[i]+' / '+ENEMY_NAMES_ZH[i])
    if i==0:
        humanoid(s);s.rect(11,14,3,5,'brown').rect(18,21,3,4,'rust').rect(14,5,5,2,'steel')
    elif i==1:
        s.oval(18,4,7,7,'tan').line(12,14,20,10,'rust',4).line(12,15,5,19,'steel',3).line(17,17,25,18,'steel',2)
        s.line(12,18,8,25,'dark',3).line(8,25,3,27,'dark',2).line(17,18,20,25,'steel',3).line(20,25,28,25,'steel',2).dot(23,7,'amber')
    elif i==2:
        s.rect(11,3,11,8,'brown').rect(6,12,21,13,'soil').rect(3,14,5,13,'brown').rect(25,12,5,15,'brown').rect(8,25,7,4,'dark').rect(20,25,7,4,'dark')
        s.rect(11,14,13,4,'tan').rect(14,7,2,2,'amber').rect(19,7,2,2,'amber').rect(15,20,5,2,'rust')
    elif i==3:
        s.oval(9,13,14,10,'rust').oval(20,10,8,8,'tan').rect(23,12,2,1,'amber').line(11,18,3,23,'brown',3).line(19,20,25,27,'brown',3).line(12,21,8,28,'steel',2).line(20,17,28,20,'steel',2).rect(4,13,7,4,'dark')
    elif i==4:
        humanoid(s,'olive','leaf',2);s.oval(5,12,9,9,'green').rect(10,7,5,3,'olive').rect(14,11,10,2,'teal').rect(24,19,4,5,'green').rect(27,25,2,3,'teal')
    elif i==5:
        humanoid(s,'steel','steel');s.rect(12,8,9,2,'red').rect(4,14,11,15,'steel').rect(6,16,7,11,'dark').rect(8,18,3,7,'amber').rect(24,12,3,14,'light').rect(23,11,5,3,'brown')
    elif i==6:
        humanoid(s,'soil','dark',1);s.rect(11,14,2,6,'rust').rect(17,17,3,5,'amber').rect(13,7,2,1,'gold').rect(18,7,2,1,'gold').rect(10,3,2,4,'red').rect(21,10,2,4,'amber').rect(5,8,2,2,'amber')
        # Visible flames, rather than a merely dark/charred clothing recolor.
        s.rect(7,15,3,6,'rust').rect(8,13,2,6,'amber').dot(9,12,'gold')
        s.rect(20,19,4,6,'rust').rect(21,17,2,6,'gold').dot(22,16,'cream')
    elif i==7:
        # Upright infected, exposed throat, large open jaw and stepped cry marks.
        s.rect(11,15,11,9,'shadow').rect(12,24,3,5,'dark').rect(19,24,3,5,'steel')
        s.rect(13,12,7,6,'rose').rect(12,4,9,10,'tan').rect(13,7,2,1,'amber').rect(18,7,2,1,'amber')
        s.rect(14,9,6,6,'ink').rect(14,9,6,1,'cream').rect(15,14,4,1,'cream').rect(15,15,4,3,'rust')
        s.line(11,17,7,11,'steel',2).rect(7,7,2,6,'tan').line(22,17,25,11,'steel',2).rect(25,7,2,6,'tan')
        s.rect(3,7,1,8,'light').rect(2,9,1,4,'light').rect(29,7,1,8,'light').rect(30,9,1,4,'light')
    elif i==8:
        humanoid(s,'amber','amber');s.rect(11,5,10,6,'dark').rect(12,6,8,3,'cyan').rect(14,10,6,4,'steel').rect(14,15,5,7,'rust').rect(8,24,3,3,'steel').rect(23,24,4,3,'steel')
    elif i==9:
        humanoid(s,'blue','ice');s.rect(11,3,3,5,'ice').rect(19,2,3,6,'teal').rect(8,13,3,4,'ice').rect(22,13,3,4,'ice').rect(12,15,7,3,'teal').rect(8,25,3,4,'ice').rect(23,22,3,6,'ice')
    elif i==10:
        # Two-legged zombie visibly carrying a strapped cartoon explosive bundle.
        humanoid(s,'brown','tan');s.rect(12,5,7,2,'steel').rect(15,10,4,2,'rust')
        s.rect(9,14,15,11,'dark')
        for x in [10,15,20]:s.rect(x,15,3,9,'red').rect(x,15,3,2,'amber').rect(x+1,18,1,4,'rose')
        s.rect(9,21,15,2,'ink').rect(15,20,3,3,'steel').dot(16,20,'gold')
        s.rect(7,18,3,4,'tan').rect(24,18,3,4,'tan').rect(23,11,2,4,'brown').rect(24,10,2,2,'gold').dot(27,8,'amber')
    elif i==11:
        # Hooded humanoid elite: luminous eyes, exposed teeth and hooked claws.
        # Broken antler-like trophies retain identity, not a new animal/drone type.
        s.rect(10,7,13,10,'dark').rect(12,9,9,7,'steel').rect(7,17,19,10,'shadow')
        s.rect(4,18,4,9,'dark').rect(26,17,3,10,'dark').rect(11,26,4,4,'dark').rect(20,26,4,4,'steel')
        s.line(12,8,7,3,'brown',2).rect(5,2,3,3,'tan').rect(7,5,1,3,'tan').line(21,8,26,3,'brown',2).rect(26,2,3,2,'tan')
        s.rect(13,10,3,2,'teal').rect(19,10,3,2,'teal').rect(14,14,7,2,'ink')
        for x in [14,17,20]:s.dot(x,14,'cream')
        s.rect(14,19,8,6,'purple').rect(15,20,2,4,'dark').rect(3,26,4,2,'cream').rect(26,26,4,2,'cream')
    return s

def bolt(s,x,y):s.rect(x,y,1,1,'light')
def site(i):
    # Entrances/front facades, ordered by WorldGenerationCatalog.Sites.
    # Industrial devices are a separate machine_* contract.
    s=Sprite('site_'+str(i),SITES[i]+' / '+SITE_NAMES_ZH[i]+' entrance')
    if i==0:
        # Gabled cottage, chimney, porch lamp and domestic window.
        s.rect(6,12,21,17,'tan').rect(22,4,3,7,'soil')
        for x,y,w in [(13,4,7),(10,6,13),(7,8,19),(4,10,25)]:s.rect(x,y,w,3,'rust')
        s.rect(7,13,19,1,'sand').rect(17,19,6,10,'dark').dot(21,24,'gold')
        s.rect(9,17,5,6,'blue').rect(11,17,1,6,'light').rect(9,19,5,1,'light')
        s.rect(16,17,9,2,'brown').rect(6,28,22,2,'steel').dot(25,20,'gold')
    elif i==1:
        # Broad shopfront, striped canvas awning, display and glazed entry.
        s.rect(4,7,25,22,'tan').rect(5,5,23,4,'dark').rect(11,6,11,2,'gold')
        s.rect(3,11,27,6,'cream')
        for x in [4,10,16,22,28]:s.rect(x,11,2,6,'rust')
        s.rect(6,19,12,8,'dark').rect(7,20,10,5,'cyan')
        s.rect(8,23,3,2,'sand').rect(13,22,3,3,'leaf')
        s.rect(21,18,6,11,'dark').rect(22,19,4,6,'blue').dot(25,26,'amber').rect(4,28,25,2,'soil')
    elif i==2:
        # Wide loading bay with corrugated shutter and lintel windows.
        s.rect(3,10,27,19,'steel').rect(5,7,23,3,'light').rect(10,5,13,2,'light')
        s.rect(6,12,21,4,'dark')
        for x in [7,12,17,22]:s.rect(x,13,3,2,'cyan')
        s.rect(7,18,20,10,'dark').rect(9,19,16,8,'light')
        for y in [21,24]:s.rect(9,y,16,1,'steel')
        s.rect(4,27,25,3,'soil').rect(4,23,4,4,'brown').rect(5,24,2,1,'sand')
    elif i==3:
        # Sawtooth factory roof and repair-bay frontage with spanner sign.
        s.rect(3,12,26,17,'brown')
        for x in [3,12,21]:
            s.rect(x,10,8,4,'steel').rect(x+3,7,5,3,'steel').rect(x+6,5,2,4,'light')
        s.rect(5,18,12,11,'dark').rect(6,19,10,8,'steel')
        for y in [21,24]:s.rect(6,y,10,1,'light')
        s.rect(20,20,6,9,'dark').rect(19,13,8,5,'ink')
        s.rect(21,14,4,1,'cream').rect(20,15,3,2,'cream').rect(23,16,2,1,'cream')
        s.rect(3,28,26,2,'soil')
    elif i==4:
        # Pale clinic, high medical cross and double glass doors.
        s.rect(5,9,23,20,'light').rect(4,7,25,3,'cream').rect(12,3,9,9,'cream')
        s.rect(15,4,3,7,'red').rect(13,6,7,3,'red')
        s.rect(12,18,9,11,'dark').rect(13,19,7,8,'cyan').rect(16,19,1,9,'light')
        s.rect(7,16,3,6,'blue').rect(23,16,3,6,'blue').rect(4,28,25,2,'steel')
    elif i==5:
        # Gambrel barn front, loft and cross-braced timber doors.
        s.rect(6,13,21,16,'rust').rect(4,11,25,4,'brown').rect(7,7,19,4,'brown').rect(11,4,11,3,'tan')
        s.rect(14,10,6,5,'dark').rect(15,11,4,3,'gold').rect(10,18,14,11,'dark')
        s.rect(11,19,12,9,'brown').rect(16,19,1,9,'sand')
        s.line(11,19,16,27,'sand').line(22,19,17,27,'sand')
        s.rect(4,25,5,4,'gold').rect(4,26,5,1,'tan').rect(6,25,1,4,'tan')
    elif i==6:
        # Fuel canopy over forecourt, pump and attached convenience kiosk.
        s.rect(18,14,11,15,'tan').rect(17,12,13,3,'rust').rect(23,18,4,11,'dark')
        s.rect(19,18,3,5,'cyan').rect(4,10,2,18,'steel').rect(15,10,2,18,'steel')
        s.rect(2,7,27,5,'cream').rect(2,10,27,2,'rust').rect(11,8,9,1,'gold')
        s.rect(7,19,5,9,'red').rect(8,20,3,3,'light').rect(8,24,3,1,'dark')
        s.rect(12,20,2,1,'dark').rect(13,21,1,5,'dark').rect(3,28,26,2,'soil')
    elif i==7:
        # Civic precinct entrance, shield badge, paired blue warning lights.
        s.rect(5,10,23,19,'steel').rect(4,8,25,3,'light').rect(11,5,12,9,'dark')
        s.rect(14,6,6,5,'gold').rect(15,11,4,2,'gold').rect(16,7,2,4,'blue')
        s.rect(12,19,10,10,'dark').rect(13,20,8,7,'blue').rect(17,20,1,8,'light')
        for x in [7,24]:s.rect(x,17,2,6,'cyan').rect(x,14,2,2,'teal')
        s.rect(3,29,27,1,'light')
    elif i==8:
        # Low school wings, central bell tower and open-book entry plaque.
        s.rect(3,14,27,15,'tan').rect(2,12,28,3,'rust').rect(12,8,10,21,'sand')
        s.rect(11,7,12,3,'brown').rect(14,3,6,5,'brown').rect(15,4,4,3,'gold')
        s.rect(14,22,6,7,'dark').rect(13,14,4,5,'cream').rect(18,14,4,5,'cream').rect(17,15,1,5,'soil')
        for x in [5,25]:s.rect(x,18,3,5,'blue').rect(x,20,3,1,'light')
        s.rect(10,28,14,2,'steel')
    elif i==9:
        # Tall, narrow multi-storey apartment block with repeated balconies.
        s.rect(7,4,20,25,'brown').rect(6,3,22,3,'steel')
        for y in [8,14,20]:
            for x in [10,20]:s.rect(x,y,4,4,'blue').rect(x,y,4,1,'light')
            s.rect(8,y+4,17,1,'tan')
        s.rect(15,24,5,5,'dark').rect(14,23,7,1,'steel').dot(18,27,'gold')
        s.rect(6,29,22,1,'light')
    elif i==10:
        # Stepped glazed arch, structural mullions, planted rows and glass door.
        s.rect(4,12,25,17,'blue').rect(7,8,19,5,'cyan').rect(11,5,11,4,'cyan')
        s.rect(11,4,11,1,'light').rect(7,7,19,1,'light').rect(4,11,25,1,'light')
        for x in [4,10,22,28]:s.rect(x,12,1,16,'light')
        s.rect(16,5,1,24,'light').rect(4,17,25,1,'light').rect(4,27,25,2,'brown')
        for x in [6,23]:s.rect(x,23,4,3,'soil').rect(x+1,20,2,4,'leaf').rect(x,21,4,1,'green')
        s.rect(13,19,8,10,'dark').rect(14,20,6,7,'cyan').rect(16,20,1,8,'light').dot(19,24,'gold')
    elif i==11:
        # Fenced utility compound entrance, control hut and hazard gate plaque.
        s.rect(5,11,10,17,'steel').rect(4,9,12,3,'light').rect(7,14,5,4,'cyan').rect(8,21,5,7,'dark')
        s.rect(20,6,2,13,'brown').rect(17,6,10,2,'steel')
        for x in [18,24]:s.rect(x,4,2,6,'light').rect(x-1,5,4,1,'cream')
        for x in [3,16,28]:s.rect(x,18,1,12,'steel')
        s.rect(3,20,26,1,'light').rect(3,28,26,1,'light')
        for x in [6,10,13,19,23,26]:s.rect(x,21,1,7,'steel')
        s.rect(17,21,9,6,'gold').rect(21,21,2,2,'ink').rect(20,23,2,2,'ink').dot(21,25,'ink')
    return s

def biome(i):
    s=Sprite('biome_'+str(i),BIOMES[i]+' ecology cluster')
    if i==0:
        for x,y in [(7,20),(13,17),(22,22)]:s.line(x,y+7,x+1,y,'green').line(x,y+7,x-3,y+2,'leaf')
        s.rect(10,11,1,13,'leaf').rect(8,11,5,2,'cream').rect(10,10,2,4,'gold').rect(23,16,3,2,'amber')
    elif i==1:
        s.rect(8,17,11,10,'brown').oval(7,14,14,7,'tan').rect(10,16,8,2,'sand').rect(13,16,3,3,'brown')
        for x,y in [(20,20),(22,13),(3,22)]:leaf(s,x,y)
        s.rect(21,25,5,2,'rust')
    elif i==2:
        s.rect(14,19,4,10,'brown')
        for y,x,w in [(4,14,4),(8,11,10),(13,8,16),(18,5,22)]:s.rect(x,y,w,4,'leaf').rect(x+1,y,w-2,1,'green')
        s.oval(23,25,5,4,'brown')
    elif i==3:
        s.rect(13,6,6,22,'leaf').rect(7,12,4,10,'leaf').rect(9,19,5,4,'leaf').rect(21,9,4,12,'green').rect(18,18,6,4,'leaf').rect(15,7,1,18,'green').rect(21,26,8,3,'tan').dot(16,12,'cream')
    elif i==4:
        for x,y in [(7,8),(15,5),(24,10)]:s.rect(x,y,1,21,'leaf').rect(x-1,y,3,7,'brown').rect(x-1,y,3,2,'rust').line(x,25,x-4,17,'green')
        s.rect(6,28,20,1,'cyan')
    elif i==5:
        s.rect(6,22,19,6,'steel').rect(9,18,13,7,'light').rect(7,21,17,2,'ice').rect(11,18,9,2,'cream')
        s.rect(4,25,7,3,'ice').rect(23,26,5,2,'ice').rect(14,15,1,5,'cyan').rect(12,16,5,1,'cyan')
    return s

def terrain_field(kind,x,y):
    """Periodic material formula, valid at arbitrary world pixel coordinates.

    Opposing sample edges are equal too. Allowed adjacency is same-biome only;
    cross-biome transitions/autotiles require a later art pass.
    """
    x=x%32;y=y%32;x=np.where(x==31,0,x);y=np.where(y==31,0,y)
    g=(x*17+y*29+x*y*7)%71
    if kind==0:
        a=np.full(x.shape,NAMES.index('shadow'));a[g<14]=NAMES.index('leaf')
        a[((x+3*y)%19==0)&(y%5<2)]=NAMES.index('green')
        a[(x%13==4)&(y%11==6)]=NAMES.index('tan')
    elif kind==1:
        a=np.full(x.shape,NAMES.index('soil'));a[g<11]=NAMES.index('brown')
        leafpat=((x+2*y)%17<4)&((2*x-y)%13<3)
        a[leafpat]=NAMES.index('rust');a[leafpat&(g%3==0)]=NAMES.index('tan')
        a[((x-y)%23==0)&(x%6<4)]=NAMES.index('shadow')
    elif kind==2:
        a=np.full(x.shape,NAMES.index('dark'));a[g<9]=NAMES.index('soil')
        a[((x+y*2)%17<2)&(x%9<6)]=NAMES.index('leaf')
        a[((x-y)%29==0)&(y%9<6)]=NAMES.index('brown')
        a[((2*x+y)%31==0)&(g<30)]=NAMES.index('olive')
    elif kind==3:
        a=np.full(x.shape,NAMES.index('sand'));a[g<7]=NAMES.index('tan')
        bands=(y+(x//8)%2)%12
        a[bands==4]=NAMES.index('tan');a[(bands==5)&(x%11<8)]=NAMES.index('cream')
        a[(x%23==9)&(y%19==11)]=NAMES.index('brown')
    elif kind==4:
        a=np.full(x.shape,NAMES.index('shadow'))
        water=(((x-9)%32-16)**2+(((y-5)%32-16)*2)**2<80)|(((x+6)%32-16)**2+((y+8)%32-16)**2<35)
        a[water]=NAMES.index('blue');a[water&(y%5==0)]=NAMES.index('cyan')
        a[(~water)&(g<12)]=NAMES.index('olive');a[(~water)&(x%9==3)&(y%6<2)]=NAMES.index('leaf')
    else:
        a=np.full(x.shape,NAMES.index('light'));a[g<5]=NAMES.index('steel')
        dx=(x-6+16)%32-16;dy=(y-9+16)%32-16
        ex=(x-25+16)%32-16;ey=(y-27+16)%32-16
        snow=(dx*dx+2*dy*dy<76)|(2*ex*ex+ey*ey<63)
        a[snow]=NAMES.index('ice');a[snow&(g<8)]=NAMES.index('cream')
        rocks=((x>=19)&(x<=23)&(y>=16)&(y<=18))|((x>=9)&(x<=11)&(y>=25)&(y<=26))
        a[rocks]=NAMES.index('steel');a[rocks&(y%3==1)]=NAMES.index('dark')
    return a

def terrain(i):
    s=Sprite('terrain_'+str(i),BIOMES[i]+' seamless opaque ground',False)
    yy,xx=np.indices((32,32));s.p=terrain_field(i,xx,yy).astype(np.int16)
    return s

def deco(i):
    labels=['berry shrub','felled stump','fallen log','ore outcrop','reed fan','mushroom patch','dead tree','hazard barrel']
    s=Sprite('deco_'+str(i),labels[i])
    if i==0:
        for x,y in [(6,16),(12,11),(19,15),(10,21)]:s.oval(x,y,10,8,'leaf')
        for x,y in [(9,18),(18,16),(17,22)]:s.rect(x,y,2,2,'red')
        s.rect(15,26,3,3,'brown')
    elif i==1:
        s.rect(8,14,16,13,'brown').oval(6,10,20,9,'tan').oval(10,12,12,5,'sand').rect(12,14,8,1,'brown').rect(7,25,4,4,'soil').rect(23,25,4,4,'soil')
    elif i==2:
        s.rect(5,12,23,12,'brown').rect(7,13,19,2,'tan').oval(3,12,9,12,'sand').oval(5,14,5,8,'tan').rect(7,16,1,4,'brown').rect(18,9,3,5,'soil')
    elif i==3:
        s.rect(5,18,23,10,'steel').rect(8,12,15,10,'steel').rect(16,8,8,9,'light').rect(7,20,7,3,'rust').rect(18,13,3,6,'amber').rect(20,23,5,3,'brown')
    elif i==4:
        for x,y in [(6,11),(10,6),(15,3),(20,7),(25,12)]:s.line(16,28,x,y,'leaf').rect(x,y,1,5,'green')
    elif i==5:
        for x,y,z in [(5,18,8),(15,11,12),(20,19,8)]:
            s.rect(x+z//2-1,y+4,3,7,'cream').oval(x,y,z,6,'rust').rect(x+2,y+1,2,1,'amber')
    elif i==6:
        s.rect(14,8,5,21,'soil').line(15,17,7,9,'brown',2).line(17,12,23,6,'brown',2).rect(6,5,2,7,'brown').rect(23,3,2,7,'brown').rect(15,2,3,9,'brown').rect(10,28,13,2,'soil')
    elif i==7:
        s.rect(8,6,17,22,'steel').rect(7,5,19,3,'light').rect(7,25,19,3,'dark').rect(9,13,15,8,'amber').line(10,14,14,20,'ink',2).line(18,14,22,20,'ink',2).rect(11,9,1,3,'light').rect(20,8,3,2,'brown')
    return s

FX=['rain','snow','dust','fog','muzzle','spark','flame','ice','soil','poison','wave','lightning','ember','fire_large','frost_cloud','ice_shard','hit_flesh','hit_metal','hit_wood','muzzle_shotgun','muzzle_rifle','muzzle_sniper','explosion','smoke']
def burst(s,c,arms):
    for x,y,xx,yy,w in arms:s.line(x,y,xx,yy,c,w)

def fx(i):
    name=FX[i];s=Sprite('fx_'+name,name+' static FX')
    if name=='rain':
        for x,y,h in [(6,4,9),(17,8,12),(26,3,8),(10,22,5)]:s.line(x,y,x-2,y+h,'cyan').rect(x-2,y+h,1,2,'ice')
    elif name=='snow':
        for x,y in [(8,8),(23,12),(13,24)]:s.rect(x-2,y,5,1,'ice').rect(x,y-2,1,5,'cream').dot(x-1,y-1,'ice').dot(x+1,y+1,'ice')
    elif name=='dust':
        for x,y,w in [(4,23,12),(12,17,16),(6,11,15),(19,6,8)]:s.rect(x,y,w,2,'tan').rect(x+2,y-1,w-4,1,'brown')
        s.rect(3,17,2,2,'sand').rect(25,26,3,1,'tan')
    elif name=='fog':
        for x,y,w in [(3,10,22),(7,14,22),(4,18,19),(8,22,17)]:s.rect(x,y,w,2,'light').rect(x+3,y+2,w-5,1,'steel')
    elif name in ('muzzle','muzzle_shotgun','muzzle_rifle','muzzle_sniper'):
        if name=='muzzle':
            s.rect(5,14,10,5,'amber').rect(12,10,6,12,'gold').rect(18,13,9,6,'amber').rect(14,13,10,5,'cream').rect(20,8,3,3,'gold').rect(22,22,4,2,'amber')
        elif name=='muzzle_shotgun':
            burst(s,'amber',[(5,16,25,5,3),(5,16,29,14,2),(5,16,25,26,3)])
            s.rect(6,13,10,7,'gold').rect(8,15,8,3,'cream')
        elif name=='muzzle_rifle':
            s.rect(3,15,26,3,'amber').rect(5,14,21,5,'gold').rect(8,15,17,2,'cream').rect(13,9,2,5,'gold').rect(15,19,2,5,'gold')
        else:
            s.rect(2,15,28,2,'cream').rect(6,14,21,1,'gold').rect(7,17,16,1,'amber').rect(20,12,6,2,'gold').rect(23,18,5,2,'amber').rect(4,10,2,3,'light')
    elif name=='spark':
        burst(s,'gold',[(13,17,5,9,1),(16,13,20,4,1),(19,17,28,14,1),(18,20,24,26,1),(12,21,7,27,1)])
        s.rect(13,14,5,6,'cream').rect(11,16,9,2,'gold').rect(25,7,2,2,'amber')
    elif name in ('flame','fire_large'):
        large=name=='fire_large';s.rect(10 if not large else 6,22,14 if not large else 21,6,'rust').rect(8,18,18,8,'rust').rect(11,12,13,12,'amber').rect(16,6 if large else 9,6,14,'amber').rect(18,3 if large else 7,3,8,'gold').rect(13,19,9,7,'gold').rect(16,20,4,6,'cream')
        if large:s.rect(6,12,3,8,'amber').rect(26,17,2,7,'amber').rect(10,8,2,3,'rust')
    elif name=='ice':
        s.rect(14,5,3,23,'ice').rect(5,15,22,3,'ice').line(7,7,24,24,'cyan').line(7,24,24,7,'cyan').rect(12,12,7,7,'cream').rect(14,14,3,3,'teal')
    elif name=='soil':
        s.rect(6,25,21,3,'soil').rect(11,22,12,4,'brown')
        for x,y,w,h in [(4,17,4,3),(12,12,4,4),(22,17,5,3),(9,5,2,2),(23,7,3,2)]:s.rect(x,y,w,h,'tan')
    elif name=='poison':
        for x,y,z in [(4,18,11),(11,10,14),(20,17,9)]:s.oval(x,y,z,z,'leaf')
        s.rect(10,22,13,4,'olive').oval(6,6,4,4,'green').oval(21,3,5,5,'teal').dot(14,16,'teal').rect(17,20,3,2,'green')
    elif name=='wave':
        for x,y,w in [(4,12,9),(15,10,12),(6,20,14),(22,19,7)]:s.rect(x,y,w,2,'cyan').rect(x+1,y-2,w-3,2,'ice')
        s.rect(13,15,5,1,'teal').rect(3,26,13,1,'blue')
    elif name=='lightning':
        s.rect(17,2,7,4,'ice').rect(14,6,8,5,'ice').rect(11,11,10,5,'cream').rect(15,16,6,3,'gold').rect(12,19,6,5,'gold').rect(9,24,5,5,'amber')
    elif name=='ember':
        for x,y,w,h in [(6,21,2,5),(15,12,3,4),(24,5,2,4),(21,23,2,3),(9,6,1,2)]:s.rect(x,y,w,h,'rust').rect(x,y,1,h-1,'gold')
    elif name=='frost_cloud':
        s.oval(3,18,17,10,'blue').oval(10,11,16,14,'ice').oval(21,19,8,8,'cyan').rect(7,23,13,2,'ice').rect(9,8,1,5,'cream').rect(7,10,5,1,'cream').rect(23,7,2,2,'ice')
    elif name=='ice_shard':
        s.rect(13,3,5,6,'cream').rect(11,9,8,13,'ice').rect(13,22,5,6,'cyan').rect(11,13,3,6,'teal').rect(23,15,3,9,'blue').rect(6,20,2,8,'ice')
    elif name=='hit_flesh':
        s.rect(11,12,10,9,'red').rect(8,15,15,4,'rust').line(8,11,4,6,'red',2).line(23,12,27,8,'rose',2).rect(25,24,3,3,'red').rect(8,25,2,4,'rust').rect(13,14,3,2,'rose')
    elif name=='hit_metal':
        s.rect(12,13,8,8,'steel').rect(14,15,4,4,'dark');burst(s,'cream',[(10,12,5,5,1),(22,12,27,6,1),(24,19,29,22,1),(12,24,8,28,1)])
        s.rect(20,23,2,3,'amber')
    elif name=='hit_wood':
        for x,y,w,h in [(12,12,7,10),(4,7,3,7),(24,5,3,6),(25,23,4,3),(6,22,3,5)]:s.rect(x,y,w,h,'tan').rect(x,y,1,h,'brown')
    elif name=='explosion':
        s.rect(8,6,18,18,'rust').rect(4,10,24,13,'rust').rect(9,24,15,4,'brown').rect(10,8,13,15,'amber').rect(7,13,19,7,'gold').rect(13,12,7,8,'cream').rect(3,4,3,3,'amber').rect(26,27,3,2,'rust')
    elif name=='smoke':
        s.oval(8,19,19,10,'steel').oval(4,12,18,12,'light').oval(12,4,15,14,'steel').rect(8,15,10,4,'light').rect(14,24,8,3,'shadow').rect(14,2,3,2,'light')
    return s

def all_world():
    return ([terrain(i) for i in range(6)]+[biome(i) for i in range(6)]+
            [site(i) for i in range(12)]+[crop(i,j) for i in range(8) for j in range(2)]+
            [creature(i) for i in range(12)]+[enemy(i) for i in range(12)]+
            [deco(i) for i in range(8)]+[fx(i) for i in range(len(FX))])
