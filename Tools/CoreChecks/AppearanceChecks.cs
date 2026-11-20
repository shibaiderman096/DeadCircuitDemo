using DeadCircuit;
using System.Text.Json;
using System.Text.Json.Nodes;

static partial class Checks
{
    static void AppearanceChecks()
    {
        Run("appearance entry applies menu preset or default and covers every catalog choice", () =>
        {
            var sim = new WorldSimulator(Flat(era: TechEra.Primitive));
            var player = sim.AddPlayer("appearance-options", "Options");
            Require(player.appearance == AppearanceCatalog.DefaultCode, "entry without a preset did not use the default");

            const int combinedPreset = 0x55d31d;
            sim.SetConnected(player.id, false);
            player = sim.AddPlayer(player.id, "Options", combinedPreset);
            Require(player.appearance == combinedPreset, "entry did not apply the menu preset");
            int[] counts = { 6, 6, 8, 3, 8, 8, 6, 3 };
            Require(AppearanceCatalog.FieldCount == 8 && combinedPreset == 0x55d31d, "appearance catalog contract changed");
            for (int field = 0; field < counts.Length; field++)
            {
                Require(AppearanceCatalog.Count(field) == counts[field] && !string.IsNullOrWhiteSpace(AppearanceCatalog.Label(field)),
                    "appearance field or option count changed");
                for (int value = 0; value < counts[field]; value++)
                {
                    Require(!string.IsNullOrWhiteSpace(AppearanceCatalog.Name(field, value)), "missing appearance choice name");
                    int selection = AppearanceCatalog.With(AppearanceCatalog.DefaultCode, field, value);
                    sim.SetConnected(player.id, false);
                    player = sim.AddPlayer(player.id, "Options", selection);
                    Require(player.appearance == selection && AppearanceCatalog.Get(player.appearance, field) == value,
                        "valid menu appearance choice was not applied on entry");
                }
            }
            Require(AppearanceCatalog.Name(1, 0) == "短发" && AppearanceCatalog.Name(1, 1) == "侧分" &&
                AppearanceCatalog.Name(1, 2) == "刺发" && AppearanceCatalog.Name(1, 3) == "齐颈" &&
                AppearanceCatalog.Name(1, 4) == "马尾" && AppearanceCatalog.Name(1, 5) == "光头", "hair style order changed");
            Require(AppearanceCatalog.Name(3, 0) == "短袖" && AppearanceCatalog.Name(3, 1) == "夹克" &&
                AppearanceCatalog.Name(3, 2) == "连帽衫", "top style order changed");
        });

        Run("appearance invalid entry presets reject atomically before mutation", () =>
        {
            var sim = new WorldSimulator(Flat(era: TechEra.Primitive));
            var player = sim.AddPlayer("appearance-atomic", "Atomic", 0x55d31d);
            foreach (int code in InvalidAppearanceCodes())
            {
                string before = Dump(sim.State);
                bool rejected = false;
                try { sim.AddPlayer("appearance-invalid-" + code, "Invalid", code); }
                catch (ArgumentException) { rejected = true; }
                Require(!AppearanceCatalog.IsValid(code) && rejected, "invalid new-player preset was accepted: " + code);
                Require(Dump(sim.State) == before, "invalid new-player preset mutated the world: " + code);
            }

            sim.SetConnected(player.id, false);
            string disconnected = Dump(sim.State);
            bool reentryRejected = false;
            try { sim.AddPlayer(player.id, "Atomic", -1); } catch (ArgumentException) { reentryRejected = true; }
            Require(reentryRejected && Dump(sim.State) == disconnected, "invalid re-entry preset mutated the saved player");
        });

        Run("appearance presets remain isolated and connected players cannot change through entry", () =>
        {
            var sim = new WorldSimulator(Flat(era: TechEra.Primitive));
            var first = sim.AddPlayer("appearance-isolation-a", "A", 0x55d31d);
            var second = sim.AddPlayer("appearance-isolation-b", "B", 1);
            Require(ReferenceEquals(sim.AddPlayer(first.id, "A", 2), first), "connected role did not reuse its player");
            Require(first.appearance == 0x55d31d && second.appearance == 1, "entry preset changed a connected or different player");
            Require(ReferenceEquals(sim.AddPlayer(second.id, "B"), second) && second.appearance == 1,
                "omitted preset changed an existing connected player's appearance");
        });

        Run("appearance disconnect re-entry adopts a supplied preset or retains the saved value", () =>
        {
            var sim = new WorldSimulator(Flat(era: TechEra.Primitive));
            var first = sim.AddPlayer("appearance-reentry-a", "A", 0x55d31d);
            var second = sim.AddPlayer("appearance-reentry-b", "B", 1);
            sim.SetConnected(first.id, false);
            first = sim.AddPlayer(first.id, "A", 2);
            Require(first.connected && first.appearance == 2 && second.appearance == 1, "valid re-entry preset was not isolated");
            sim.SetConnected(first.id, false);
            first = sim.AddPlayer(first.id, "A");
            Require(first.connected && first.appearance == 2 && second.appearance == 1, "re-entry without a preset lost saved appearance");
        });

        Run("appearance game commands are always ignored", () =>
        {
            var sim = new WorldSimulator(Flat(era: TechEra.Primitive));
            var player = sim.AddPlayer("appearance-command", "Command", 0x55d31d);
            foreach (int code in new[] { AppearanceCatalog.DefaultCode, 1, 0x55d31d, -1, int.MaxValue })
            {
                string before = Dump(sim.State);
                sim.Apply(Command(player, "appearance", arg: code));
                Require(Dump(sim.State) == before && player.appearance == 0x55d31d,
                    "game appearance command changed player or world state: " + code);
            }
        });

        Run("appearance catalog guards invalid fields and choices", () =>
        {
            const int code = 0x55d31d;
            for (int field = 0; field < 8; field++)
            {
                Require(AppearanceCatalog.With(code, field, -1) == code &&
                    AppearanceCatalog.With(code, field, AppearanceCatalog.Count(field)) == code, "invalid choice changed the code");
                Require(AppearanceCatalog.Name(field, -1) == "" &&
                    AppearanceCatalog.Name(field, AppearanceCatalog.Count(field)) == "", "invalid choice had a name");
            }
            foreach (int field in new[] { -1, 8, 16, int.MinValue, int.MaxValue })
            {
                Require(AppearanceCatalog.Count(field) == 0 && AppearanceCatalog.Get(code, field) == 0, "invalid field was readable");
                Require(AppearanceCatalog.With(code, field, 0) == code && AppearanceCatalog.With(code, field, 1) == code,
                    "invalid field changed the code");
                Require(AppearanceCatalog.Label(field) == "" && AppearanceCatalog.Name(field, 0) == "", "invalid field had a label or choice");
            }
            Require(AppearanceCatalog.Get(-1, 0) == 0 && AppearanceCatalog.With(-1, 0, 1) == 0, "invalid source code had no safe fallback");
        });

        Run("appearance save snapshots and archives preserve player presets", () =>
        {
            var sim = new WorldSimulator(Flat(era: TechEra.Primitive));
            var first = sim.AddPlayer("appearance-save-a", "A", 0x55d31d);
            var second = sim.AddPlayer("appearance-save-b", "B", 1);
            var saved = Saved(sim.State);
            Require(saved.version == WorldSimulator.SaveVersion && new WorldState().version == WorldSimulator.SaveVersion,
                "appearance changed the current save contract");
            string before = Dump(saved);
            var restored = new WorldSimulator(saved);
            Require(restored.State.players[0].appearance == 0x55d31d && restored.State.players[1].appearance == 1,
                "restore lost or mixed player appearances");
            Require(Dump(saved) == before, "restoring appearance mutated the caller's save");

            var snapshotSim = new WorldSimulator(new WorldGenerationSettings { seed = 14, chunked = true });
            var snapshotFirst = snapshotSim.AddPlayer("appearance-snapshot-a", "A", 0x55d31d);
            snapshotSim.AddPlayer("appearance-snapshot-b", "B", 1);
            string authority = Dump(snapshotSim.State);
            var snapshot = WorldSnapshot.ForPlayer(snapshotSim.State, snapshotFirst.id);
            string payload = Dump(snapshot);
            var received = Saved(snapshot);
            Require(snapshot.players[0].appearance == 0x55d31d && snapshot.players[1].appearance == 1 && Dump(snapshotSim.State) == authority,
                "world snapshot lost presets or mutated authority");
            snapshotSim.SetConnected(snapshotFirst.id, false);
            snapshotSim.AddPlayer(snapshotFirst.id, "A", 2);
            Require(received.players[0].appearance == 0x55d31d && received.players[1].appearance == 1 &&
                Dump(snapshot) == payload, "later re-entry mutated an earlier snapshot");

            var archive = ArchiveFixture(out _);
            string id = Guid.NewGuid().ToString("N");
            archive.Write(id, sim.State);
            Require(archive.Read(id).players[0].appearance == 0x55d31d && archive.Read(id).players[1].appearance == 1,
                "archive roundtrip lost a player's menu preset");
            first.appearance = -1;
            bool rejected = false;
            try { archive.Write(id, sim.State); } catch (ArgumentException) { rejected = true; }
            Require(rejected && archive.Read(id).players[0].appearance == 0x55d31d, "corrupt appearance replaced the last valid archive");
        });
    }

    static int[] InvalidAppearanceCodes() => new[]
    {
        -1, int.MinValue, int.MaxValue, 0x1000000, 0x10000000,
        6, 7, 48, 56,
        1536, 2048, 2560, 3072, 3584,
        1572864, 1835008,
        6291456, 8388608, 10485760, 12582912, 14680064
    };
}
