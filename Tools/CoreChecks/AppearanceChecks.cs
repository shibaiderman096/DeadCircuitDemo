using DeadCircuit;
using System.Text.Json;
using System.Text.Json.Nodes;

static partial class Checks
{
    static void AppearanceChecks()
    {
        Run("appearance valid customization reaches the addressed player", () =>
        {
            var sim = new WorldSimulator(Flat(era: TechEra.Primitive));
            var player = sim.AddPlayer("appearance-a", "A");
            sim.AddPlayer("appearance-b", "B");
            var command = JsonSerializer.Deserialize<PlayerCommand>(
                JsonSerializer.Serialize(Command(player, "appearance", arg: 0x55d31d), Json), Json);
            sim.Apply(command);
            var players = JsonNode.Parse(Dump(sim.State))["players"].AsArray();
            Require(players[0]["appearance"]?.GetValue<int>() == 0x55d31d, "valid appearance command was not applied");
            Require(players[1]["appearance"]?.GetValue<int>() == 0, "customization changed another player's default");
        });
        Run("appearance catalog choices build the stable command layout", () =>
        {
            var sim = new WorldSimulator(Flat(era: TechEra.Primitive));
            var player = sim.AddPlayer("appearance-catalog", "Catalog");
            int[] counts = { 6, 6, 8, 3, 8, 8, 6, 3 };
            int[] choices = { 5, 3, 4, 1, 5, 3, 5, 2 };
            int code = AppearanceCatalog.DefaultCode;
            Require(AppearanceCatalog.FieldCount == 8 && code == 0, "appearance catalog contract changed");
            for (int field = 0; field < 8; field++)
            {
                Require(AppearanceCatalog.Count(field) == counts[field], "appearance choice count changed");
                Require(!string.IsNullOrWhiteSpace(AppearanceCatalog.Label(field)), "missing appearance field label");
                for (int value = 0; value < counts[field]; value++)
                {
                    Require(!string.IsNullOrWhiteSpace(AppearanceCatalog.Name(field, value)), "missing appearance choice name");
                    int selection = AppearanceCatalog.With(code, field, value);
                    sim.Step(WorldSimulator.FixedStep);
                    sim.Apply(Command(player, "appearance", arg: selection));
                    Require(player.appearance == selection && AppearanceCatalog.Get(player.appearance, field) == value,
                        "valid catalog choice did not reach the player");
                    for (int previous = 0; previous < field; previous++)
                        Require(AppearanceCatalog.Get(player.appearance, previous) == choices[previous], "changing one field changed another");
                }
                code = AppearanceCatalog.With(code, field, choices[field]);
            }
            Require(code == 0x55d31d, "appearance persisted bit order changed");
            Require(AppearanceCatalog.Name(1, 0) == "短发" && AppearanceCatalog.Name(1, 1) == "侧分" &&
                AppearanceCatalog.Name(1, 2) == "刺发" && AppearanceCatalog.Name(1, 3) == "齐颈" &&
                AppearanceCatalog.Name(1, 4) == "马尾" && AppearanceCatalog.Name(1, 5) == "光头", "hair style order changed");
            Require(AppearanceCatalog.Name(3, 0) == "短袖" && AppearanceCatalog.Name(3, 1) == "夹克" &&
                AppearanceCatalog.Name(3, 2) == "连帽衫", "top style order changed");
        });
        Run("appearance current save roundtrip preserves both players and source ownership", () =>
        {
            var sim = new WorldSimulator(Flat(era: TechEra.Primitive));
            var first = sim.AddPlayer("appearance-save-a", "A");
            var second = sim.AddPlayer("appearance-save-b", "B");
            sim.Apply(Command(first, "appearance", arg: 0x55d31d));
            sim.Apply(Command(second, "appearance", arg: 1));
            var saved = Saved(sim.State);
            Require(saved.version == 14 && new WorldState().version == 14, "appearance requires the v14 save contract");
            string before = Dump(saved);
            var restored = new WorldSimulator(saved);
            Require(restored.State.players[0].appearance == 0x55d31d && restored.State.players[1].appearance == 1,
                "restore lost or mixed player appearances");
            Require(Dump(saved) == before, "restoring appearance mutated the caller's save");
            restored.Apply(Command(restored.State.players[0], "appearance", arg: 2));
            Require(first.appearance == 0x55d31d && saved.players[0].appearance == 0x55d31d && second.appearance == 1,
                "restored appearance aliases another player's or the saved state");
        });
        Run("appearance catalog guards invalid fields before shifting", () =>
        {
            const int code = 0x55d31d;
            foreach (int field in new[] { -1, 8, 16, int.MinValue, int.MaxValue })
            {
                Require(AppearanceCatalog.Count(field) == 0 && AppearanceCatalog.Get(code, field) == 0, "invalid field was readable");
                Require(AppearanceCatalog.With(code, field, 0) == code && AppearanceCatalog.With(code, field, 1) == code,
                    "invalid field shifted into a valid field");
                Require(AppearanceCatalog.Label(field) == "" && AppearanceCatalog.Name(field, 0) == "", "invalid field had a label or choice");
            }
            for (int field = 0; field < 8; field++)
            {
                Require(AppearanceCatalog.With(code, field, -1) == code &&
                    AppearanceCatalog.With(code, field, AppearanceCatalog.Count(field)) == code, "invalid choice changed the code");
                Require(AppearanceCatalog.Name(field, -1) == "" &&
                    AppearanceCatalog.Name(field, AppearanceCatalog.Count(field)) == "", "invalid choice had a name");
            }
            Require(AppearanceCatalog.Get(-1, 0) == 0 && AppearanceCatalog.With(-1, 0, 1) == 0, "invalid source code had no safe fallback");
        });
        Run("appearance malformed command codes are rejected atomically", () =>
        {
            var sim = new WorldSimulator(Flat(era: TechEra.Primitive));
            var player = sim.AddPlayer("appearance-invalid", "Invalid");
            sim.Apply(Command(player, "appearance", arg: 0x55d31d));
            foreach (int code in InvalidAppearanceCodes())
            {
                sim.Step(WorldSimulator.FixedStep);
                string before = Dump(sim.State);
                Require(!AppearanceCatalog.IsValid(code), "malformed appearance passed catalog validation: " + code);
                sim.Apply(Command(player, "appearance", arg: code));
                Require(Dump(sim.State) == before, "invalid appearance command mutated world state: " + code);
            }
        });
        Run("appearance v13 JSON without the field migrates to zero without mutating its source", () =>
        {
            var sim = new WorldSimulator(Flat(era: TechEra.Primitive));
            var player = sim.AddPlayer("appearance-legacy", "Legacy");
            sim.Apply(Command(player, "appearance", arg: 0x55d31d));
            var json = JsonNode.Parse(Dump(sim.State));
            json["version"] = 13;
            foreach (var savedPlayer in json["players"].AsArray()) savedPlayer.AsObject().Remove("appearance");
            var old = JsonSerializer.Deserialize<WorldState>(json.ToJsonString(), Json);
            string before = Dump(old);
            var restored = new WorldSimulator(old);
            Require(restored.State.version == 14 && restored.State.players[0].appearance == 0, "missing old appearance was not defaulted");
            Require(Dump(old) == before, "migration mutated the caller's old save");
            old.players[0].appearance = -1;
            Require(new WorldSimulator(old).State.players[0].appearance == 0 && old.players[0].appearance == -1,
                "v13 unexpectedly trusted or rewrote a field outside its schema");
        });
        Run("appearance current saves reject every malformed code without repairing the caller", () =>
        {
            var sim = new WorldSimulator(Flat(era: TechEra.Primitive));
            sim.AddPlayer("appearance-corrupt-save", "Corrupt");
            foreach (int code in InvalidAppearanceCodes())
            {
                var saved = Saved(sim.State);
                saved.players[0].appearance = code;
                string before = Dump(saved);
                Reject(saved, "malformed saved appearance accepted: " + code);
                Require(Dump(saved) == before, "save validation repaired the caller's graph");
            }
        });
        Run("appearance snapshots preserve every player without mutating authority", () =>
        {
            foreach (bool chunked in new[] { false, true })
            {
                var sim = chunked ? new WorldSimulator(new WorldGenerationSettings { seed = 14, chunked = true }) :
                    new WorldSimulator(Flat(era: TechEra.Primitive));
                var first = sim.AddPlayer("appearance-snapshot-a", "A");
                var second = sim.AddPlayer("appearance-snapshot-b", "B");
                sim.Apply(Command(first, "appearance", arg: 0x55d31d));
                sim.Apply(Command(second, "appearance", arg: 1));
                string before = Dump(sim.State);
                var snapshot = WorldSnapshot.ForPlayer(sim.State, first.id);
                string payload = Dump(snapshot);
                var received = Saved(snapshot);
                Require(Dump(WorldSnapshot.ForPlayer(sim.State, first.id)) == payload && Dump(sim.State) == before,
                    "snapshot changed between reads or mutated authority");
                var other = WorldSnapshot.ForPlayer(sim.State, second.id);
                Require(other.players[0].appearance == 0x55d31d && other.players[1].appearance == 1,
                    "a recipient's snapshot lost another player's appearance");
                sim.Apply(Command(first, "appearance", arg: 2));
                Require(received.players[0].appearance == 0x55d31d && received.players[1].appearance == 1,
                    "serialized snapshot aliased later authority changes");
                if (chunked) Require(snapshot.players[0].appearance == 0x55d31d, "projected scalar appearance changed after its copy");
                Require(WorldSnapshot.ForPlayer(sim.State, second.id).players[0].appearance == 2, "next snapshot missed the new appearance");
            }
        });
        Run("appearance disconnect reconnect death and respawn retain customization", () =>
        {
            var sim = new WorldSimulator(Flat(era: TechEra.Primitive));
            var player = sim.AddPlayer("appearance-lifecycle", "Lifecycle");
            var other = sim.AddPlayer("appearance-lifecycle-other", "Other");
            sim.Apply(Command(player, "appearance", arg: 0x55d31d));
            sim.Apply(Command(other, "appearance", arg: 1));
            sim.Apply(new PlayerCommand { playerId = "unknown", action = "appearance", arg = 2 });
            sim.SetConnected(player.id, false);
            sim.Apply(Command(player, "appearance", arg: 2));
            Require(player.appearance == 0x55d31d && other.appearance == 1, "unknown or disconnected command changed a player");
            Require(ReferenceEquals(sim.AddPlayer(player.id, "Rejoined"), player) && player.appearance == 0x55d31d,
                "reconnecting rebuilt or reset the player");
            player.hp = 0;
            player.lastDamageAt = sim.State.elapsed;
            sim.Apply(Command(player, "appearance", arg: 3));
            Require(player.appearance == 0x55d31d, "dead actor bypassed the command policy");
            Advance(sim, 5.1f);
            sim.Apply(Command(player, "respawn"));
            Require(player.hp > 0 && player.appearance == 0x55d31d && other.appearance == 1, "respawning lost or mixed appearances");
        });
        Run("appearance commands preserve the existing per tick work limit", () =>
        {
            var sim = new WorldSimulator(Flat(era: TechEra.Primitive));
            var player = sim.AddPlayer("appearance-rate", "Rate");
            for (int code = 0; code < 6; code++) sim.Apply(Command(player, "appearance", arg: code));
            sim.Apply(Command(player, "appearance", arg: 8));
            Require(player.appearance == 5, "appearance bypassed the six discrete commands per tick limit");
            sim.Step(WorldSimulator.FixedStep);
            sim.Apply(Command(player, "appearance", arg: 8));
            Require(player.appearance == 8, "next tick did not accept a valid appearance");
        });
        Run("appearance archive read write roundtrip rejects corrupt replacement", () =>
        {
            var sim = new WorldSimulator(Flat(era: TechEra.Primitive));
            var player = sim.AddPlayer("appearance-archive", "Archive");
            sim.Apply(Command(player, "appearance", arg: 0x55d31d));
            var archive = ArchiveFixture(out _);
            string id = Guid.NewGuid().ToString("N");
            string before = Dump(sim.State);
            archive.Write(id, sim.State);
            Require(Dump(sim.State) == before && archive.Read(id).players[0].appearance == 0x55d31d,
                "archive write mutated or dropped the appearance");
            player.appearance = 6;
            bool rejected = false;
            try { archive.Write(id, sim.State); } catch (ArgumentException) { rejected = true; }
            Require(rejected && archive.Read(id).players[0].appearance == 0x55d31d, "bad appearance overwrote the last valid archive");
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
