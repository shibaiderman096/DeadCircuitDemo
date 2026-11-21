"""Hand-authored silhouettes, ordered to match the DeadCircuit catalogs."""
from pixel_svg import Sprite

FOODS='Berries Mushroom Apple Carrot Potato Corn Wheat Tomato Pumpkin Beans RawMeat RawPoultry RawFish Egg RoastMeat RoastPoultry GrilledFish BoiledEgg BakedPotato RoastedCorn Bread VegetableStew MeatStew FishSoup FruitSalad Flour DriedMeat CookedBeans PumpkinSoup HerbalTea Herb'.split()
GEAR='StoneAxe StonePickaxe SteelAxe SteelPickaxe Wrench Knife Pistol Shotgun AssaultRifle PrecisionRifle ClothHood LeatherJacket WorkPants WorkBoots SteelHelmet ArmorVest TacticalHelmet TacticalVest TacticalBoots'.split()
PARTS='RedDot Scope4x Suppressor MuzzleBrake ExtendedMag ErgonomicGrip MatchBarrel'.split()
ITEMS='Wood Ore Iron Scrap Fuel Ammo Food CopperOre Copper Bronze Steel Circuit Polymer AdvancedCircuit UraniumOre NuclearFuel Coolant NuclearWaste CrushedOre TinOre Tin RifleAmmo Shell PrecisionAmmo ArmorPiercingAmmo'.split()

def leaf(s,x,y,c='leaf'):
    s.rect(x+1,y,4,2,c).rect(x,y+2,6,2,c).rect(x+2,y+4,2,1,c)
def berry(s,x,y,c='red'):
    s.oval(x,y,6,6,c).rect(x+1,y+1,2,1,'rose')
def steam(s,x,y):
    s.rect(x,y,1,3,'light').rect(x+1,y-2,1,2,'light')
def bowl(s,c='steel',deep=False):
    s.rect(4,15,24,4,'light').rect(6,19,20,5 if deep else 3,c)
    s.rect(9,24 if deep else 22,14,2,'dark').rect(6,15,20,2,'soil')
def fish(s,x=6,y=10,c='blue'):
    s.oval(x,y,17,10,c).rect(x-3,y,3,2,c).rect(x-4,y+1,3,8,c)
    s.rect(x+5,y-2,5,2,c).rect(x+8,y+10,4,2,c).dot(x+14,y+3,'cream').dot(x+15,y+3,'ink')
    s.rect(x+3,y+5,7,2,'light')
def cob(s,x=12,y=8,roast=False):
    s.oval(x,y,9,17,'amber' if roast else 'gold')
    for yy in range(y+2,y+15,4):
        s.rect(x+2,yy,2,2,'brown' if roast else 'amber').rect(x+6,yy+1,1,2,'cream')
def herb(s):
    s.line(13,27,18,7,'brown',2)
    for x,y in [(9,18),(17,16),(10,10),(18,6)]:leaf(s,x,y)
    s.rect(17,4,2,3,'green')

def food(i):
    s=Sprite('food_'+str(i),FOODS[i])
    if i==0:
        leaf(s,10,5);leaf(s,17,7,'green')
        for x,y in [(8,12),(15,11),(11,18),(18,18)]:berry(s,x,y)
    elif i==1:
        s.rect(13,16,7,11,'sand').rect(17,17,3,9,'tan').oval(4,6,24,13,'rust').rect(5,15,22,3,'amber')
        s.rect(10,9,3,2,'cream').rect(20,11,3,2,'cream')
    elif i==2:
        s.oval(6,10,12,17,'red').oval(15,9,11,18,'red').rect(10,13,2,6,'rose').rect(15,5,2,7,'brown');leaf(s,18,4)
    elif i==3:
        s.rect(11,10,10,6,'amber').rect(12,16,8,5,'amber').rect(14,21,5,4,'rust').rect(15,25,2,3,'amber')
        s.rect(14,5,3,7,'green').rect(9,4,3,5,'leaf').rect(19,3,3,6,'leaf').rect(12,13,4,1,'cream').rect(15,19,4,1,'brown')
    elif i==4:
        s.oval(4,11,24,15,'tan').rect(9,10,12,4,'tan').rect(8,15,4,2,'sand')
        for x,y in [(16,13),(24,18),(12,22),(19,21)]:s.rect(x,y,2,1,'brown')
    elif i==5:
        cob(s);s.line(9,15,13,27,'leaf',3).line(23,14,19,27,'green',3).rect(15,4,3,5,'sand')
    elif i==6:
        s.line(12,27,16,6,'tan').line(19,27,22,7,'brown')
        for y in [7,11,15,19]:
            s.rect(12,y,4,2,'gold').rect(16,y+2,3,2,'amber').rect(20,y+1,5,2,'sand')
    elif i==7:
        s.oval(4,11,24,15,'red').rect(8,14,3,4,'rose').rect(14,7,3,7,'leaf').rect(9,9,14,2,'green').rect(12,11,9,2,'leaf')
    elif i==8:
        s.oval(3,10,26,17,'rust').oval(7,10,18,17,'amber').oval(12,9,8,19,'rust').rect(15,5,3,6,'leaf').rect(15,12,2,12,'amber')
    elif i==9:
        s.line(6,20,11,11,'green',4).line(11,11,16,7,'leaf',3).line(17,25,23,16,'olive',4)
        for x,y in [(8,17),(11,12),(19,22),(22,17)]:s.rect(x,y,2,3,'gold')
    elif i==10:
        s.oval(4,6,23,21,'red').oval(7,10,16,14,'rose').rect(13,9,3,14,'cream').rect(9,10,11,3,'cream').rect(10,22,12,2,'red')
    elif i==11:
        s.oval(9,10,15,15,'rose').rect(10,11,3,6,'cream').line(9,18,5,22,'rose',3).line(22,20,25,25,'rose',3)
        s.rect(4,23,3,3,'cream').rect(25,26,3,2,'cream').rect(15,6,5,5,'rose')
    elif i==12:fish(s)
    elif i==13:
        s.oval(9,5,14,23,'cream').rect(12,10,2,11,'white').rect(19,18,2,5,'sand')
    elif i==14:
        s.line(4,26,27,8,'brown',2)
        for x,y in [(6,16),(12,11),(18,7)]:
            s.rect(x,y,7,8,'rust').rect(x+1,y+1,5,2,'amber').rect(x+2,y+5,4,1,'soil')
    elif i==15:
        s.oval(7,8,18,17,'brown').rect(10,10,11,4,'amber').line(8,20,5,24,'rust',3).line(22,21,26,25,'rust',3)
        s.rect(3,25,4,2,'cream').rect(26,26,3,2,'cream').rect(14,16,8,2,'tan')
    elif i==16:
        fish(s,c='tan');s.line(3,25,28,6,'brown');
        for x in [11,15,19]:s.rect(x,12,1,6,'soil')
    elif i==17:
        s.oval(4,9,12,18,'white').oval(17,7,11,18,'cream').oval(7,14,6,8,'gold').oval(20,12,5,8,'amber')
    elif i==18:
        s.oval(3,15,26,12,'brown').oval(5,16,21,8,'sand').rect(10,17,12,4,'cream').rect(14,16,4,5,'gold');steam(s,10,10);steam(s,20,8)
    elif i==19:
        cob(s,12,6,True);s.rect(15,24,2,5,'tan').rect(15,3,2,3,'brown')
    elif i==20:
        s.oval(3,10,26,15,'tan').rect(6,19,22,6,'brown').rect(7,13,17,8,'sand')
        for x in [9,15,21]:s.line(x,12,x-2,17,'cream',2)
    elif i==21:
        bowl(s);s.rect(8,14,4,3,'amber').rect(14,13,3,4,'green').rect(20,14,3,3,'red');steam(s,11,9);steam(s,20,7)
    elif i==22:
        bowl(s,'dark',True);s.rect(2,17,3,5,'steel').rect(27,17,3,5,'steel').rect(9,14,5,4,'rust').rect(18,15,4,3,'tan').rect(19,10,2,6,'cream');steam(s,11,8)
    elif i==23:
        s.rect(8,12,17,14,'blue').rect(6,11,21,3,'light').rect(10,15,13,2,'cream').rect(18,7,3,6,'tan').rect(20,6,5,2,'tan').rect(11,19,5,3,'amber');steam(s,10,8)
    elif i==24:
        bowl(s,'cyan')
        for x,y in [(7,12),(17,11),(13,13)]:berry(s,x,y)
        s.rect(11,10,4,3,'gold');leaf(s,21,10)
    elif i==25:
        s.rect(9,9,14,4,'tan').oval(6,12,21,16,'sand').rect(11,5,10,4,'cream').rect(10,10,13,2,'brown').rect(11,17,11,6,'cream').rect(15,18,3,4,'tan').rect(3,27,7,1,'cream')
    elif i==26:
        s.rect(5,6,22,2,'brown').rect(6,8,1,19,'tan').rect(25,8,1,19,'tan')
        for x,y,h in [(9,10,16),(15,9,13),(21,11,14)]:
            s.rect(x,y,3,h,'rust').rect(x,y+3,2,1,'amber').rect(x+1,y+8,2,2,'soil')
    elif i==27:
        s.rect(8,11,16,16,'steel').rect(7,10,18,3,'light').rect(8,14,16,7,'amber').rect(10,7,12,2,'light')
        for x in [10,16,21]:s.rect(x,15,2,3,'brown')
        s.line(23,8,27,4,'cream',2)
    elif i==28:
        s.oval(5,14,23,13,'rust').oval(8,14,17,12,'amber').rect(6,13,21,3,'gold').rect(10,13,13,2,'brown').rect(7,7,15,2,'amber').rect(13,4,3,3,'leaf');steam(s,25,9)
    elif i==29:
        s.rect(7,13,17,13,'cream').rect(24,15,5,8,'sand').rect(25,17,2,4,None).rect(9,12,14,3,'brown').rect(11,18,5,5,'green');steam(s,11,8);steam(s,19,7)
    elif i==30:herb(s)
    return s

def gear(i):
    s=Sprite('gear_'+str(i),GEAR[i])
    if i in (0,1,2,3):
        steel=i>=2;pick=i%2==1
        s.line(9,28,20,6,'brown',3).line(10,26,19,9,'tan')
        if pick:
            if steel:
                s.rect(9,7,17,3,'light').rect(5,10,6,3,'steel').rect(25,9,3,6,'light').rect(4,12,3,5,'steel').rect(3,15,2,4,'light')
            else:
                s.rect(8,6,14,5,'steel').rect(6,10,4,5,'dark').rect(22,9,3,4,'steel').rect(24,12,2,3,'dark').rect(11,7,4,2,'light').rect(16,9,4,4,'brown')
        else:
            s.rect(7,6,11,11,'light' if steel else 'steel').rect(4,8,5,10,'steel').rect(4,16,7,3,'light').rect(17,9,7,4,'steel')
            if steel:s.rect(5,7,2,9,'cream')
            else:s.rect(10,7,4,3,'dark').dot(8,15,'light')
    elif i==4:
        s.line(8,27,20,13,'steel',4).oval(15,3,13,13,'light').rect(20,2,6,7,None).rect(19,8,5,3,'steel').rect(7,25,3,2,'light')
    elif i==5:
        s.rect(8,22,5,6,'brown').line(11,22,24,6,'light',3).line(14,18,23,7,'cream').line(7,20,15,25,'steel',2)
    elif i==6:
        s.rect(5,9,23,6,'steel').rect(7,8,19,2,'light').rect(9,15,7,11,'brown').rect(17,15,6,5,'dark').rect(18,16,3,2,None).rect(25,10,3,3,'ink').rect(5,15,4,2,'steel')
    elif i==7:
        s.rect(3,10,26,3,'light').rect(13,13,16,2,'dark').rect(3,13,13,6,'brown').rect(17,12,8,4,'tan').rect(4,19,5,5,'brown').rect(1,16,4,6,'tan')
    elif i==8:
        s.rect(3,12,25,4,'steel').rect(11,10,10,2,'dark').rect(22,13,7,1,'light').rect(5,16,7,3,'brown').rect(4,19,4,5,'tan').rect(14,16,3,7,'brown').rect(19,16,4,8,'dark').rect(20,23,4,2,'steel').rect(15,8,2,2,'teal')
    elif i==9:
        s.rect(2,14,28,2,'light').rect(7,16,14,3,'brown').rect(2,18,7,6,'tan').rect(12,19,3,5,'brown').rect(10,10,12,3,'dark').rect(11,9,3,4,'cyan').rect(20,9,3,4,'light').rect(24,17,1,10,'steel')
    elif i==10:
        s.oval(7,4,18,21,'shadow').rect(10,12,12,11,'ink').rect(12,13,8,9,'brown').rect(13,14,6,2,'tan').rect(7,24,18,4,'steel').rect(12,26,2,3,'cream').rect(20,26,1,3,'cream')
    elif i==11:
        s.rect(9,7,14,20,'brown').rect(5,9,4,15,'tan').rect(23,9,4,15,'brown').rect(12,6,8,4,'tan').rect(15,9,2,18,'steel').rect(10,17,4,5,'rust').rect(19,16,3,5,'tan')
    elif i==12:
        s.rect(7,5,18,9,'brown').rect(8,14,6,14,'tan').rect(18,14,6,14,'brown').rect(8,7,16,2,'dark').rect(14,7,4,3,'amber').rect(9,17,4,5,'steel').rect(19,17,4,5,'steel')
    elif i==13:
        for x,y in [(5,7),(18,4)]:
            s.rect(x+3,y,7,14,'brown').rect(x,y+12,10,7,'tan').rect(x-1,y+18,12,3,'dark').rect(x+4,y+3,4,1,'sand').rect(x+4,y+6,4,1,'sand')
    elif i==14:
        s.oval(5,6,22,17,'steel').rect(3,20,26,4,'dark').rect(11,8,10,3,'light').rect(5,19,5,2,'light').rect(23,23,2,5,'brown')
    elif i==15:
        s.rect(8,6,5,5,'brown').rect(20,6,5,5,'brown').rect(6,11,21,16,'steel').rect(11,9,11,2,'steel').rect(9,13,15,10,'light').rect(11,15,11,6,'steel').rect(7,25,19,3,'dark')
    elif i==16:
        s.oval(5,5,22,20,'shadow').rect(9,7,14,5,'steel').rect(5,15,22,5,'dark').rect(8,16,7,3,'cyan').rect(18,16,6,3,'teal').rect(3,19,5,7,'steel').rect(25,18,4,9,'dark').rect(13,4,6,4,'light')
    elif i==17:
        s.rect(8,5,5,6,'dark').rect(20,5,5,6,'dark').rect(5,11,23,17,'shadow').rect(13,11,7,6,'steel').rect(7,18,6,7,'olive').rect(15,18,6,7,'brown').rect(23,17,4,9,'steel').rect(7,12,4,4,'amber').rect(14,9,5,2,'light')
    elif i==18:
        s.rect(6,5,8,14,'shadow').rect(6,7,8,3,'steel').rect(5,18,9,8,'dark').rect(2,24,13,4,'steel').rect(18,8,8,13,'shadow').rect(18,11,8,3,'steel').rect(17,20,10,6,'dark').rect(16,26,13,3,'light').rect(8,13,5,1,'tan').rect(20,17,4,1,'tan')
    return s

def part(i):
    s=Sprite('part_'+str(i),PARTS[i])
    if i==0:
        s.rect(9,5,14,17,'steel').rect(12,8,8,10,'cyan').rect(15,12,2,2,'red').rect(6,23,20,4,'dark').rect(13,21,6,2,'light')
    elif i==1:
        s.rect(4,11,24,8,'dark').rect(3,8,6,14,'steel').rect(24,9,5,12,'steel').rect(4,10,2,9,'teal').rect(14,7,4,5,'light').rect(13,19,6,5,'brown').rect(11,24,11,2,'steel')
    elif i==2:
        s.rect(5,11,23,10,'steel').rect(3,13,3,6,'dark').rect(8,11,2,10,'light').rect(14,12,12,2,'light').rect(26,13,2,6,'ink')
    elif i==3:
        s.rect(5,10,22,12,'light').rect(4,13,3,6,'steel').rect(25,13,3,6,'dark')
        for x in [10,16,22]:s.rect(x,12,2,8,'dark')
    elif i==4:
        s.rect(10,5,12,13,'steel').rect(11,18,12,9,'dark').rect(14,27,9,2,'steel').rect(12,5,8,2,'amber')
        for y in [11,16,21]:s.rect(14,y,6,1,'light')
    elif i==5:
        s.rect(7,6,19,4,'steel').rect(12,11,10,5,'brown').rect(11,16,9,5,'tan').rect(9,21,9,6,'brown').rect(10,25,8,3,'dark')
        for y in [13,18,23]:s.rect(13,y,6,1,'dark')
    elif i==6:
        s.rect(3,13,26,5,'steel').rect(8,12,12,7,'light').rect(22,12,4,7,'dark').rect(2,14,4,3,'dark').rect(28,13,1,5,'cream')
        for x in [11,15,19]:s.rect(x,13,1,5,'steel')
    return s

def ore(s,c,kind=0):
    if kind==0:
        s.rect(5,17,23,9,'dark').rect(9,10,15,14,'steel').rect(13,7,8,5,'steel').rect(4,21,24,6,'shadow')
        s.rect(10,13,5,4,c).rect(19,17,5,3,c).rect(8,23,4,2,c)
    elif kind==1:
        s.rect(4,20,24,7,'dark').rect(5,14,11,10,'brown').rect(15,8,9,16,'steel').rect(23,16,5,9,'steel').rect(17,7,5,3,'rust')
        s.rect(17,11,3,9,c).rect(7,16,5,3,c).rect(21,22,5,2,c)
    elif kind==2:
        s.rect(4,23,24,4,'dark').rect(6,17,20,7,'steel').rect(10,13,15,5,'light').rect(15,8,8,6,'steel').rect(9,19,14,2,c).rect(17,11,4,2,c).rect(5,25,8,1,c)
    else:
        s.rect(5,22,23,5,'dark').rect(8,12,6,13,'steel').rect(18,6,5,19,'steel').rect(24,17,3,8,'shadow').rect(9,14,2,9,c).rect(19,8,2,14,c).rect(6,24,4,2,c).rect(14,19,3,7,'olive')
def ingot(s,c,hi):
    s.rect(5,15,22,10,c).rect(9,11,16,4,hi).rect(7,13,19,3,hi).rect(5,23,22,3,'dark').rect(24,15,3,8,'brown')
def bullet(s,x,y,h,c='amber',tip='cream',w=4):
    s.rect(x+1,y,2,3,tip).rect(x,y+3,w,h-3,c).rect(x,y+h,w,2,'brown').rect(x+1,y+4,1,h-5,'gold')

def item(i):
    s=Sprite('item_'+str(i),ITEMS[i])
    if i==0:
        for x,y in [(5,15),(12,8),(16,18)]:
            s.rect(x,y,10,6,'brown').oval(x-2,y,5,6,'sand').rect(x+3,y+1,5,1,'tan').dot(x,y+3,'brown')
    elif i in (1,7,14,19):ore(s,{1:'light',7:'rust',14:'teal',19:'cream'}[i],{1:0,7:1,14:3,19:2}[i])
    elif i==2:ingot(s,'steel','light');s.rect(9,17,9,2,'cream')
    elif i==3:
        s.rect(5,15,15,5,'rust').rect(8,9,5,15,'steel').rect(19,13,8,12,'brown').rect(20,14,5,8,None).line(4,26,25,7,'light',2).rect(5,8,6,4,'rust')
    elif i==4:
        s.rect(7,8,18,20,'olive').rect(10,5,12,4,'olive').rect(13,6,5,2,None).rect(22,7,5,3,'steel').rect(9,13,14,2,'tan').line(10,15,22,25,'dark').line(22,15,10,25,'dark').rect(14,17,4,5,'amber')
    elif i==5:
        s.rect(5,13,22,15,'brown').rect(7,16,18,8,'amber').rect(10,18,3,4,'dark').rect(15,18,3,4,'dark').rect(20,18,3,4,'dark')
        bullet(s,9,4,10);bullet(s,19,5,9)
    elif i==6:
        s.rect(6,5,20,23,'brown').rect(6,6,20,5,'amber').rect(9,12,14,12,'cream').rect(13,14,6,2,'leaf').rect(12,18,8,4,'rust').rect(8,25,16,1,'tan')
    elif i==8:
        s.oval(4,7,23,18,'rust').oval(8,10,15,11,'amber').oval(11,12,9,7,'soil').rect(22,20,5,7,'rust').rect(23,23,4,2,'amber')
    elif i==9:
        s.rect(4,20,23,7,'olive').rect(7,17,19,4,'gold').rect(13,5,9,15,'tan').rect(9,8,17,8,'gold').rect(15,3,5,17,'gold').rect(16,9,4,5,'brown').rect(16,10,3,3,None).rect(5,23,8,2,'brown')
    elif i==10:
        s.rect(5,7,22,4,'light').rect(12,11,8,12,'steel').rect(5,23,22,5,'steel').rect(14,12,2,10,'light').rect(7,24,18,2,'light')
    elif i==11:
        s.rect(6,6,20,21,'leaf').rect(12,12,9,9,'dark')
        for j in [9,14,19,24]:s.rect(3,j,3,1,'gold').rect(26,j,3,1,'gold')
        s.line(8,9,17,9,'teal').line(9,10,9,23,'teal').rect(15,14,4,4,'steel').rect(21,23,3,2,'amber')
    elif i==12:
        s.rect(6,10,20,16,'light').rect(9,6,14,5,'cream').rect(12,11,9,11,'steel').rect(15,14,9,5,'dark').rect(7,23,18,3,'cream').rect(7,13,4,7,'ice')
    elif i==13:
        s.rect(7,7,19,19,'dark').rect(12,12,9,9,'purple').rect(14,14,5,5,'teal').rect(4,4,4,6,'amber').rect(24,24,4,4,'cyan')
        for z in [10,15,20]:s.rect(z,4,1,3,'gold').rect(4,z,3,1,'teal').rect(z,26,1,3,'gold').rect(26,z,3,1,'teal')
    elif i==15:
        s.rect(8,5,6,23,'olive').rect(18,5,6,23,'steel').rect(9,8,4,15,'teal').rect(19,8,4,15,'gold').rect(6,4,20,3,'steel').rect(6,26,20,3,'dark').rect(10,13,2,6,'cream')
    elif i==16:
        s.rect(9,9,15,19,'blue').rect(12,4,9,5,'steel').rect(10,12,12,12,'ice').rect(14,15,4,7,'cyan').rect(12,17,8,3,'cyan').rect(10,25,12,2,'teal')
    elif i==17:
        s.rect(7,6,19,22,'olive').rect(6,5,21,3,'steel').rect(7,25,19,3,'dark').rect(9,12,15,10,'gold').rect(15,16,3,3,'ink').rect(12,13,3,3,'ink').rect(19,13,3,3,'ink').rect(14,20,6,2,'ink').rect(21,9,3,2,'teal')
    elif i==18:
        s.rect(4,25,24,2,'soil')
        for x,y,w in [(5,20,6),(10,16,5),(16,12,4),(19,18,7),(13,22,5),(23,24,4)]:
            s.rect(x,y,w,3,'steel').rect(x,y,w-2,1,'tan')
    elif i==20:
        s.rect(5,13,22,13,'light').rect(9,7,18,6,'cream').rect(8,9,16,2,'steel').rect(6,16,18,2,'ice').rect(23,11,4,13,'steel').rect(24,12,2,10,'dark')
    elif i==21:
        bullet(s,5,6,19);bullet(s,14,3,22);bullet(s,23,8,17);s.rect(4,24,25,4,'olive')
    elif i==22:
        for x,y in [(5,8),(14,5),(23,11)]:
            s.rect(x,y,5,17,'red').rect(x,y,5,3,'cream').rect(x,y+14,5,4,'gold').rect(x+1,y+4,1,8,'rose')
    elif i==23:
        bullet(s,9,3,24,'tan','light',5);bullet(s,20,7,20,'amber','steel',5);s.rect(10,13,4,2,'rust').rect(21,17,4,2,'rust')
    elif i==24:
        s.rect(3,21,26,8,'dark');bullet(s,6,5,20,'steel','cyan');bullet(s,16,2,23,'steel','teal');s.rect(23,19,4,7,'red').rect(24,21,2,3,'cream')
    return s

def all_icons():
    return [food(i) for i in range(31)]+[gear(i) for i in range(19)]+[part(i) for i in range(7)]+[item(i) for i in range(25)]
