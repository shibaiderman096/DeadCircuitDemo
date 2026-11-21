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
        static Mouse qaMouse;
        int qaRadiationSuitId;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartChecks()
        {
            if (Environment.GetCommandLineArgs().Contains("--demo-qa")) new GameObject("Demo / Native runtime checks").AddComponent<DemoRuntimeChecks>();
        }
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1);
            var bridge = GameBridge.Instance;
            Require(bridge != null, "bootstrap");
            Require(bridge.QaArgumentsValid && !string.IsNullOrEmpty(bridge.QaOutputDirectory) && !string.IsNullOrEmpty(bridge.QaSavePath),
                "QA 启动参数有效：" + bridge.QaArgumentError);
            string parserRoot = Path.Combine(Path.GetTempPath(), "dead-circuit-qa-parser");
            string[] validArguments = { "--demo-qa", "--demo-qa-dir", parserRoot, "--demo-qa-save-path", Path.Combine(parserRoot, "world.json") };
            Require(GameBridge.TryResolveQaPaths(validArguments, out _, out _, out _), "QA path parser accepts a save inside its output directory");
            Require(!GameBridge.TryResolveQaPaths(new[] { "--demo-qa", "--demo-qa-dir", parserRoot }, out _, out _, out _),
                "QA path parser requires an explicit save path");
            Require(!GameBridge.TryResolveQaPaths(new[] { "--demo-qa", "--demo-qa-dir", parserRoot, "--demo-qa-save-path", Path.Combine(parserRoot, "..", "outside.json") }, out _, out _, out _),
                "QA path parser rejects saves outside its output directory");
            Require(!GameBridge.TryResolveQaPaths(validArguments.Concat(new[] { "--demo-smoke-host" }).ToArray(), out _, out _, out _),
                "QA path parser rejects smoke mode combinations");
            output = bridge.QaOutputDirectory;
            Directory.CreateDirectory(output);
            string qaSavePath = Path.GetFullPath(bridge.QaSavePath);
            Require(!File.Exists(qaSavePath) && !bridge.IsRunning,
                "QA starts at the menu with a fresh isolated save path");
            if (Environment.GetCommandLineArgs().Contains("--demo-modular-qa"))
            {
                yield return CheckModularMachines(bridge);
                Application.Quit(0);
                yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("--demo-controls-qa"))
            {
                yield return CheckPlayerControls(bridge);
                Application.Quit(0);
                yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("--demo-exterior-qa"))
            {
                yield return CheckNaturalEntrances(bridge);
                Application.Quit(0);
                yield break;
            }
            Require(Active("Start Page") && !Active("Create World Page") && !Active("Load World Page") && !Active("LAN Page"), "home page is the initial menu");
            RequireButtonIcon("New Game", "ui_new_world");
            RequireButtonIcon("Load Game", "ui_load_world");
            RequireButtonIcon("Open LAN", "ui_network");
            Button appearanceEntry = FindButton("Open Appearance");
            Require(appearanceEntry.interactable && PointerTargetsButton(appearanceEntry), "appearance entry receives UI raycasts on the start page");
            Click("Open Appearance");
            yield return null;
            Require(Active("Appearance Panel") && !bridge.IsRunning, "appearance editor opens before entering a world");
            Click("Close Appearance");
            yield return null;
            Require(!Active("Appearance Panel") && Active("Start Page"), "appearance editor returns to the start page");
            yield return Capture("menu");
            Click("New Game");
            yield return null;
            Require(Active("Create World Page") && !bridge.IsRunning, "New Game opens configuration without starting a session");
            Click("Create World Page Back");
            yield return null;
            Click("Open LAN");
            yield return null;
            Require(Active("LAN Page") && FindButton("Start Host").interactable && !bridge.IsRunning, "host action belongs to LAN page");
            yield return Capture("lan");
            Click("Start Host");
            yield return null;
            Require(Active("Create World Page") && !bridge.IsRunning, "Start Host also requires world configuration confirmation");
            Click("Create World Page Back");
            yield return null;
            Click("New Game");
            string qaWorldName = "运行时QA-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            InputField("World Name").text = qaWorldName;
            InputField("World Seed").text = "73149";
            Click("World Preset Balanced");
            Click("City Density 1");
            yield return null;
            Require(!bridge.IsRunning && FindButton("Create World").interactable, "world starts only after Create World confirmation");
            yield return Capture("create-world");
            Click("Create World");
            yield return new WaitForSecondsRealtime(.5f);
            Require(bridge.IsRunning && bridge.IsHost && bridge.LocalPlayer != null, "Create World starts the configured host session");
            Require(bridge.World.generation != null && bridge.World.generation.worldName == qaWorldName && bridge.World.seed == 73149 &&
                bridge.World.generation.preset == WorldPreset.Balanced && bridge.World.generation.cityDensity == 1, "creation fields reach the generated world");
            Require(Path.GetFullPath(bridge.SavePath) == qaSavePath, "QA never saves over a regular world");
            var p = bridge.LocalPlayer;
            Require(bridge.World.era == TechEra.Primitive && p.items.Length == DemoCatalog.ItemNames.Length, "primitive world and expanded material inventory");
            Require(!bridge.World.buildings.Any(b => DemoCatalog.Demand(b.kind) > 0 && TechCatalog.RequiredEra(b.kind) >= TechEra.LowVoltage), "no prebuilt modern factory in a new world");
            yield return PointAtWorld(p.x, p.y);
            RequireDefaultHud();
            int currentAppearance = bridge.AppearanceCode;
            appearanceEntry.onClick.Invoke();
            Require(!Active("Appearance Panel") && !bridge.CanEditAppearance &&
                !bridge.TrySetAppearance(currentAppearance) && bridge.AppearanceCode == currentAppearance,
                "appearance editor and preset changes stay disabled in a running world");
            var hotbar = Rect("Build Hotbar");
            Require(hotbar.rect.width <= 660f && hotbar.rect.height <= 76f, "compact hotbar");
            Click("HUD Build Toggle");
            yield return null;
            Require(Active("Build Hotbar") && !Active("Build Details"), "build toggle expands the tray without selecting a building");
            Click("HUD Build Toggle");
            yield return null;
            RequireDefaultHud();
            yield return Capture("gameplay");
            yield return ClickHudTool("Open Inventory");
            Require(Active("Inventory Panel") && bridge.GetComponent<DemoHud>().CapturesInput, "inventory opens and consumes world input");
            RequireInventoryLayout();
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
            PrepareQaPrimitiveProgression(bridge.World);
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
            yield return CheckQaTechnologyTree(bridge);
            Array.Copy(stored, p.items, stored.Length);
            yield return Press(Key.T);
            Require(!Active("Technology Panel"), "T closes technology");
            for (int era = 1; era <= (int)TechEra.Nuclear; era++)
            {
                var target = (TechEra)era;
                foreach (BuildKind required in QaPrerequisites(target))
                    if (!bridge.World.buildings.Any(b => b.kind == required)) yield return BuildQaAtFreePlot(bridge, required);
                if (target == TechEra.Nuclear) PrepareQaNuclearProgression(bridge.World);
                GrantSamples(p, TechCatalog.ResearchCost(target));
                yield return ClickHudTool("Technology Button");
                yield return new WaitForSecondsRealtime(.2f);
                Click("Technology Era " + era);
                yield return null;
                Require(TechCatalog.CanResearch(bridge.World, p, target), "reachable research prerequisites: " + target + " " + TechCatalog.ResearchRequirement(bridge.World,target));
                Require(FindButton("Research Button").interactable, "next era research button: " + target);
                if (target == TechEra.Nuclear) yield return Capture("technology-nuclear");
                Click("Research Button");
                yield return new WaitForSecondsRealtime(.2f);
                Require(bridge.World.era == target, "research button unlocks era " + target);
                Require(p.attributes[1] == 1 && p.skills[0] == 1 && p.attributePoints == 1 && p.skillPoints == 0, "research keeps personal growth independent");
                if (target == TechEra.Water)
                {
                    PrepareQaWaterProgression(bridge.World);
                    foreach (WaterTech waterTech in new[] { WaterTech.Wheel, WaterTech.Shafts, WaterTech.Crushing, WaterTech.Blast,
                        WaterTech.Pressing, WaterTech.Components, WaterTech.Transport })
                        yield return ResearchQaWater(bridge, waterTech);
                }
                if (target == TechEra.Steam)
                {
                    PrepareQaSteamProgression(bridge.World);
                    var workbench = bridge.World.buildings.FirstOrDefault(b => b != null && b.kind == BuildKind.Workbench);
                    Require(workbench != null, "steam research workbench");
                    p.x = DemoCatalog.CenterX(workbench);
                    p.y = DemoCatalog.CenterY(workbench) + 1.1f;
                    foreach (SteamTech steamTech in SteamCatalog.All.Select(d => d.id))
                        yield return ResearchQaSteam(bridge, steamTech);
                    foreach (ToolTech toolTech in ToolTechCatalog.All.Select(d => (ToolTech)d.id))
                        yield return ResearchQaEquipment(bridge, ResearchDomain.Tool, (int)toolTech);
                }
                if (target == TechEra.LowVoltage)
                {
                    PrepareQaLowVoltageProgression(bridge.World);
                    var workbench = bridge.World.buildings.FirstOrDefault(b => b != null && b.kind == BuildKind.Workbench);
                    Require(workbench != null, "low voltage research workbench");
                    p.x = DemoCatalog.CenterX(workbench);
                    p.y = DemoCatalog.CenterY(workbench) + 1.1f;
                    foreach (LowVoltageTech lowVoltageTech in LowVoltageCatalog.All.Select(d => d.id))
                        yield return ResearchQaLowVoltage(bridge, lowVoltageTech);
                    foreach (WeaponTech weaponTech in Enumerable.Range(0, 5).Select(id => (WeaponTech)id))
                        yield return ResearchQaEquipment(bridge, ResearchDomain.Weapon, (int)weaponTech);
                }
                if (target == TechEra.MediumVoltage)
                {
                    PrepareQaPetrochemicalProgression(bridge.World);
                    var workbench = bridge.World.buildings.FirstOrDefault(b => b != null && b.kind == BuildKind.Workbench);
                    Require(workbench != null, "medium voltage research workbench");
                    p.x = DemoCatalog.CenterX(workbench);
                    p.y = DemoCatalog.CenterY(workbench) + 1.1f;
                    foreach (PetrochemicalTech petrochemicalTech in PetrochemicalCatalog.All.Select(d => d.id))
                        yield return ResearchQaPetrochemical(bridge, petrochemicalTech);
                    foreach (WeaponTech weaponTech in Enumerable.Range(5, 4).Select(id => (WeaponTech)id))
                        yield return ResearchQaEquipment(bridge, ResearchDomain.Weapon, (int)weaponTech);
                }
                if (target == TechEra.HighVoltage)
                {
                    PrepareQaPrecisionProgression(bridge.World);
                    var workbench = bridge.World.buildings.FirstOrDefault(b => b != null && b.kind == BuildKind.Workbench);
                    Require(workbench != null, "high voltage research workbench");
                    p.x = DemoCatalog.CenterX(workbench);
                    p.y = DemoCatalog.CenterY(workbench) + 1.1f;
                    foreach (PrecisionTech precisionTech in PrecisionCatalog.All.Select(d => d.id))
                        yield return ResearchQaPrecision(bridge, precisionTech);
                    foreach (WeaponTech weaponTech in Enumerable.Range(9, 3).Select(id => (WeaponTech)id))
                        yield return ResearchQaEquipment(bridge, ResearchDomain.Weapon, (int)weaponTech);
                    Require(bridge.World.weaponResearch == WeaponTechCatalog.AllResearch &&
                        bridge.World.toolResearch == ToolTechCatalog.AllResearch,
                        "all fourteen optional equipment studies completed through their real HUD commands");
                }
                if (target == TechEra.Nuclear) yield return CheckQaNuclearResearch(bridge);
                if (Active("Technology Panel")) yield return Press(Key.T);
            }
            yield return BuildQaNuclearFacility(bridge);
            foreach(var kind in new[]{BuildKind.SteamPipe,BuildKind.SteamTurbine,BuildKind.MediumPole,BuildKind.Transformer,BuildKind.NuclearProcessor})
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
            yield return SetQaInspector(true);
            yield return PointAtWorld(reactor.x+1,reactor.y+1);
            Require(Active("Mouse Inspector"),"new reactor has a real footprint inspector");
            Require(reactor.powerGeneration>0 && bridge.World.produced[(int)ItemKind.NuclearWaste]>0 &&
                bridge.World.produced[(int)ItemKind.HotCoolant]>0,"native nuclear fuel coolant and waste cycle");
            yield return Capture("reactor");
            yield return SetQaInspector(false);
            yield return PointAtWorld(34,27);
            yield return ClickHudTool("Open Inventory");
            yield return Capture("inventory-industrial");
            Click("Close Inventory");
            yield return VerifyEquipment(bridge);
            // Preserve the original equipment checks, then restore nuclear work protection.
            yield return WearQaRadiationSuit(bridge);
            qaArmorIds = (int[])bridge.LocalPlayer.armorIds.Clone();
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
            Click("HUD Build Toggle");
            yield return null;
            Require(Active("Build Hotbar"), "build tray must be expanded before choosing a slot");
            int beltSlot = Array.IndexOf(TechCatalog.Hotbar(bridge.World.era), BuildKind.Belt);
            Require(beltSlot >= 0, "current era exposes a belt hotbar slot");
            Click("Build Slot " + (beltSlot + 1));
            yield return new WaitForSecondsRealtime(.2f);
            Require(bridge.BuildSelection == BuildKind.Belt && Active("Build Details") && !Active("Mouse Inspector"), "build slot selects belt and shows contextual details only");
            yield return Capture("building");
            yield return BuildQa(bridge,BuildKind.Belt,34,30);
            Click("HUD Build Toggle");
            yield return null;
            Require(!bridge.BuildSelection.HasValue && !Active("Build Hotbar") && !Active("Build Details"), "collapsing build cancels the selected building");
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
            Require(!Active("Mouse Inspector"), "machine hover stays hidden until inspection is enabled");
            yield return SetQaInspector(true);
            yield return PointAtWorld(29, 31);
            Require(Active("Mouse Inspector"), "hovering a machine's non-anchor cell shows its inspector");
            yield return Capture("machine");
            yield return PointAtWorld(32, 34);
            Require(!Active("Mouse Inspector"), "empty ground hides inspector");
            yield return SetQaInspector(false);
            p.x=32; p.y=29;
            bridge.Send(new PlayerCommand { action="recipe",x=31,y=30,arg=52 });
            yield return new WaitForSecondsRealtime(.3f);
            bridge.Send(new PlayerCommand { action="interact",x=31,y=30 });
            float ammoDeadline=Time.realtimeSinceStartup+9f;
            while(chest.items[(int)ItemKind.RifleAmmo]==0 && Time.realtimeSinceStartup<ammoDeadline) yield return null;
            Require(chest.items[(int)ItemKind.RifleAmmo]>0,"new rifle ammunition is manufactured and physically transported into storage");
            yield return PointAtWorld(32,34);
            yield return Capture("ammunition-production");
            yield return ClickHudTool("Open Session");
            Require(Active("Session Modal") && bridge.GetComponent<DemoHud>().CapturesInput, "session menu consumes world input");
            yield return Capture("session");
            int radiationSuitDurability = EquipmentCatalog.Find(bridge.LocalPlayer, qaRadiationSuitId).durability;
            Click("Save World");
            Require(Path.GetFullPath(bridge.SavePath) == qaSavePath && File.Exists(qaSavePath), "save button writes the isolated QA world");
            int buildings = bridge.World.buildings.Count;
            Click("Return to Menu");
            yield return WaitForAppearanceEditing(bridge);
            Click("Open Appearance");
            yield return null;
            Require(Active("Appearance Panel") && !bridge.IsRunning, "appearance editor opens immediately after returning to the menu");
            Click("Appearance Next 0");
            Require(FindButton("Save Appearance").interactable, "appearance saving re-enables after network teardown");
            Click("Close Appearance");
            yield return null;
            Click("Load Game");
            yield return null;
            Require(Active("Load World Page") && !bridge.IsRunning, "Load Game opens the world selector without loading");
            // qaRun writes qa-world-v1.json, outside the regular Worlds archive. The
            // bridge exposes ONLY that validated QA save as saveId=qa in this mode.
            // Never fall back to the first real save or call StartHost(load:true).
            Require(bridge.Worlds != null && bridge.Worlds.Count == 1, "QA bridge must list only its isolated save, never regular worlds");
            var qaWorld = bridge.Worlds.SingleOrDefault(w => w != null && w.name == qaWorldName && w.seed == 73149);
            Require(qaWorld != null && qaWorld.saveId == "qa", "only this run's isolated QA save is selectable as saveId=qa");
            Require(!FindButton("Load Selected World").interactable, "loading requires an explicit world selection");
            yield return ClickScrolled("World Save Select " + qaWorld.saveId);
            Require(!bridge.IsRunning && FindButton("Load Selected World").interactable, "selecting a save only enables confirmation");
            yield return Capture("load-world");
            Click("Load Selected World");
            yield return new WaitForSecondsRealtime(.5f);
            Require(bridge.IsRunning && bridge.IsHost && bridge.LocalPlayer != null && Path.GetFullPath(bridge.SavePath) == qaSavePath &&
                bridge.World.generation.worldName == qaWorldName && bridge.World.seed == 73149, "load confirmation restores the selected isolated QA world");
            Require(bridge.World.era == TechEra.Nuclear && bridge.LocalPlayer.items.Length == DemoCatalog.ItemNames.Length && bridge.LocalPlayer.attributes[1] == 1 && bridge.LocalPlayer.skills[0] == 1 && bridge.World.buildings.Count == buildings, "selected world restores technology, materials, personal growth and factory");
            Require(NuclearCatalog.HasResearch(bridge.World, NuclearTech.UraniumBeneficiation) &&
                NuclearCatalog.HasResearch(bridge.World, NuclearTech.UraniumEnrichment) &&
                PrimitiveCatalog.IsRecipeUnlocked(bridge.World, TechCatalog.Recipe(93)) &&
                PrimitiveCatalog.IsRecipeUnlocked(bridge.World, TechCatalog.Recipe(92)) &&
                bridge.World.nuclearUraniumMined >= 4 && bridge.World.nuclearUraniumMined <= bridge.World.gathered,
                "selected world restores fuel studies, recipe unlocks and real mining evidence");
            Require(NuclearCatalog.All.Length == 11 && TechnologyTreeModel.All.Length == 80 &&
                NuclearCatalog.HasResearch(bridge.World, NuclearTech.RadiationMonitoring) &&
                NuclearCatalog.HasResearch(bridge.World, NuclearTech.RadiationProtection),
                "selected world restores radiation studies and the eleven-study nuclear catalog");
            var restoredSuit = EquipmentCatalog.Find(bridge.LocalPlayer, qaRadiationSuitId);
            Require(restoredSuit != null && restoredSuit.kind == GearKind.RadiationSuit &&
                restoredSuit.durability == radiationSuitDurability && bridge.LocalPlayer.armorIds[1] == restoredSuit.id &&
                Mathf.Approximately(RadiationCatalog.Protection(bridge.LocalPlayer), .8f),
                "selected world restores the crafted suit, durability and worn torso protection");
            VerifyRestoredEquipment(bridge.LocalPlayer);
            RequireQaRadiationHud();
            yield return PointAtWorld(32, 34);
            RequireDefaultHud();
            yield return Capture("restored");
            Debug.Log("DEMO_RUNTIME_QA_PASS menu=createConfirmation=lanNavigation=saveSelection=isolatedQaSave=collapsedTools=collapsedBuild=optInInspection=compactHud=inventory=help=keyboard=growth=technology=sevenEras=researchGates=build=inspection=factory=powerGrid=footprints=smoothCargo=equipment=attachments=reload=weaponDamage=save=load=true");
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
            for(int i=0;i<cost.Length;i++) if(i!=(int)ItemKind.NuclearWaste) player.items[i]=Math.Max(player.items[i],cost[i]+10);
        }
        static void PrepareQaPlot(WorldState world,bool clearBuildings)
        {
            world.zombies.Clear(); world.nextWaveAt=world.elapsed+9999;
            if (world.survival != null) world.survival.creatures.Clear();
            world.nodes.RemoveAll(n=>n.x>=17 && n.x<=52 && n.y>=12 && n.y<=32);
            char[] ground=world.ground.ToCharArray();
            var chunkGround=new Dictionary<long,char[]>();
            for(int y=12;y<=32;y++) for(int x=17;x<=52;x++)
            {
                char tile=(char)('0'+(int)(y==12?GroundKind.Water:GroundKind.Floor));
                ground[x+y*world.width]=tile;
                if(!ChunkStreaming.Enabled(world)) continue;
                int cx=WorldTiles.FloorDiv(x,ChunkStreaming.ChunkSize),cy=WorldTiles.FloorDiv(y,ChunkStreaming.ChunkSize);
                if(!WorldTiles.TryChunk(world,cx,cy,out var chunk)) throw new InvalidOperationException($"QA plot chunk missing: {cx},{cy}");
                long key=WorldTiles.Key(cx,cy);
                if(!chunkGround.TryGetValue(key,out var tiles))
                {
                    if(string.IsNullOrEmpty(chunk.ground) || chunk.ground.Length!=ChunkStreaming.ChunkSize*ChunkStreaming.ChunkSize)
                        throw new InvalidOperationException($"QA plot chunk terrain invalid: {cx},{cy}");
                    tiles=chunk.ground.ToCharArray();
                    chunkGround.Add(key,tiles);
                }
                int lx=x-cx*ChunkStreaming.ChunkSize,ly=y-cy*ChunkStreaming.ChunkSize;
                tiles[lx+ly*ChunkStreaming.ChunkSize]=tile;
            }
            world.ground=new string(ground);
            if(ChunkStreaming.Enabled(world))
            {
                foreach(var chunk in world.chunks.records)
                    if(chunk!=null && chunkGround.TryGetValue(WorldTiles.Key(chunk.x,chunk.y),out var tiles)) chunk.ground=new string(tiles);
                world.chunks.index=null; world.chunks.indexSource=null; world.chunks.indexedCount=0;
                world.chunks.revision++; world.chunks.budgetBlocked=false;
            }
            if(clearBuildings) { world.buildings.Clear(); world.cargo.Clear(); world.powerNetworks.Clear(); }
        }
        static void PrepareQaPrimitiveProgression(WorldState world)
        {
            // The QA world starts with the real primitive progression enabled; seed only
            // the prerequisites needed to exercise the later era ladder in isolation.
            world.primitiveResearch |=
                1 << (int)PrimitiveTech.Tools |
                1 << (int)PrimitiveTech.Charcoal |
                1 << (int)PrimitiveTech.Ceramics |
                1 << (int)PrimitiveTech.CopperTin |
                1 << (int)PrimitiveTech.Bronze |
                1 << (int)PrimitiveTech.Bloomery |
                1 << (int)PrimitiveTech.Construction;
            world.produced[(int)ItemKind.Bronze] = Mathf.Max(world.produced[(int)ItemKind.Bronze], 4);
            world.produced[(int)ItemKind.Iron] = Mathf.Max(world.produced[(int)ItemKind.Iron], 6);
            world.produced[(int)ItemKind.Brick] = Mathf.Max(world.produced[(int)ItemKind.Brick], 4);
            world.produced[(int)ItemKind.Charcoal] = Mathf.Max(world.produced[(int)ItemKind.Charcoal], 2);
        }
        static void PrepareQaWaterProgression(WorldState world)
        {
            // Seed only the measured water-era milestones so the QA can exercise
            // the real research buttons and host commands without simulating a
            // full production chain before the later-era checks.
            world.primitiveResearch |= 1 << (int)PrimitiveTech.CopperTin;
            world.waterCrushed = Math.Max(world.waterCrushed, 4);
            world.waterSmelted = Math.Max(world.waterSmelted, 6);
            world.waterPressed = Math.Max(world.waterPressed, 4);
            world.waterGears = Math.Max(world.waterGears, 2);
            world.waterBearings = Math.Max(world.waterBearings, 2);
            world.waterTransported = Math.Max(world.waterTransported, 4);
        }
        static void PrepareQaSteamProgression(WorldState world)
        {
            // Seed measured production evidence so each steam research button still
            // runs through the real requirement and host command path.
            world.steamCoked = Math.Max(world.steamCoked, 6);
            world.steamRefractory = Math.Max(world.steamRefractory, 8);
            world.steamPigIron = Math.Max(world.steamPigIron, 6);
            world.steamSteel = Math.Max(world.steamSteel, 4);
            world.steamPlates = Math.Max(world.steamPlates, 4);
            world.steamFittings = Math.Max(world.steamFittings, 4);
            world.steamDriven = Math.Max(world.steamDriven, 4);
        }
        static void PrepareQaLowVoltageProgression(WorldState world)
        {
            // Seed measured electrical production evidence so each low-voltage
            // research button still runs through the real requirement path.
            world.lvWire = Math.Max(world.lvWire, 12);
            world.lvInsulators = Math.Max(world.lvInsulators, 8);
            world.lvCable = Math.Max(world.lvCable, 8);
            world.lvCoils = Math.Max(world.lvCoils, 4);
            world.lvMotors = Math.Max(world.lvMotors, 2);
            world.lvRelays = Math.Max(world.lvRelays, 4);
            world.lvCircuits = Math.Max(world.lvCircuits, 6);
            world.lvElectric = Math.Max(world.lvElectric, 8);
            world.lvAutomated = Math.Max(world.lvAutomated, 4);
            world.lvSorted = Math.Max(world.lvSorted, 4);
            world.lvProtected = Math.Max(world.lvProtected, 4);
        }
        static void PrepareQaPetrochemicalProgression(WorldState world)
        {
            // Seed measured petrochemical milestones so each MV research button
            // still runs through the real prerequisites, evidence, and host command.
            world.mvSurvey = Math.Max(world.mvSurvey, 1);
            world.mvExtracted = Math.Max(world.mvExtracted, 12);
            world.mvPiped = Math.Max(world.mvPiped, 8);
            world.mvDistilled = Math.Max(world.mvDistilled, 12);
            world.mvDiesel = Math.Max(world.mvDiesel, 4);
            world.mvPolymer = Math.Max(world.mvPolymer, 4);
            world.mvLubricant = Math.Max(world.mvLubricant, 2);
            world.mvCooled = Math.Max(world.mvCooled, 4);
            world.mvPowered = Math.Max(world.mvPowered, 4);
        }
        static void PrepareQaPrecisionProgression(WorldState world)
        {
            // Seed measured HV production evidence so each precision research
            // button still runs through the real prerequisites and host command.
            world.produced[(int)ItemKind.IndustrialSilicon] = Math.Max(world.produced[(int)ItemKind.IndustrialSilicon], 4);
            world.produced[(int)ItemKind.PureSilicon] = Math.Max(world.produced[(int)ItemKind.PureSilicon], 2);
            world.produced[(int)ItemKind.Wafer] = Math.Max(world.produced[(int)ItemKind.Wafer], 4);
            world.produced[(int)ItemKind.Semiconductor] = Math.Max(world.produced[(int)ItemKind.Semiconductor], 4);
            world.produced[(int)ItemKind.AdvancedCircuit] = Math.Max(world.produced[(int)ItemKind.AdvancedCircuit], 2);
            world.produced[(int)ItemKind.PrecisionPart] = Math.Max(world.produced[(int)ItemKind.PrecisionPart], 4);
            world.produced[(int)ItemKind.ControlModule] = Math.Max(world.produced[(int)ItemKind.ControlModule], 2);
            world.produced[(int)ItemKind.HeatAlloy] = Math.Max(world.produced[(int)ItemKind.HeatAlloy], 4);
        }
        static void PrepareQaNuclearProgression(WorldState world)
        {
            // Seed the measured HV acceptance that gates the nuclear era.
            world.produced[(int)ItemKind.ControlModule] = Math.Max(world.produced[(int)ItemKind.ControlModule], 4);
            world.produced[(int)ItemKind.HeatAlloy] = Math.Max(world.produced[(int)ItemKind.HeatAlloy], 6);
            world.hvPowered = Math.Max(world.hvPowered, 4);
        }
        IEnumerator CheckQaTechnologyTree(GameBridge bridge)
        {
            int lineCount = RequireTechnologyTreeLayout();
            var era = bridge.World.era;
            var player = bridge.LocalPlayer;
            var items = (int[])player.items.Clone();
            var future = TechnologyTreeModel.All.Single(n => n.IsEra && n.era == (int)TechEra.Nuclear);
            var panel = Rect("Technology Panel");
            var content = Rect("Technology Tree Content");

            bool ShowsFutureDetails() => panel.GetComponentsInChildren<TMPro.TextMeshProUGUI>()
                .Any(label => label.isActiveAndEnabled && !label.transform.IsChildOf(content) &&
                    label.GetComponentInParent<Button>() == null &&
                    label.text?.Contains(future.name) == true);

            Require(!ShowsFutureDetails(), "technology details start on a different node");
            Click("Era Node " + future.era);
            yield return new WaitForSecondsRealtime(.2f);
            Require(ShowsFutureDetails(), "clicking a locked future node updates the details");
            Require(bridge.World.era == era && player.items.SequenceEqual(items) && !FindButton("Research Button").interactable,
                "selecting a future node preserves research and inventory and disables confirmation");

            yield return Press(Key.T);
            Require(!Active("Technology Panel"), "technology tree closes");
            yield return Press(Key.T);
            Require(Active("Technology Panel"), "technology tree reopens");
            Require(RequireTechnologyTreeLayout() == lineCount, "reopening the technology tree preserves connection count");

            Click("Technology Domain Weapon");
            yield return null;
            Require(FindButton("Technology Domain Weapon").colors.normalColor == new Color32(31, 77, 72, 255),
                "weapon domain filter is visibly selected");
            int visibleWeaponNodes = content.GetComponentsInChildren<Button>(false).Count(button =>
                button.name.StartsWith("Weapon Research ", StringComparison.Ordinal) || button.name.StartsWith("Era Node ", StringComparison.Ordinal));
            Require(visibleWeaponNodes == 19, "weapon filter shows twelve studies and seven era entries");
            Click("Technology Domain Tool");
            yield return null;
            int visibleToolNodes = content.GetComponentsInChildren<Button>(false).Count(button =>
                button.name.StartsWith("Tool Research ", StringComparison.Ordinal) || button.name.StartsWith("Era Node ", StringComparison.Ordinal));
            Require(visibleToolNodes == 9, "tool filter shows two studies and seven era entries");
            Click("Technology Domain All");
            yield return null;
        }

        static int RequireTechnologyTreeLayout()
        {
            var panel = Rect("Technology Panel");
            var viewport = Rect("Technology Tree Viewport");
            var content = Rect("Technology Tree Content");
            var scroll = viewport.GetComponentInParent<ScrollRect>();
            Require(viewport.IsChildOf(panel) && scroll != null && scroll.isActiveAndEnabled && scroll.horizontal && scroll.vertical &&
                scroll.viewport == viewport && scroll.content == content && content.IsChildOf(viewport) &&
                viewport.GetComponent<RectMask2D>() != null, "technology tree has a masked two-axis viewport");
            Require(content.GetComponent<LayoutGroup>() == null && content.GetComponent<ContentSizeFitter>() == null,
                "technology tree content allows manual node placement");

            var expected = new HashSet<string>(TechnologyTreeModel.All.Select(DemoHud.TechnologyNodeName));
            var nodes = content.GetComponentsInChildren<Button>(true);
            Require(TechnologyTreeModel.All.Length == 80 && TechnologyTreeModel.All.Count(node => !node.IsEra) == 73 &&
                nodes.Length == TechnologyTreeModel.All.Length && expected.SetEquals(nodes.Select(b => b.name)) &&
                nodes.All(b => b.gameObject.activeInHierarchy), "technology tree has one active button per model node");

            foreach (TechnologyTreeNode node in TechnologyTreeModel.All.Where(value => !value.IsEra && value.domain != ResearchDomain.Industry))
                Require(node.parents.Length == 0 || node.parents.All(parent => parent != node.era * 100),
                    "equipment node uses direct parents without a redundant era edge: " + node.name);

            var lines = content.GetComponentsInChildren<TechnologyTreeLines>(true);
            Require(lines.Length == 1 && lines[0].isActiveAndEnabled &&
                lines[0].GetComponent<CanvasRenderer>() != null && lines[0].paths.Count > 0,
                "technology tree has one active renderer with dependency connections");
            return lines[0].paths.Count;
        }

        IEnumerator ResearchQaEquipment(GameBridge bridge, ResearchDomain domain, int id)
        {
            string filter = domain == ResearchDomain.Weapon ? "Weapon" : "Tool";
            Click("Technology Domain " + filter);
            yield return null;
            var node = TechnologyTreeModel.All.Single(n => !n.IsEra && n.domain == domain && n.researchId == id);
            var definition = domain == ResearchDomain.Weapon ? WeaponTechCatalog.All[id] : ToolTechCatalog.All[id];
            for (int item = 0; item < definition.production.Length; item++)
                bridge.World.produced[item] = Math.Max(bridge.World.produced[item], definition.production[item]);
            GrantSamples(bridge.LocalPlayer, definition.cost);
            yield return ClickScrolled(DemoHud.TechnologyNodeName(node));
            yield return new WaitForSecondsRealtime(.2f);
            string missing = EquipmentResearchCatalog.Requirement(bridge.World, bridge.LocalPlayer,
                new ResearchRef { domain = domain, era = definition.era, id = id });
            Require(FindButton("Research Button").interactable && string.IsNullOrEmpty(missing),
                "equipment study shows the real research prerequisites: " + node.name + " / " + missing);
            Click("Research Button");
            yield return new WaitForSecondsRealtime(.7f);
            int mask = domain == ResearchDomain.Weapon ? bridge.World.weaponResearch : bridge.World.toolResearch;
            Require((mask & (1 << id)) != 0, "equipment research button did not update authoritative world state: " + node.name);
            Click("Technology Domain All");
            yield return null;
        }

        IEnumerator CheckQaNuclearResearch(GameBridge bridge)
        {
            var world = bridge.World;
            var player = bridge.LocalPlayer;
            var workbench = world.buildings.First(b => b.kind == BuildKind.Workbench);
            player.x = DemoCatalog.CenterX(workbench);
            player.y = DemoCatalog.CenterY(workbench) + 1.1f;

            int beneficiationId = (int)NuclearTech.UraniumBeneficiation;
            int researchId = (int)NuclearTech.UraniumEnrichment;
            var beneficiationRecipe = TechCatalog.Recipe(93);
            var recipe = TechCatalog.Recipe(92);
            GrantSamples(player, NuclearCatalog.Definition(beneficiationId).cost);
            GrantSamples(player, NuclearCatalog.Definition(researchId).cost);
            yield return SelectQaTechnologyEra((int)TechEra.Nuclear);
            yield return new WaitForSecondsRealtime(.6f);
            Require(TechnologyTreeModel.All.Count(n => !n.IsEra && n.era == (int)TechEra.Nuclear) == NuclearCatalog.All.Length &&
                NuclearCatalog.All.Length == 11 && TechnologyTreeModel.All.Count(n => !n.IsEra && n.domain == ResearchDomain.Industry && n.era == (int)TechEra.Nuclear) == 11 &&
                TechnologyTreeModel.All.Count(n => !n.IsEra) == 73 && TechnologyTreeModel.All.Length == 80,
                "technology tree includes eleven nuclear industrial studies, 73 studies and seven eras");
            Click("Nuclear Research " + beneficiationId);
            yield return null;
            Require(world.nuclearUraniumMined == 0 && !FindButton("Research Button").interactable &&
                !PrimitiveCatalog.IsRecipeUnlocked(world, beneficiationRecipe),
                "beneficiation stays locked without real mining even with research samples");
            bridge.Send(new PlayerCommand { action = "nuclear", arg = beneficiationId });
            yield return new WaitForSecondsRealtime(.3f);
            Require(!NuclearCatalog.HasResearch(world, beneficiationId), "authority rejects beneficiation without mining evidence");

            // This isolated QA plot was cleared by PrepareQaPlot. Only real interact commands
            // generate mining evidence; never seed nuclearUraniumMined or gathered.
            const int mineX = 51, mineY = 31;
            Require(!world.nodes.Any(n => n.x == mineX && n.y == mineY) &&
                !world.buildings.Any(b => DemoCatalog.Contains(b, mineX, mineY)), "QA uranium plot is free");
            world.nodes.Add(new NodeState { id = world.nextId++, x = mineX, y = mineY, kind = NodeKind.UraniumOre, stock = 4, hp = 35 });
            player.x = mineX; player.y = mineY + 1;
            int gatheredBefore = world.gathered;
            int uraniumBefore = player.items[(int)ItemKind.UraniumOre];
            for (int attempt = 0; attempt < 4 && world.nuclearUraniumMined < 4; attempt++)
            {
                yield return new WaitForSecondsRealtime(.5f);
                bridge.Send(new PlayerCommand { action = "interact", x = mineX, y = mineY });
                yield return null;
            }
            Require(world.nuclearUraniumMined == 4 && world.gathered >= gatheredBefore + 4 &&
                player.items[(int)ItemKind.UraniumOre] == uraniumBefore + 4 &&
                world.nuclearUraniumMined <= world.gathered, "real uranium interaction records saveable mining and gathered evidence");
            player.x = DemoCatalog.CenterX(workbench);
            player.y = DemoCatalog.CenterY(workbench) + 1.1f;
            yield return new WaitForSecondsRealtime(.6f);
            Require(FindButton("Research Button").interactable, "beneficiation becomes researchable at the workbench after mining");
            Click("Research Button");
            yield return new WaitForSecondsRealtime(.7f);
            Require(NuclearCatalog.HasResearch(world, beneficiationId) && PrimitiveCatalog.IsRecipeUnlocked(world, beneficiationRecipe) &&
                PrimitiveCatalog.IsRecipeUnlocked(world, TechCatalog.Recipe(90)), "beneficiation opens chemical extraction and preserves startup fuel access");
            Click("Nuclear Research " + researchId);
            yield return null;
            Require(world.produced[(int)ItemKind.NuclearFuel] < 2 && !FindButton("Research Button").interactable &&
                !PrimitiveCatalog.IsRecipeUnlocked(world, recipe), "enrichment still requires fuel production after beneficiation");
            bridge.Send(new PlayerCommand { action = "nuclear", arg = researchId });
            yield return new WaitForSecondsRealtime(.3f);
            Require(!NuclearCatalog.HasResearch(world, researchId), "authority rejects enrichment without fuel production evidence");

            // Fuel production is a fixture here, not end-to-end chemical/fuel manufacturing.
            // Verify that production chain separately in core checks; this QA covers HUD/authority gates.
            world.produced[(int)ItemKind.NuclearFuel] = 2;
            yield return new WaitForSecondsRealtime(.2f);
            Click("Research Button");
            yield return new WaitForSecondsRealtime(.7f);
            Require(NuclearCatalog.HasResearch(world, researchId) && PrimitiveCatalog.IsRecipeUnlocked(world, recipe),
                "nuclear research button unlocks the efficient fuel recipe");
            Require(!Active("Radiation Status"), "safe HUD hides unresearched radiation readings");
            GrantSamples(player, EquipmentCatalog.Gear(GearKind.RadiationSuit).cost);
            yield return Press(Key.T);
            yield return ClickHudTool("Open Inventory");
            Click("Inventory Tab Craft");
            yield return new WaitForSecondsRealtime(.3f);
            string suitRequirement = EquipmentCatalog.CraftRequirement(world, player, GearKind.RadiationSuit);
            Require(!FindButton("Gear Craft " + (int)GearKind.RadiationSuit).interactable && suitRequirement.Contains("核作业防护") &&
                Rect("Gear Craft " + (int)GearKind.RadiationSuit + " Row").GetComponentsInChildren<TMPro.TextMeshProUGUI>()
                    .Any(label => label.text != null && label.text.Contains(suitRequirement)),
                "suit crafting UI shows the authoritative protection research gate despite sufficient materials");
            Click("Close Inventory");
            yield return Press(Key.T);
            foreach (var study in new[] { NuclearTech.RadiationMonitoring, NuclearTech.RadiationProtection })
            {
                GrantSamples(player, NuclearCatalog.Definition((int)study).cost);
                yield return new WaitForSecondsRealtime(.6f);
                yield return ClickScrolled("Nuclear Research " + (int)study);
                yield return new WaitForSecondsRealtime(.2f);
                Require(FindButton("Research Button").interactable && world.nuclearUraniumMined == 4 &&
                    world.produced[(int)ItemKind.NuclearWaste] == 0 && !world.buildings.Any(b => b.kind == BuildKind.Reactor),
                    "radiation preparation is available before first reactor startup: " + study);
                var evidence = Rect("Technology Panel").GetComponentsInChildren<TMPro.TextMeshProUGUI>()
                    .FirstOrDefault(label => label.text != null && label.text.Contains("首堆装料前即可研究"));
                Require(evidence != null && !evidence.text.Contains("核燃料 2"),
                    "radiation preparation progress shows mining evidence without a fuel requirement");
                Click("Research Button");
                yield return new WaitForSecondsRealtime(.7f);
                Require(NuclearCatalog.HasResearch(world, study), "radiation study completes through HUD: " + study);
            }
            yield return Press(Key.T);
            RequireQaRadiationHud();
            GrantSamples(player, EquipmentCatalog.Gear(GearKind.RadiationSuit).cost);
            yield return ClickHudTool("Open Inventory");
            Click("Inventory Tab Craft");
            yield return new WaitForSecondsRealtime(.3f);
            yield return ClickScrolled("Gear Craft " + (int)GearKind.RadiationSuit);
            yield return new WaitForSecondsRealtime(.7f);
            var suit = player.gear.SingleOrDefault(g => g.kind == GearKind.RadiationSuit);
            Require(suit != null && suit.durability == EquipmentCatalog.Gear(suit.kind).maxDurability && suit.durability == 150,
                "craft radiation suit at its 150 durability maximum through the real inventory recipe button");
            qaRadiationSuitId = suit.id;
            Click("Close Inventory");
            yield return null;
            yield return WearQaRadiationSuit(bridge);
            Debug.Log("DEMO_NUCLEAR_TREE_QA_PASS evidenceGate=true recipeUnlock=true monitoring=true protection=true suitCraftEquip=true");
        }

        IEnumerator WearQaRadiationSuit(GameBridge bridge)
        {
            yield return ClickHudTool("Open Inventory");
            Click("Inventory Tab Equipment");
            yield return new WaitForSecondsRealtime(.25f);
            yield return ClickScrolled("Gear Slot " + qaRadiationSuitId);
            yield return new WaitForSecondsRealtime(.2f);
            RequireButtonIcon("Gear Slot " + qaRadiationSuitId, DemoArt.GetIcon("gear_17").name);
            yield return ClickScrolled("Equip Selected Gear");
            yield return new WaitForSecondsRealtime(.4f);
            var player = bridge.LocalPlayer;
            Require(player.armorIds[1] == qaRadiationSuitId && Mathf.Approximately(RadiationCatalog.Protection(player), .8f),
                "inventory equips the radiation suit in the torso slot with eighty percent protection");
            var suit = EquipmentCatalog.Find(player, qaRadiationSuitId);
            string durability = suit.durability + " / " + EquipmentCatalog.Gear(suit.kind).maxDurability;
            Require(Rect("Selected Item Quantity").GetComponent<TMPro.TextMeshProUGUI>().text.Contains(durability) &&
                FindButton("Armor Slot 1").GetComponentInChildren<TMPro.TextMeshProUGUI>().text.Contains(durability),
                "suit details and worn slot show current durability against the catalog maximum");
            var layers = new SurvivorLayer[SurvivorVisuals.Capacity];
            for (int facing = 0; facing < 4; facing++)
            {
                int count = SurvivorVisuals.Sample(player, player.appearance, facing, 0f, false, true, layers, true);
                Require(count > 0 && layers.Take(count).Any(layer => layer.sprite != null &&
                    layer.sprite.name == "avatar_armor_17_torso_" + facing),
                    "radiation suit safely reuses tactical torso art in world and inventory preview: " + facing);
            }
            yield return Capture("equipment-radiation-suit");
            Click("Close Inventory");
            yield return new WaitForSecondsRealtime(.25f);
            RequireQaRadiationHud();
        }

        static void RequireQaRadiationHud()
        {
            var label = Rect("Radiation Status").GetComponent<TMPro.TextMeshProUGUI>();
            Require(label.isActiveAndEnabled && label.text.Contains("环境暴露") && label.text.Contains("累计暴露") &&
                label.text.Contains("防护效率") && !label.raycastTarget,
                "monitored HUD shows exposure rate, accumulated dose and protection without capturing world input");
            Canvas.ForceUpdateCanvases();
            label.ForceMeshUpdate();
            Require(label.rectTransform.rect.width <= 540f && label.rectTransform.rect.height <= 36f &&
                label.rectTransform.rect.height > 0 && !label.isTextOverflowing,
                "radiation readings wrap inside the existing compact feedback area");
        }

        IEnumerator ResearchQaWater(GameBridge bridge, WaterTech id)
        {
            var player = bridge.LocalPlayer;
            int index = (int)id;
            GrantSamples(player, WaterCatalog.Definition(index).cost);
            yield return SelectQaTechnologyEra((int)TechEra.Water);
            yield return new WaitForSecondsRealtime(.6f);
            Click("Water Research " + index);
            yield return new WaitForSecondsRealtime(.2f);
            Click("Research Button");
            yield return new WaitForSecondsRealtime(.7f);
            Require(WaterCatalog.HasResearch(bridge.World, id), "water research command: " + id + " " + player.notice);
        }
        IEnumerator ResearchQaSteam(GameBridge bridge, SteamTech id)
        {
            var player = bridge.LocalPlayer;
            int index = (int)id;
            GrantSamples(player, SteamCatalog.Definition(index).cost);
            yield return SelectQaTechnologyEra((int)TechEra.Steam);
            yield return new WaitForSecondsRealtime(.6f);
            Click("Steam Research " + index);
            yield return new WaitForSecondsRealtime(.2f);
            Click("Research Button");
            yield return new WaitForSecondsRealtime(.7f);
            Require(SteamCatalog.HasResearch(bridge.World, id), "steam research command: " + id + " " + player.notice);
        }
        IEnumerator ResearchQaLowVoltage(GameBridge bridge, LowVoltageTech id)
        {
            var player = bridge.LocalPlayer;
            int index = (int)id;
            GrantSamples(player, LowVoltageCatalog.Definition(index).cost);
            yield return SelectQaTechnologyEra((int)TechEra.LowVoltage);
            yield return new WaitForSecondsRealtime(.6f);
            Click("Low Voltage Research " + index);
            yield return new WaitForSecondsRealtime(.2f);
            Click("Research Button");
            yield return new WaitForSecondsRealtime(.7f);
            Require(LowVoltageCatalog.HasResearch(bridge.World, id), "low voltage research command: " + id + " " + player.notice);
        }
        IEnumerator ResearchQaPetrochemical(GameBridge bridge, PetrochemicalTech id)
        {
            var player = bridge.LocalPlayer;
            int index = (int)id;
            GrantSamples(player, PetrochemicalCatalog.Definition(index).cost);
            yield return SelectQaTechnologyEra((int)TechEra.MediumVoltage);
            yield return new WaitForSecondsRealtime(.6f);
            Click("Petrochemical Research " + index);
            yield return new WaitForSecondsRealtime(.2f);
            Click("Research Button");
            yield return new WaitForSecondsRealtime(.7f);
            Require(PetrochemicalCatalog.HasResearch(bridge.World, id), "petrochemical research command: " + id + " " + player.notice);
        }
        IEnumerator ResearchQaPrecision(GameBridge bridge, PrecisionTech id)
        {
            var player = bridge.LocalPlayer;
            int index = (int)id;
            GrantSamples(player, PrecisionCatalog.Definition(index).cost);
            yield return SelectQaTechnologyEra((int)TechEra.HighVoltage);
            yield return new WaitForSecondsRealtime(.6f);
            Click("Precision Node " + index);
            yield return null;
            yield return new WaitForSecondsRealtime(.2f);
            Click("Research Button");
            yield return new WaitForSecondsRealtime(.7f);
            Require(PrecisionCatalog.HasResearch(bridge.World, id), "precision research command: " + id + " " + player.notice);
        }
        IEnumerator SelectQaTechnologyEra(int era)
        {
            Require(Active("Technology Panel"), "technology panel remains open for era " + era);
            Click("Technology Era " + era);
            yield return null;
            yield return new WaitForSecondsRealtime(.1f);
            Require(Active("Technology Panel"), "technology panel refreshes for era " + era);
        }
        IEnumerator BuildQaNuclearFacility(GameBridge bridge)
        {
            yield return BuildQa(bridge,BuildKind.HighGenerator,36,16);
            yield return BuildQa(bridge,BuildKind.HighPole,41,16);
            yield return BuildQa(bridge,BuildKind.ReactorControlConsole,42,16);
            yield return BuildQa(bridge,BuildKind.DieselGenerator,42,13);
            yield return BuildQa(bridge,BuildKind.CoolingTower,45,13);
            yield return BuildQa(bridge,BuildKind.Reactor,45,16);
            foreach(var pipe in new[] {(48,13,1,8),(48,14,1,8),(48,15,2,8),(47,15,1,8),(46,15,3,7)})
            {
                yield return BuildQa(bridge,BuildKind.LiquidPipe,pipe.Item1,pipe.Item2,pipe.Item3);
                bridge.Send(new PlayerCommand { action="configure",x=pipe.Item1,y=pipe.Item2,arg=pipe.Item4 });
                yield return new WaitForSecondsRealtime(.3f);
            }
            yield return BuildQa(bridge,BuildKind.NuclearWasteVault,48,16);
            var player=bridge.LocalPlayer;
            player.x=41;player.y=15;
            bridge.Send(new PlayerCommand { action="configure",x=42,y=16,arg=1 });
            var diesel=bridge.World.buildings.Single(b=>b.kind==BuildKind.DieselGenerator);
            diesel.items[(int)ItemKind.Diesel]=8;
            var tower=bridge.World.buildings.Single(b=>b.kind==BuildKind.CoolingTower);
            tower.items[(int)ItemKind.ProcessWater]=20;
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
        IEnumerator BuildQa(GameBridge bridge,BuildKind kind,int x,int y,int rotation=0)
        {
            var player=bridge.LocalPlayer;
            GrantSamples(player,DemoCatalog.BuildCost(kind));
            bridge.Send(new PlayerCommand());
            var candidates=new[]{new Vector2(x,y-1.2f),new Vector2(x-1.2f,y),new Vector2(x,y+DemoCatalog.Height(kind,rotation)+.2f)};
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
            // Ambient zombies spawn even with waves disabled. Keep the construction fixture deterministic.
            bridge.World.zombies.Clear();
            bridge.Send(new PlayerCommand { action="build",arg=(int)kind,x=x,y=y,rotation=rotation });
            Require(bridge.World.buildings.Any(b=>b.kind==kind && b.x==x && b.y==y),"actual build command: "+kind+" "+player.notice);
            yield return new WaitForSecondsRealtime(.3f);
        }
        static Button FindButton(string name) => FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(b=>b.name==name);
        static bool PointerTargetsButton(Button button)
        {
            if (button == null || EventSystem.current == null || !(button.transform is RectTransform rect)) return false;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * .5f) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            return hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button;
        }
        static TMPro.TMP_InputField InputField(string name) => FindObjectsByType<TMPro.TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(f => f.name == name);
        static RectTransform Rect(string name) => FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(r => r.name == name);
        static bool Active(string name) => Rect(name).gameObject.activeInHierarchy;
        static void RequireDefaultHud()
        {
            foreach (string name in new[] { "Main Menu", "Inventory Panel", "Session Modal", "Help Panel", "Growth Modal", "Technology Panel", "Settings Panel", "HUD Tools Tray", "Build Hotbar", "Build Details", "Mouse Inspector" })
                Require(!Active(name), "default HUD hides " + name);
            Require(Active("HUD Tools Toggle") && Active("HUD Build Toggle"), "collapsed HUD keeps its explicit expansion controls");
            var inspect = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == "Inspect Toggle");
            Require(inspect.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text == "检查：关", "default HUD keeps inspection disabled");
            RequireButtonIcon("HUD Build Toggle", "ui_create");
            RequireButtonIcon("HUD Tools Toggle", "ui_settings");
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * .5f, Screen.height * .5f) }, hits);
            Require(hits.Count == 0, "default HUD leaves world centre interactive");
        }
        static IEnumerator ClickHudTool(string name)
        {
            if (!Active("HUD Tools Tray"))
            {
                Click("HUD Tools Toggle");
                yield return null;
            }
            Require(Active("HUD Tools Tray"), "HUD tools must be expanded before " + name);
            Click(name);
            yield return null;
            Require(!Active("HUD Tools Tray"), "opening " + name + " collapses the tools tray");
        }
        static IEnumerator SetQaInspector(bool enabled)
        {
            var toggle = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == "Inspect Toggle");
            var label = toggle.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (label.text != (enabled ? "检查：开" : "检查：关"))
                yield return ClickHudTool("Inspect Toggle");
            Require(label.text == (enabled ? "检查：开" : "检查：关"), "inspection toggle reflects the requested state");
            if (!enabled) Require(!Active("Mouse Inspector"), "disabling inspection hides its panel");
        }
        static IEnumerator ClickScrolled(string name)
        {
            var button = FindButton(name);
            var scroll = button.GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                Require(scroll.content != null && scroll.viewport != null, "scroll structure for " + name);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                Canvas.ForceUpdateCanvases();
                var target = button.GetComponent<RectTransform>();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, target);
                var view = scroll.viewport.rect;
                float shift = bounds.min.y < view.yMin ? view.yMin - bounds.min.y : bounds.max.y > view.yMax ? view.yMax - bounds.max.y : 0f;
                // Nuclear preparation nodes can be in a later column of the two-axis tree.
                float shiftX = !scroll.horizontal ? 0f : bounds.min.x < view.xMin ? view.xMin - bounds.min.x :
                    bounds.max.x > view.xMax ? view.xMax - bounds.max.x : 0f;
                scroll.StopMovement();
                Vector3 delta = scroll.content.parent.InverseTransformVector(scroll.viewport.TransformVector(new Vector3(shiftX, shift, 0)));
                scroll.content.anchoredPosition += new Vector2(delta.x, delta.y);
                yield return null;
                Canvas.ForceUpdateCanvases();
                bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, target);
                Require(bounds.min.y >= view.yMin - 1f && bounds.max.y <= view.yMax + 1f &&
                    bounds.min.x >= view.xMin - 1f && bounds.max.x <= view.xMax + 1f, "scrolled button is inside its viewport: " + name);
            }
            Click(name);
            yield return null;
        }
        static IEnumerator Press(Key key)
        {
            var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>("Demo QA Keyboard");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            float deadline = Time.realtimeSinceStartup + 1f;
            while (!keyboard[key].isPressed && Time.realtimeSinceStartup < deadline) yield return null;
            Require(keyboard[key].isPressed, "QA key press reached Input System: " + key);
            yield return new WaitForSecondsRealtime(.05f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            deadline = Time.realtimeSinceStartup + 1f;
            while (keyboard[key].isPressed && Time.realtimeSinceStartup < deadline) yield return null;
            Require(!keyboard[key].isPressed, "QA key release reached Input System: " + key);
            yield return new WaitForSecondsRealtime(.15f);
        }
        static IEnumerator PointAtWorld(float x, float y)
        {
            Camera camera = Camera.main ?? FindAnyObjectByType<Camera>();
            Require(camera != null, "QA camera");
            Mouse mouse = Mouse.current;
            if (mouse == null) mouse = qaMouse = InputSystem.AddDevice<Mouse>("Demo QA Mouse");
            var point = camera.WorldToScreenPoint(new Vector3(x, y, 0));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(point.x, point.y) });
            yield return new WaitForSecondsRealtime(.2f);
        }
        static IEnumerator WaitForAppearanceEditing(GameBridge bridge)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!bridge.CanEditAppearance && Time.realtimeSinceStartup < deadline) yield return null;
            Require(bridge.CanEditAppearance, "network teardown releases appearance editing");
        }
        static void Click(string name)
        {
            var button = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(b => b.name == name);
            Require(button.interactable, "disabled button: " + name);
            button.onClick.Invoke();
        }
        static void RequireButtonIcon(string buttonName, string expectedSprite)
        {
            var button = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == buttonName);
            var icon = button.transform.Find("Item Icon")?.GetComponent<Image>();
            Require(icon != null && icon.sprite != null && icon.sprite.name == expectedSprite && !icon.raycastTarget,
                "button icon loads and leaves clicks to its button: " + buttonName + " / " + expectedSprite);
        }
        static void RequireInventoryLayout()
        {
            RectTransform card = Rect("Inventory Card");
            RectTransform browser = Rect("Inventory Browser Surface");
            RectTransform preview = Rect("Character Preview Surface");
            RectTransform equipment = Rect("Equipment Slots Surface");
            RectTransform detail = Rect("Inventory Detail Surface");
            Require(browser.rect.width > 300f && preview.rect.width > 300f && equipment.rect.width > 400f && detail.rect.width > 400f,
                "PUBG-style inventory keeps three usable columns");
            Require(browser.anchoredPosition.x < preview.anchoredPosition.x && preview.anchoredPosition.x < equipment.anchoredPosition.x,
                "inventory columns are ordered browser, character, equipment");
            Require(preview.anchoredPosition.y == equipment.anchoredPosition.y && detail.anchoredPosition.y < equipment.anchoredPosition.y,
                "equipment and detail stack in the right column");
            Rect browserBounds = RelativeBounds(card, browser);
            Rect previewBounds = RelativeBounds(card, preview);
            Rect equipmentBounds = RelativeBounds(card, equipment);
            Rect detailBounds = RelativeBounds(card, detail);
            foreach (var pair in new[] {
                new KeyValuePair<string, Rect>("browser", browserBounds),
                new KeyValuePair<string, Rect>("preview", previewBounds),
                new KeyValuePair<string, Rect>("equipment", equipmentBounds),
                new KeyValuePair<string, Rect>("detail", detailBounds) })
                Require(card.rect.Contains(pair.Value.min) && card.rect.Contains(pair.Value.max), "inventory surface stays inside card: " + pair.Key);
            Require(!browserBounds.Overlaps(previewBounds) && !browserBounds.Overlaps(equipmentBounds) &&
                !browserBounds.Overlaps(detailBounds) && !previewBounds.Overlaps(equipmentBounds) &&
                !previewBounds.Overlaps(detailBounds) && !equipmentBounds.Overlaps(detailBounds),
                "inventory columns do not overlap");
            RectTransform stage = Rect("Inventory Preview Stage");
            RectTransform origin = Rect("Inventory Preview Origin");
            Require(stage.rect.width > 0f && stage.rect.height > 0f && origin.parent == stage,
                "inventory preview stage has a positioned origin");
            var layers = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(image => image.name.StartsWith("Inventory Survivor Layer", StringComparison.Ordinal))
                .ToArray();
            Require(layers.Length == SurvivorVisuals.Capacity && layers.Count(image => image.enabled && image.sprite != null) > 0,
                "inventory preview paints at least one survivor layer");
            Require(FindButton("Weapon Equipment Slot") != null && FindButton("Tool Equipment Slot") != null &&
                FindButton("Armor Slot 0") != null && FindButton("Armor Slot 3") != null,
                "inventory preview exposes weapon, tool and armor slots");
        }
        static Rect RelativeBounds(RectTransform parent, RectTransform child)
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, child);
            return new Rect(bounds.min, bounds.size);
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
