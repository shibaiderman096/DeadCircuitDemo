using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoWorldView
    {
        // Small point-filtered machine silhouettes match the existing procedural demo art.
        // They remain view-owned, and never change imported sprites or gameplay state.
        void CreateIndustryArt()
        {
            const int size = 64, columns = 7, atlasWidth = size * columns;
            int tiles = (int)BuildKind.CoolingTower - (int)BuildKind.Workbench + 1 + DemoCatalog.ItemCount - 7;
            int rows = (tiles + columns - 1) / columns;
            var pixels = new Color32[atlasWidth * size * rows];
            var ink = new Color32(23, 36, 36, 255);
            var edge = new Color32(91, 113, 105, 255);
            var metal = new Color32(139, 157, 148, 255);
            var dark = new Color32(40, 59, 56, 255);
            var wood = new Color32(150, 107, 59, 255);
            var bronze = new Color32(191, 137, 69, 255);
            var hot = new Color32(247, 156, 68, 255);
            var mint = new Color32(105, 205, 166, 255);
            var blue = new Color32(103, 170, 206, 255);
            var violet = new Color32(163, 143, 204, 255);
            var nuclear = new Color32(171, 208, 79, 255);
            int tile = 0;
            for (int value = (int)BuildKind.Workbench; value <= (int)BuildKind.CoolingTower; value++, tile++)
            {
                var kind = (BuildKind)value;
                int bx = tile % columns * size, by = tile / columns * size;
                System.Action<int,int,int,int,Color32> fill = (x,y,w,h,color) =>
                {
                    for (int py = Mathf.Max(0,y); py < Mathf.Min(size,y+h); py++)
                        for (int px = Mathf.Max(0,x); px < Mathf.Min(size,x+w); px++)
                            pixels[(by+py)*atlasWidth+bx+px] = color;
                };
                System.Action<int,int,int,Color32> disc = (cx,cy,r,color) =>
                {
                    for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++)
                        if (x*x+y*y <= r*r) fill(cx+x,cy+y,1,1,color);
                };
                Color32 accent = value <= (int)BuildKind.SteamTurbine ? bronze :
                    value <= (int)BuildKind.Transformer ? mint : value < (int)BuildKind.NuclearProcessor ? violet : nuclear;
                bool pole = kind == BuildKind.MediumPole || kind == BuildKind.HighPole;
                if (!pole && kind != BuildKind.Shaft && kind != BuildKind.SteamPipe)
                {
                    fill(3,4,58,52,ink); fill(5,6,54,47,edge); fill(7,9,50,40,dark);
                    fill(5,6,54,3,metal); fill(7,49,50,3,metal);
                    fill(7,10,3,3,accent); fill(54,10,3,3,accent);
                }
                switch (kind)
                {
                    case BuildKind.Workbench:
                        fill(3,4,58,52,new Color32(0,0,0,0));
                        fill(10,7,7,33,ink); fill(47,7,7,33,ink);
                        fill(12,9,3,27,wood); fill(49,9,3,27,wood);
                        fill(4,31,56,20,ink); fill(6,34,52,14,wood); fill(7,46,50,3,bronze);
                        fill(11,36,22,2,dark); fill(22,36,2,12,dark);
                        fill(38,39,14,5,metal); fill(43,34,4,13,metal);
                        break;
                    case BuildKind.StoneFurnace:
                    case BuildKind.SteamFurnace:
                        fill(11,9,42,45,ink); fill(13,12,38,38,new Color32(111,108,89,255));
                        for (int y = 15; y < 49; y += 8)
                        { fill(13,y,38,2,ink); fill(y%16==15?25:37,y,2,8,ink); }
                        fill(21,16,22,20,ink); fill(24,18,16,14,hot); fill(28,20,7,7,new Color32(255,211,110,255));
                        fill(22,51,20,6,ink); fill(25,53,14,3,edge);
                        if (kind == BuildKind.SteamFurnace) { fill(4,16,6,30,bronze); fill(4,42,18,5,bronze); }
                        break;
                    case BuildKind.Waterwheel:
                        fill(3,4,58,52,new Color32(0,0,0,0));
                        fill(8,3,6,34,ink); fill(50,3,6,34,ink);
                        disc(32,32,27,ink); disc(32,32,24,wood); disc(32,32,19,ink); disc(32,32,15,dark);
                        for (int k = -1; k <= 1; k += 2)
                        { fill(29,9,6,46,bronze); fill(9,29,46,6,bronze); fill(15,15,7,7,bronze); fill(42,42,7,7,bronze); }
                        disc(32,32,7,ink); disc(32,32,4,metal);
                        break;
                    case BuildKind.Shaft:
                        fill(3,23,58,18,ink); fill(4,27,56,10,wood);
                        fill(10,20,8,24,edge); fill(46,20,8,24,edge); fill(12,23,3,18,metal); fill(48,23,3,18,metal);
                        break;
                    case BuildKind.SteamPipe:
                        fill(0,23,64,18,ink); fill(0,26,64,12,bronze); fill(0,35,64,2,hot);
                        fill(9,20,7,24,edge); fill(48,20,7,24,edge);
                        break;
                    case BuildKind.Boiler:
                        fill(13,13,38,37,ink); fill(16,15,32,32,bronze);
                        fill(18,42,28,3,hot); fill(20,24,24,4,ink);
                        fill(23,12,18,12,ink); fill(26,14,12,7,hot);
                        fill(42,47,9,14,ink); fill(44,49,5,11,edge);
                        disc(31,35,6,ink); disc(31,35,4,metal); fill(30,35,2,4,hot);
                        break;
                    case BuildKind.MechanicalCrusher:
                    case BuildKind.SteamMiner:
                        fill(13,19,38,28,ink); fill(17,22,30,22,bronze);
                        fill(11,43,42,9,ink); fill(14,46,36,4,metal);
                        fill(22,10,20,10,edge); fill(25,12,14,5,ink);
                        if (kind == BuildKind.SteamMiner) { fill(27,14,10,28,metal); fill(21,10,22,5,bronze); }
                        break;
                    case BuildKind.SteamTurbine:
                    case BuildKind.DieselGenerator:
                    case BuildKind.HighGenerator:
                        fill(12,17,40,28,ink); fill(15,20,34,23,accent);
                        fill(10,19,7,21,edge); fill(48,19,7,21,edge);
                        fill(17,47,23,7,ink); fill(20,49,17,3,metal);
                        fill(45,43,8,18,ink); fill(47,45,4,14,metal);
                        for (int x = 20; x < 47; x += 6) fill(x,23,3,14,dark);
                        break;
                    case BuildKind.CircuitAssembler:
                    case BuildKind.PrecisionAssembler:
                        fill(10,17,44,29,ink); fill(13,20,38,23,accent);
                        fill(6,12,52,9,ink); fill(8,15,48,3,metal);
                        fill(22,25,20,14,dark); fill(25,28,14,8,mint);
                        fill(12,45,6,12,metal); fill(15,51,28,5,metal); fill(37,42,6,12,metal);
                        if (kind == BuildKind.PrecisionAssembler) { fill(45,23,3,16,blue); fill(19,22,2,21,metal); }
                        break;
                    case BuildKind.ChemicalPlant:
                    case BuildKind.NuclearProcessor:
                        fill(10,15,18,34,ink); fill(13,18,12,27,accent); fill(14,39,10,3,metal);
                        fill(36,15,18,34,ink); fill(39,18,12,27,metal); fill(40,39,10,3,accent);
                        fill(24,21,16,7,ink); fill(25,23,14,3,bronze);
                        fill(10,46,44,7,ink); fill(12,48,40,3,edge);
                        fill(42,23,5,9,kind == BuildKind.NuclearProcessor ? nuclear : blue);
                        break;
                    case BuildKind.MediumPole:
                    case BuildKind.HighPole:
                        fill(28,3,8,53,ink); fill(31,5,3,48,metal);
                        fill(7,44,50,6,ink); fill(10,46,44,2,metal);
                        fill(12,49,7,10,ink); fill(45,49,7,10,ink);
                        fill(14,51,3,6,kind == BuildKind.HighPole ? violet : blue);
                        fill(47,51,3,6,kind == BuildKind.HighPole ? violet : blue);
                        if (kind == BuildKind.HighPole) { fill(14,30,36,5,ink); fill(17,32,30,2,metal); }
                        break;
                    case BuildKind.Transformer:
                        fill(13,17,38,28,ink); fill(16,20,32,22,metal);
                        for (int x = 20; x <= 40; x += 20)
                        { fill(x-4,25,10,28,ink); for(int y=28;y<50;y+=5) fill(x-2,y,6,3,bronze); }
                        fill(29,22,6,16,dark);
                        break;
                    case BuildKind.Reactor:
                        disc(32,32,26,ink); disc(32,32,22,metal); disc(32,32,17,dark); disc(32,32,10,nuclear);
                        fill(28,4,8,13,edge); fill(28,47,8,13,edge); fill(4,28,13,8,edge); fill(47,28,13,8,edge);
                        disc(32,32,3,ink); fill(28,37,8,5,ink); fill(22,26,7,5,ink); fill(36,26,7,5,ink);
                        break;
                    case BuildKind.CoolingTower:
                        for (int y = 8; y <= 53; y++)
                        {
                            int half = 13 + Mathf.Abs(y-34)/3;
                            fill(32-half,y,half*2,1,ink); fill(34-half,y,half*2-4,1,metal);
                        }
                        fill(14,50,36,7,ink); fill(17,52,30,3,dark);
                        fill(11,8,42,6,edge); fill(17,15,5,13,dark); fill(42,15,5,13,dark);
                        fill(28,20,8,20,blue);
                        break;
                }
            }
            // The first seven item sprites keep the authored atlas. New materials get
            // individually coloured silhouettes, so cargo remains legible in transit.
            for (int item = 7; item < itemSprites.Length; item++,tile++)
            {
                int bx=tile%columns*size,by=tile/columns*size;
                Color32 color = ItemColors[item];
                bool circuit = item==(int)ItemKind.Circuit || item==(int)ItemKind.AdvancedCircuit;
                bool canister = item==(int)ItemKind.Coolant || item==(int)ItemKind.NuclearWaste;
                for (int y=14;y<50;y++) for(int x=12;x<52;x++)
                {
                    bool inside = circuit || canister ? x>=18 && x<=46 :
                        Mathf.Abs(x-32)+Mathf.Abs((y-32)*2/3)<24;
                    if (!inside) continue;
                    bool rim=x<21 || x>43 || y<18 || y>45;
                    pixels[(by+y)*atlasWidth+bx+x]=rim?ink:color;
                    if(circuit && x>=26 && x<=38 && y>=24 && y<=40) pixels[(by+y)*atlasWidth+bx+x]=dark;
                    if(canister && (y==24 || y==25 || y==38 || y==39)) pixels[(by+y)*atlasWidth+bx+x]=metal;
                }
            }
            industryTexture=new Texture2D(atlasWidth,size*rows,TextureFormat.RGBA32,false)
            {name="DeadCircuit industrial pixels",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            industryTexture.SetPixels32(pixels); industryTexture.Apply(false,true);
            tile=0;
            for(int value=(int)BuildKind.Workbench;value<=(int)BuildKind.CoolingTower;value++,tile++)
            {
                var sprite=Sprite.Create(industryTexture,new Rect(tile%columns*size,tile/columns*size,size,size),new Vector2(.5f,.5f),size,0,SpriteMeshType.FullRect);
                sprite.name="industry_"+value; sprites.Add(sprite.name,sprite);
            }
            for(int item=7;item<itemSprites.Length;item++,tile++)
            {
                var sprite=Sprite.Create(industryTexture,new Rect(tile%columns*size,tile/columns*size,size,size),new Vector2(.5f,.5f),size,0,SpriteMeshType.FullRect);
                sprite.name="industry_item_"+item; sprites.Add(sprite.name,sprite); itemSprites[item]=sprite;
            }
        }
    }
}
