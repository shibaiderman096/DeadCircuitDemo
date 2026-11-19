using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoWorldView
    {
        readonly LineRenderer[] weaponReticle = new LineRenderer[4];

        // The reticle shows the same radian spread and recoil as the authority.
        // It is presentation only; hit detection always remains in WorldSimulator.
        void DrawWeaponReticle(PlayerState player)
        {
            var gear = player == null ? null : EquipmentCatalog.Equipped(player);
            bool visible = player != null && player.hp > 0 && gear != null &&
                !EquipmentCatalog.Gear(gear.kind).tool && gear.kind != GearKind.Knife &&
                !bridge.BuildSelection.HasValue && !bridge.WorldInputBlocked;
            for (int i = 0; i < weaponReticle.Length; i++)
            {
                if (weaponReticle[i] == null && visible)
                    weaponReticle[i] = NewLine("Weapon spread reticle " + i, 10010, .026f, false);
                if (weaponReticle[i] != null) weaponReticle[i].enabled = visible;
            }
            if (!visible) return;
            Vector2 cursor = bridge.MouseWorld;
            float distance = Vector2.Distance(new Vector2(player.x, player.y), cursor);
            var stats = EquipmentCatalog.Stats(gear);
            float radians = EquipmentCatalog.EffectiveSpread(player, gear);
            float radius = Mathf.Max(.09f, Mathf.Tan(radians) * Mathf.Min(distance, stats.range + player.attributes[3] * .35f));
            Color color = player.reloadRemaining > 0 || gear.magazine == 0 || distance > stats.range + player.attributes[3] * .35f ?
                new Color(1f, .65f, .35f, .85f) : player.aiming ?
                new Color(.48f, 1f, .78f, .9f) : new Color(.9f, .95f, .9f, .75f);
            for (int i = 0; i < weaponReticle.Length; i++)
            {
                Vector2 direction = i == 0 ? Vector2.right : i == 1 ? Vector2.left : i == 2 ? Vector2.up : Vector2.down;
                var line = weaponReticle[i];
                line.startColor = line.endColor = color;
                line.positionCount = 2;
                line.SetPosition(0, cursor + direction * radius);
                line.SetPosition(1, cursor + direction * (radius + .15f));
            }
        }
    }
}
