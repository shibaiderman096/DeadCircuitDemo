#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeadCircuit
{
    // Opt-in native runtime checks. No desktop control or always-on command server.
    public sealed partial class DemoRuntimeChecks : MonoBehaviour
    {
        string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartChecks()
        {
            if (Environment.GetCommandLineArgs().Contains("--demo-qa")) new GameObject("Demo / Native runtime checks").AddComponent<DemoRuntimeChecks>();
        }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "--demo-qa-dir");
            output = i >= 0 && i + 1 < args.Length ? args[i + 1] : Application.persistentDataPath;
            Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(1);
            var bridge = GameBridge.Instance;
            Require(bridge != null, "bootstrap");
            if (bridge.PlayerName == "联机测试主机" || bridge.PlayerName == "联机测试客户端")
            {
                // Clean only the display value left by the first smoke-test revision.
                bridge.PlayerName = "幸存者";
                FindObjectsByType<TMPro.TMP_InputField>(FindObjectsSortMode.None).Single(f => f.name == "Player Name").text = "幸存者";
                PlayerPrefs.SetString("DeadCircuit.Name", "幸存者");
                PlayerPrefs.Save();
            }
            yield return Capture("menu");
            Click("New Game");
            yield return new WaitForSecondsRealtime(.5f);
            Require(bridge.IsRunning && bridge.IsHost && bridge.LocalPlayer != null, "new game button");
            var p = bridge.LocalPlayer;
            Require(bridge.World.era == TechEra.Primitive && p.items.Length == DemoCatalog.ItemNames.Length, "primitive world and expanded material inventory");
            Require(!bridge.World.buildings.Any(b => DemoCatalog.Demand(b.kind) > 0 && TechCatalog.RequiredEra(b.kind) >= TechEra.LowVoltage), "no prebuilt modern factory in a new world");
            yield return PointAtWorld(32, 35);
            RequireDefaultHud();
            var hotbar = Rect("Build Hotbar");
            Require(hotbar.rect.width <= 660f && hotbar.rect.height <= 76f, "compact hotbar");
            yield return Capture("gameplay");
            Click("Open Inventory");
            yield return null;
            Require(Active("Inventory Panel") && bridge.GetComponent<DemoHud>().CapturesInput, "inventory opens and consumes world input");
            yield return Capture("inventory");
            yield return Press(Key.B);
            Require(!Active("Inventory Panel"), "B closes inventory");
            yield return Press(Key.B);
            Require(Active("Inventory Panel"), "B opens inventory");
            Click("Close Inventory");
            yield return Press(Key.H);
            Require(Active("Help Panel") && bridge.GetComponent<DemoHud>().CapturesInput, "H opens help");
            yield return Capture("help");
            Click("Close Help");
            yield return Press(Key.Escape);
            Require(Active("Session Modal"), "Escape opens session menu");
            Click("Close Session");
            yield return Press(Key.Tab);
            Require(Active("Growth Modal"), "Tab opens growth");
            var attribute = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Single(b => b.name == "Upgrade Attribute" && b.transform.parent.name == "Attribute 1");
            attribute.onClick.Invoke();
            var skill = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Single(b => b.name == "Tier 1" && b.transform.parent.name == "Skill Branch 0");
            skill.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.2f);
            Require(p.attributes[1] == 1 && p.skills[0] == 1 && p.attributePoints == 1 && p.skillPoints == 0, "growth buttons");
            yield return Capture("growth");
            Click("Close Growth");
            // QA grants construction/research samples only inside its separate qa-world save.
            // Every unlock and building still passes through the real UI/authority command path.
            PrepareQaPlot(bridge.World, false);
            int beforeLocked = bridge.World.buildings.Count;
            p.x = 24; p.y = 28;
            bridge.Send(new PlayerCommand { action = "build", arg = (int)BuildKind.Generator, x = 24, y = 29 });
            Require(bridge.World.buildings.Count == beforeLocked, "host rejects a locked modern building");
            var stored = (int[])p.items.Clone();
            Array.Clear(p.items, 0, p.items.Length);
            yield return Press(Key.T);
            Require(Active("Technology Panel") && bridge.GetComponent<DemoHud>().CapturesInput, "T opens technology and captures world input");
            Require(!FindButton("Research Button").interactable, "research is disabled without samples");
            yield return Capture("technology-primitive");
            Array.Copy(stored, p.items, stored.Length);
            yield return Press(Key.T);
            Require(!Active("Technology Panel"), "T closes technology");
            for (int era = 1; era <= (int)TechEra.Nuclear; era++)
            {
                var target = (TechEra)era;
                foreach (BuildKind required in QaPrerequisites(target))
                    if (!bridge.World.buildings.Any(b => b.kind == required)) yield return BuildQaAtFreePlot(bridge, required);
                GrantSamples(p, TechCatalog.ResearchCost(target));
                Click("Technology Button");
                yield return new WaitForSecondsRealtime(.2f);
                Require(TechCatalog.CanResearch(bridge.World, p, target), "reachable research prerequisites: " + target + " " + TechCatalog.ResearchRequirement(bridge.World,target));
                Require(FindButton("Research Button").interactable, "next era research button: " + target);
                if (target == TechEra.Nuclear) yield return Capture("technology-nuclear");
                Click("Research Button");
                yield return new WaitForSecondsRealtime(.2f);
                Require(bridge.World.era == target, "research button unlocks era " + target);
                Require(p.attributes[1] == 1 && p.skills[0] == 1 && p.attributePoints == 1 && p.skillPoints == 0, "research keeps personal growth independent");
                if (Active("Technology Panel")) yield return Press(Key.T);
            }
            foreach(var kind in new[]{BuildKind.SteamPipe,BuildKind.SteamTurbine,BuildKind.DieselGenerator,BuildKind.MediumPole,
                BuildKind.HighGenerator,BuildKind.HighPole,BuildKind.Transformer,BuildKind.CoolingTower,BuildKind.NuclearProcessor,BuildKind.Reactor})
                yield return BuildQaAtFreePlot(bridge,kind);
            foreach(var machine in bridge.World.buildings)
            {
                if(TechCatalog.IsFuelBurner(machine.kind)) machine.items[(int)ItemKind.Fuel]=8;
                if(machine.kind==BuildKind.Reactor) { machine.items[(int)ItemKind.NuclearFuel]=4; machine.items[(int)ItemKind.Coolant]=40; }
            }
            p.x=34;p.y=22;
            yield return new WaitForSecondsRealtime(.5f);
            yield return PointAtWorld(34,27);
            yield return Capture("industrial-machines");
            var reactor=bridge.World.buildings.Single(b=>b.kind==BuildKind.Reactor);
            yield return PointAtWorld(reactor.x+1,reactor.y+1);
            Require(Active("Mouse Inspector"),"new reactor has a real footprint inspector");
            Require(reactor.powerGeneration>0 && reactor.items[(int)ItemKind.NuclearWaste]>0,"native nuclear fuel coolant and waste cycle");
            yield return Capture("reactor");
            yield return PointAtWorld(34,27);
            Click("Open Inventory");
            yield return null;
            yield return Capture("inventory-industrial");
            Click("Close Inventory");
            yield return VerifyEquipment(bridge);
            // A deterministic factory fixture verifies the new technology gates did not
            // regress powered production, footprint interaction or smooth cargo rendering.
            PrepareQaPlot(bridge.World, true);
            bridge.World.nodes.Add(new NodeState { id=bridge.World.nextId++,x=24,y=30,kind=NodeKind.Ore,stock=200,hp=35 });
            yield return BuildQa(bridge,BuildKind.Miner,25,30);
            yield return BuildQa(bridge,BuildKind.Belt,27,30);
            yield return BuildQa(bridge,BuildKind.Smelter,28,30);
            yield return BuildQa(bridge,BuildKind.Belt,30,30);
            yield return BuildQa(bridge,BuildKind.Assembler,31,30);
            yield return BuildQa(bridge,BuildKind.Chest,35,30);
            yield return BuildQa(bridge,BuildKind.Generator,27,26);
            bridge.World.buildings.Single(b=>b.kind==BuildKind.Generator).items[(int)ItemKind.Fuel]=8;
            yield return BuildQa(bridge,BuildKind.PowerPole,31,28);
            p.x=34; p.y=32;
            bridge.BuildSelection=BuildKind.Belt;
            yield return new WaitForSecondsRealtime(.2f);
            Require(Active("Build Details") && !Active("Mouse Inspector"), "building shows contextual details only");
            yield return Capture("building");
            yield return BuildQa(bridge,BuildKind.Belt,34,30);
            bridge.BuildSelection = null;
            p.x=34; p.y=33;
            yield return new WaitForSecondsRealtime(15);
            var chest = bridge.World.buildings.Single(b => b.kind == BuildKind.Chest);
            Require(chest.items[(int)ItemKind.Ammo] > 0, "running factory delivers ammo");
            Require(bridge.World.powerNetworks.Count > 0 && bridge.World.buildings.Where(b => DemoCatalog.Demand(b.kind) > 0).All(b => b.powerRatio > .99f), "connected factory power grid");
            Require(bridge.World.buildings.Single(b => b.kind == BuildKind.Assembler).footprintVersion == 1, "multi-cell factory");
            bool sawMovingCargo = false;
            float deadline = Time.realtimeSinceStartup + 5;
            while (Time.realtimeSinceStartup < deadline && !sawMovingCargo)
            {
                sawMovingCargo = bridge.World.cargo.Any(c => c.progress > .05f && c.progress < .95f);
                yield return null;
            }
            Require(sawMovingCargo, "real in-flight transport state");
            bool smoothBetweenTicks = false;
            deadline = Time.realtimeSinceStartup + 3;
            while (Time.realtimeSinceStartup < deadline && !smoothBetweenTicks)
            {
                var icon = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).FirstOrDefault(s => s.name == "In-flight item" && s.enabled);
                long tick = bridge.World.tick;
                Vector3 before = icon != null ? icon.transform.position : Vector3.zero;
                yield return null;
                smoothBetweenTicks = icon != null && icon.enabled && bridge.World.tick == tick &&
                    Vector3.Distance(icon.transform.position, before) > .0001f;
            }
            Require(smoothBetweenTicks, "cargo animates between authoritative simulation ticks");
            yield return PointAtWorld(32, 34);
            RequireDefaultHud();
            yield return Capture("factory");
            yield return PointAtWorld(29, 31);
            Require(Active("Mouse Inspector"), "hovering a machine's non-anchor cell shows its inspector");
            yield return Capture("machine");
            yield return PointAtWorld(32, 34);
            Require(!Active("Mouse Inspector"), "empty ground hides inspector");
            p.x=32; p.y=29;
            bridge.Send(new PlayerCommand { action="recipe",x=31,y=30,arg=52 });
            yield return new WaitForSecondsRealtime(.3f);
            bridge.Send(new PlayerCommand { action="interact",x=31,y=30 });
            float ammoDeadline=Time.realtimeSinceStartup+9f;
            while(chest.items[(int)ItemKind.RifleAmmo]==0 && Time.realtimeSinceStartup<ammoDeadline) yield return null;
            Require(chest.items[(int)ItemKind.RifleAmmo]>0,"new rifle ammunition is manufactured and physically transported into storage");
            yield return PointAtWorld(32,34);
            yield return Capture("ammunition-production");
            Click("Open Session");
            yield return null;
            Require(Active("Session Modal") && bridge.GetComponent<DemoHud>().CapturesInput, "session menu consumes world input");
            yield return Capture("session");
            Click("Save World");
            Require(File.Exists(bridge.SavePath), "save button");
            int buildings = bridge.World.buildings.Count;
            Click("Return to Menu");
            yield return new WaitForSecondsRealtime(.5f);
            Click("Load Game");
            yield return new WaitForSecondsRealtime(.5f);
            Require(bridge.IsRunning && bridge.World.era == TechEra.Nuclear && bridge.LocalPlayer.items.Length == DemoCatalog.ItemNames.Length && bridge.LocalPlayer.attributes[1] == 1 && bridge.LocalPlayer.skills[0] == 1 && bridge.World.buildings.Count == buildings, "load game button restores world technology, materials, personal growth and factory");
            VerifyRestoredEquipment(bridge.LocalPlayer);
            yield return PointAtWorld(32, 34);
            RequireDefaultHud();
            yield return Capture("restored");
            Debug.Log("DEMO_RUNTIME_QA_PASS menu=compactHud=inventory=help=keyboard=growth=technology=sevenEras=researchGates=build=inspection=factory=powerGrid=footprints=smoothCargo=equipment=attachments=reload=weaponDamage=save=load=true");
            Application.Quit(0);
        }
        static BuildKind[] QaPrerequisites(TechEra era)
        {
            switch(era)
            {
                case TechEra.Water: return new[]{BuildKind.Workbench};
                case TechEra.Steam: return new[]{BuildKind.Waterwheel,BuildKind.Shaft,BuildKind.MechanicalCrusher};
                case TechEra.LowVoltage: return new[]{BuildKind.Boiler,BuildKind.SteamFurnace};
                case TechEra.MediumVoltage: return new[]{BuildKind.CircuitAssembler};
                case TechEra.HighVoltage: return new[]{BuildKind.ChemicalPlant};
                default: return new[]{BuildKind.PrecisionAssembler};
            }
        }
        static void GrantSamples(PlayerState player,int[] cost)
        {
            for(int i=0;i<cost.Length;i++) player.items[i]=Math.Max(player.items[i],cost[i]+10);
        }
        static void PrepareQaPlot(WorldState world,bool clearBuildings)
        {
            world.zombies.Clear(); world.nextWaveAt=world.elapsed+9999;
            world.nodes.RemoveAll(n=>n.x>=17 && n.x<=52 && n.y>=12 && n.y<=32);
            char[] ground=world.ground.ToCharArray();
            for(int y=12;y<=32;y++) for(int x=17;x<=52;x++)
                ground[x+y*world.width]=(char)('0'+(int)(y==12?GroundKind.Water:GroundKind.Floor));
            world.ground=new string(ground);
            if(clearBuildings) { world.buildings.Clear(); world.cargo.Clear(); world.powerNetworks.Clear(); }
        }
        IEnumerator BuildQaAtFreePlot(GameBridge bridge,BuildKind kind)
        {
            var world=bridge.World;
            bool shore=kind==BuildKind.Waterwheel || kind==BuildKind.Boiler || kind==BuildKind.CoolingTower;
            for(int y=shore?13:18;y<=(shore?16:25);y++) for(int x=18;x<=49;x++)
            {
                bool clear=true;
                if(world.buildings.Any(b=>DemoCatalog.Contains(b,x-1,y))) continue;
                for(int dy=0;dy<DemoCatalog.Height(kind);dy++) for(int dx=0;dx<DemoCatalog.Width(kind);dx++)
                {
                    int tx=x+dx,ty=y+dy;
                    if(world.buildings.Any(b=>DemoCatalog.Contains(b,tx,ty)) || world.nodes.Any(n=>n.x==tx && n.y==ty)) clear=false;
                }
                if(!clear) continue;
                yield return BuildQa(bridge,kind,x,y);
                yield break;
            }
            Require(false,"no QA placement for "+kind);
        }
        IEnumerator BuildQa(GameBridge bridge,BuildKind kind,int x,int y)
        {
            var player=bridge.LocalPlayer;
            GrantSamples(player,DemoCatalog.BuildCost(kind));
            bridge.Send(new PlayerCommand());
            var candidates=new[]{new Vector2(x,y-1.2f),new Vector2(x-1.2f,y),new Vector2(x,y+DemoCatalog.Height(kind)+.2f)};
            bool approached=false;
            foreach(var at in candidates)
            {
                int cx=Mathf.RoundToInt(at.x),cy=Mathf.RoundToInt(at.y);
                if((at-new Vector2(x,y)).sqrMagnitude>2.4f*2.4f || cx<0 || cy<0 || cx>=64 || cy>=64 ||
                    bridge.World.ground[cx+cy*64]=='2' || bridge.World.buildings.Any(b=>DemoCatalog.Contains(b,cx,cy))) continue;
                player.x=at.x;player.y=at.y;approached=true;break;
            }
            Require(approached,"dry QA approach for "+kind);
            yield return new WaitForSecondsRealtime(.3f);
            bridge.Send(new PlayerCommand { action="build",arg=(int)kind,x=x,y=y });
            Require(bridge.World.buildings.Any(b=>b.kind==kind && b.x==x && b.y==y),"actual build command: "+kind+" "+player.notice);
            yield return new WaitForSecondsRealtime(.3f);
        }
        static Button FindButton(string name) => FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(b=>b.name==name);
        static RectTransform Rect(string name) => FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(r => r.name == name);
        static bool Active(string name) => Rect(name).gameObject.activeInHierarchy;
        static void RequireDefaultHud()
        {
            foreach (string name in new[] { "Inventory Panel", "Session Modal", "Help Panel", "Growth Modal", "Technology Panel", "Build Details", "Mouse Inspector" })
                Require(!Active(name), "default HUD hides " + name);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * .5f, Screen.height * .5f) }, hits);
            Require(hits.Count == 0, "default HUD leaves world centre interactive");
        }
        static IEnumerator Press(Key key)
        {
            Require(Keyboard.current != null, "QA keyboard");
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
            yield return new WaitForSecondsRealtime(.15f);
        }
        static IEnumerator PointAtWorld(float x, float y)
        {
            Require(Mouse.current != null && Camera.main != null, "QA pointer and camera");
            var point = Camera.main.WorldToScreenPoint(new Vector3(x, y, 0));
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = new Vector2(point.x, point.y) });
            yield return new WaitForSecondsRealtime(.2f);
        }
        static void Click(string name)
        {
            var button = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(b => b.name == name);
            Require(button.interactable, "disabled button: " + name);
            button.onClick.Invoke();
        }
        IEnumerator Capture(string label)
        {
            yield return new WaitForEndOfFrame();
            foreach (var text in FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!text.isActiveAndEnabled || string.IsNullOrWhiteSpace(text.text) || text.color.a <= 0) continue;
                var input = text.GetComponentInParent<TMPro.TMP_InputField>();
                if (input != null && input.placeholder == text && !string.IsNullOrEmpty(input.text)) continue;
                text.ForceMeshUpdate();
                Require(text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(c => c.isVisible), "invisible UI label: " + text.name + " text=" + text.text);
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(output, label + ".png"));
            yield return new WaitForSecondsRealtime(.2f);
        }
        static void Require(bool condition, string message)
        {
            if (condition) return;
            Debug.LogError("DEMO_RUNTIME_QA_FAIL " + message);
            Application.Quit(11);
            throw new InvalidOperationException(message);
        }
    }
}
#endif
