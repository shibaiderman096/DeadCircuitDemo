#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DeadCircuit
{
    public sealed partial class DemoRuntimeChecks
    {
        // Opt-in integration checks exercise real Input System devices and the bridge,
        // with an explicit focus sample so unattended runs do not depend on OS focus.
        IEnumerator CheckPlayerControls(GameBridge bridge)
        {
            bridge.StartHost(73149, false);
            Require(bridge.IsRunning, "controls host starts");
            var sim = new WorldSimulator(new WorldState { seed = 73149, ground = new string('4', 4096),
                biomes = new string('0', 4096), generation = new WorldGenerationSettings { seed = 73149, chunked = false }, nextWaveAt = 10000 });
            var p = sim.AddPlayer(bridge.LocalPlayer.id, "Controls QA"); p.x = p.y = 32; p.activeSlot = 2;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GameBridge).GetField("simulator", flags).SetValue(bridge, sim);
            typeof(GameBridge).GetProperty("World").SetValue(bridge, sim.State);
            bridge.enabled = false;
            var read = typeof(GameBridge).GetMethod("ReadInputDevices", flags);
            var clock = typeof(GameBridge).GetField("inputClock", flags);
            var keyboard = InputSystem.AddDevice<Keyboard>("Controls QA Keyboard");
            qaMouse = InputSystem.AddDevice<Mouse>("Controls QA Mouse");
            yield return null; yield return null; yield return null;

            IEnumerator Sample(float x, float y, int ticks = 1, Key[] keys = null, bool left = false, bool right = false, bool focused = true)
            {
                for (int i = 0; i < ticks; i++)
                {
                    Vector3 point = bridge.GetComponent<DemoWorldView>().WorldCamera.WorldToScreenPoint(new Vector3(x, y, 0));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys ?? Array.Empty<Key>()));
                    InputSystem.QueueStateEvent(qaMouse, new MouseState { position = new Vector2(point.x, point.y),
                        buttons = (ushort)((left ? 1 : 0) | (right ? 2 : 0)) });
                    yield return null;
                    clock.SetValue(bridge, .05f); read.Invoke(bridge, new object[] { focused }); sim.Step(.05f);
                }
            }
            yield return Sample(37, 32, left: true); yield return Sample(37, 32, 55);
            Require(Math.Abs(p.x - 37) < .1f && Math.Abs(p.y - 32) < .1f, "left click moves to ground through idle heartbeats");
            Require(p.attackCooldown == 0, "ground click does not attack");
            var branch = new NodeState { id = sim.State.nextId++, x = 38, y = 32, kind = NodeKind.Branch, stock = 2, hp = 1 };
            sim.State.nodes.Add(branch);
            yield return Sample(38, 32, left: true); yield return Sample(38, 32, 12);
            Require(!sim.State.nodes.Contains(branch) && p.items[(int)ItemKind.Branch] == 2, "left click picks up branches with empty hands");
            var resource = new NodeState { id = sim.State.nextId++, x = 42, y = 32, kind = NodeKind.Tree, stock = 7, hp = 35 };
            sim.State.nodes.Add(resource); int wood = p.items[(int)ItemKind.Wood];
            yield return Sample(42, 32, left: true); yield return Sample(42, 32, 12);
            Require(resource.hp == 35 && p.items[(int)ItemKind.Wood] == wood, "bare hands cannot harvest tree");
            bridge.Send(new PlayerCommand { action = "equip", arg = p.gear.Single(g => g.kind == GearKind.StoneAxe).id });
            yield return Sample(42, 32, left: true); yield return Sample(42, 32, 55);
            Require(!sim.State.nodes.Contains(resource) && p.items[(int)ItemKind.Wood] == wood + 7, "left click approaches and gathers to depletion");

            resource = new NodeState { id = sim.State.nextId++, x = 43, y = 32, kind = NodeKind.Tree, stock = 100, hp = 80 };
            sim.State.nodes.Add(resource);
            yield return Sample(25, 25, 8, new[] { Key.Space });
            Require(resource.hp < 80 && resource.stock == 100, "space damages the tree without paying before depletion");
            yield return Sample(25, 25); float stoppedX = p.x, stoppedY = p.y; float nodeHp = resource.hp;
            yield return Sample(25, 25, 12);
            Require(resource.hp == nodeHp && p.x == stoppedX && p.y == stoppedY, "space release stops gathering and movement");
            yield return Sample(25, 25, 4, new[] { Key.Space, Key.D });
            Require(p.x > stoppedX && resource.hp == nodeHp && p.aimX > .9f, "WASD overrides work and faces movement");
            yield return Sample(25, 25); sim.State.nodes.Clear();

            bridge.Send(new PlayerCommand { action = "switch", arg = 2 });
            int food = p.items[(int)ItemKind.Food]; p.hunger = 20;
            yield return Sample(25, 25, 3, new[] { Key.F });
            Require(p.items[(int)ItemKind.Food] == food && p.attackCooldown == 0, "F without an enemy neither eats nor attacks");
            yield return Sample(25, 25); yield return Sample(25, 25, keys: new[] { Key.V });
            Require(p.items[(int)ItemKind.Food] == food - 1 && p.hunger > 50, "V eats a ration");
            yield return Sample(25, 25);
            var enemy = new ZombieState { id = sim.State.nextId++, x = p.x + 5, y = p.y, hp = 45 }; sim.State.zombies.Add(enemy);
            yield return Sample(25, 25, 65, new[] { Key.F });
            Require(!sim.State.zombies.Contains(enemy), "held F approaches and attacks an enemy");
            yield return Sample(25, 25);

            var door = new BuildingState { id = sim.State.nextId++, kind = BuildKind.Door, x = 46, y = 32,
                hp = WorldSimulator.BuildingMaxHealth(BuildKind.Door), footprintVersion = 1 }; sim.State.buildings.Add(door);
            yield return Sample(46, 32, right: true); yield return Sample(46, 32, 60);
            Require(door.setting == 1, "right click approaches and opens a door exactly once");
            bridge.BuildSelection = BuildKind.Wall;
            yield return Sample(46, 32, right: true); yield return Sample(46, 32, 8);
            Require(!bridge.BuildSelection.HasValue && door.setting == 1, "right click cancels building without interacting underneath");
            yield return Sample(46, 32, keys: new[] { Key.LeftAlt }, left: true);
            Require(p.attackCooldown > 0, "Alt and left button retain manual attacks");
            yield return Sample(46, 32);

            yield return Sample(50, 32, left: true);
            yield return Sample(50, 32, focused: false); stoppedX = p.x; stoppedY = p.y;
            yield return Sample(50, 32, 12, focused: false);
            Require(p.x == stoppedX && p.y == stoppedY, "focus loss cancels click movement");
            yield return Sample(50, 32);
            yield return ClickHudTool("Open Inventory");
            Require(bridge.GetComponent<DemoHud>().CapturesKeyboard, "inventory captures keyboard input");
            stoppedX = p.x; stoppedY = p.y;
            yield return Sample(50, 32, 8, new[] { Key.D, Key.Space, Key.F });
            Require(p.x == stoppedX && p.y == stoppedY, "modal blocks movement and held actions");
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(qaMouse);
            bridge.CancelPlayerControl();
            Debug.Log("DEMO_CONTROLS_QA_PASS click_move ground_pickup tool_gather hold_release wasd fight eat secondary build_cancel aim focus modal");
        }
    }
}
#endif
