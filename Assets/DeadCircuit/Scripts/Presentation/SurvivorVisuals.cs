using System;
using UnityEngine;

namespace DeadCircuit
{
    public struct SurvivorLayer
    {
        public Sprite sprite;
        public Color color;
        public Vector2 position;
        public float angle;
        public int order;
        public float scale;
        public bool flipY;
    }

    /// <summary>One foot-registered, six-part rig shared by the world and dressing room.</summary>
    public static class SurvivorVisuals
    {
        public const int Capacity = 40;
        const float Pixel = 1f / 32f;
        static readonly Sprite[] heads = new Sprite[4], arms = new Sprite[4], legs = new Sprite[4], shoes = new Sprite[4];
        static readonly Sprite[,] faces = new Sprite[3,4], hair = new Sprite[5,4], tops = new Sprite[3,4], sleeves = new Sprite[3,4];
        static readonly Sprite[,,] armor = new Sprite[19,5,4];
        static readonly Sprite[] held = new Sprite[10], parts = new Sprite[7];
        static bool loaded, available;

        static readonly Color32[] Skin = {
            new Color32(244,219,192,255), new Color32(235,195,156,255), new Color32(205,155,112,255),
            new Color32(174,119,81,255), new Color32(127,80,59,255), new Color32(83,53,43,255) };
        static readonly Color32[] Hair = {
            new Color32(61,48,40,255), new Color32(119,77,46,255), new Color32(140,78,45,255), new Color32(201,161,84,255),
            new Color32(190,196,186,255), new Color32(165,69,45,255), new Color32(72,88,125,255), new Color32(70,121,108,255) };
        static readonly Color32[] Fabric = {
            new Color32(200,205,194,255), new Color32(77,81,79,255), new Color32(141,70,51,255), new Color32(203,150,72,255),
            new Color32(133,145,88,255), new Color32(76,126,117,255), new Color32(59,80,111,255), new Color32(130,90,125,255) };
        static readonly Color32[] Pants = {
            new Color32(54,62,59,255), new Color32(83,94,92,255), new Color32(89,112,133,255), new Color32(51,73,105,255),
            new Color32(168,149,100,255), new Color32(97,115,76,255), new Color32(112,80,55,255), new Color32(173,178,166,255) };
        static readonly Color32[] Shoes = {
            new Color32(54,62,59,255), new Color32(205,211,201,255), new Color32(100,74,51,255),
            new Color32(90,99,98,255), new Color32(55,78,106,255), new Color32(129,69,48,255) };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { loaded = available = false; }

        public static Color ColorFor(int field, int choice)
        {
            var colors = field == 0 ? Skin : field == 2 ? Hair : field == 5 ? Pants : field == 6 ? Shoes : Fabric;
            return colors[Mathf.Clamp(choice, 0, colors.Length - 1)];
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            available = true;
            for (int i = 0; i < held.Length; i++)
                held[i] = i < 5 ? DemoArt.Get("gear_" + i) : DemoArt.Get("weapon_" + i);
            for (int i = 0; i < parts.Length; i++) parts[i] = DemoArt.Get("part_" + i);
            for (int f = 0; f < 4; f++)
            {
                heads[f] = DemoArt.Get("avatar_head_" + f);
                arms[f] = DemoArt.Get("avatar_arm_" + f);
                legs[f] = DemoArt.Get("avatar_leg_" + f);
                shoes[f] = DemoArt.Get("avatar_shoe_" + f);
                available &= heads[f] != null && arms[f] != null && legs[f] != null && shoes[f] != null;
                for (int s = 0; s < 3; s++)
                {
                    faces[s,f] = DemoArt.Get("avatar_face_" + s + "_" + f);
                    tops[s,f] = DemoArt.Get("avatar_torso_" + s + "_" + f);
                    sleeves[s,f] = DemoArt.Get("avatar_sleeve_" + s + "_" + f);
                    available &= tops[s,f] != null && sleeves[s,f] != null;
                }
                for (int s = 0; s < 5; s++) hair[s,f] = DemoArt.Get("avatar_hair_" + s + "_" + f);
                for (int g = 10; g <= 18; g++)
                {
                    armor[g,0,f] = DemoArt.Get("avatar_armor_" + g + "_head_" + f);
                    armor[g,1,f] = DemoArt.Get("avatar_armor_" + g + "_torso_" + f);
                    armor[g,2,f] = DemoArt.Get("avatar_armor_" + g + "_arm_" + f);
                    armor[g,3,f] = DemoArt.Get("avatar_armor_" + g + "_leg_" + f);
                    armor[g,4,f] = DemoArt.Get("avatar_armor_" + g + "_shoe_" + f);
                }
            }
        }

        struct Pose
        {
            public Vector2 head, hip, leftArm, rightArm;
            public float leftLegAngle, rightLegAngle, leftArmAngle, rightArmAngle;
            public bool side, rightNear;
            public Vector2 aim;
        }

        static float PointAt(Vector2 shoulder, Vector2 target)
        {
            Vector2 d = target - shoulder;
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 90f;
        }

        static Pose BodyPose(PlayerState player, int facing, float phase, bool walking, bool preview = false)
        {
            bool side = facing == 0 || facing == 2;
            float step = walking ? Mathf.Sin(phase) : 0;
            float stride = step * (side ? 23f : 9f);
            float hip = 12 * Mathf.Cos(stride * Mathf.Deg2Rad) * Pixel;
            var pose = new Pose {
                side = side, rightNear = facing != 2, hip = new Vector2(0, hip), head = new Vector2(0, hip + 12 * Pixel),
                leftArm = new Vector2((side ? -1 : -7) * Pixel, hip + 11 * Pixel),
                rightArm = new Vector2((side ? 1 : 7) * Pixel, hip + 11 * Pixel),
                leftLegAngle = stride, rightLegAngle = -stride,
                leftArmAngle = -step * (side ? 27f : 8f), rightArmAngle = step * (side ? 27f : 8f)
            };
            if (player != null && player.hp > 0 && EquipmentCatalog.Equipped(player) != null)
            {
                Vector2 aim = new Vector2(player.aimX, player.aimY).normalized;
                if (aim.sqrMagnitude < .001f) aim = Vector2.right;
                if (preview) aim = facing == 0 ? Vector2.right : facing == 1 ? Vector2.up : facing == 2 ? Vector2.left : Vector2.down;
                pose.aim = aim;
                Vector2 primary = aim * .25f + Vector2.up * (hip + .16f);
                Vector2 support = aim * .40f + Vector2.up * (hip + .16f);
                pose.leftArmAngle = PointAt(pose.leftArm, pose.rightNear ? support : primary);
                pose.rightArmAngle = PointAt(pose.rightArm, pose.rightNear ? primary : support);
            }
            return pose;
        }

        public static Vector2 WeaponGrip(PlayerState player, int facing, float phase, bool walking)
        {
            var pose = BodyPose(player, facing, phase, walking);
            Vector2 shoulder = pose.rightNear ? pose.rightArm : pose.leftArm;
            float angle = pose.rightNear ? pose.rightArmAngle : pose.leftArmAngle;
            return shoulder + (Vector2)(Quaternion.Euler(0,0,angle) * Vector3.down) * (10.5f * Pixel);
        }

        static GearState Worn(PlayerState player, int slot, bool equipment)
        {
            if (!equipment || player == null || player.armorIds == null || slot >= player.armorIds.Length) return null;
            var gear = EquipmentCatalog.Find(player, player.armorIds[slot]);
            return gear != null && EquipmentCatalog.ArmorSlot(gear.kind) == slot ? gear : null;
        }

        static void Add(SurvivorLayer[] layers, ref int count, Sprite sprite, Color color, Vector2 at, float angle, int order)
        {
            if (sprite == null) return;
            layers[count++] = new SurvivorLayer { sprite = sprite, color = color, position = at, angle = angle, order = order, scale = 1 };
        }

        static Vector2 SpritePoint(Sprite sprite, Vector2 source, float width, float height, bool flipY)
        {
            Vector2 point = new Vector2(source.x * sprite.rect.width / width - sprite.pivot.x,
                sprite.rect.height - source.y * sprite.rect.height / height - sprite.pivot.y) / sprite.pixelsPerUnit;
            if (flipY) point.y = -point.y;
            return point;
        }

        static void PreviewWeapon(PlayerState player, int facing, Pose pose, SurvivorLayer[] layers, ref int count)
        {
            var gear = player == null || player.hp <= 0 ? null : EquipmentCatalog.Equipped(player);
            if (gear == null || (int)gear.kind < 0 || (int)gear.kind >= held.Length) return;
            Sprite sprite = held[(int)gear.kind];
            if (sprite == null) return;
            bool flipY = pose.aim.x < 0;
            float scale = (int)gear.kind < 5 ? .54f : gear.kind == GearKind.Knife ? .42f : gear.kind == GearKind.Pistol ? .52f : .54f;
            float angle = Mathf.Atan2(pose.aim.y, pose.aim.x) * Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(0,0,angle);
            Vector2 hand = (pose.rightNear ? pose.rightArm : pose.leftArm) +
                (Vector2)(Quaternion.Euler(0,0,pose.rightNear ? pose.rightArmAngle : pose.leftArmAngle) * Vector3.down) * (10.5f * Pixel);
            Vector2 position = hand - (Vector2)(rotation * SpritePoint(sprite,new Vector2(20,20),64,32,flipY)) * scale;
            int order = facing == 1 ? -7 : 3;
            Add(layers,ref count,sprite,gear.durability > 0 ? Color.white : new Color(.45f,.43f,.4f),position,angle,order);
            layers[count - 1].scale = scale; layers[count - 1].flipY = flipY;
            if (!EquipmentCatalog.IsGun(gear.kind) || gear.attachments == null) return;
            for (int slot = 0; slot < 2 && slot < gear.attachments.Length; slot++)
            {
                int part = gear.attachments[slot];
                if (part < 0 || part >= parts.Length || parts[part] == null || (slot == 0 && gear.kind == GearKind.PrecisionRifle)) continue;
                Vector2 mount = slot == 0 ? (gear.kind == GearKind.Pistol ? new Vector2(29,9) : gear.kind == GearKind.Shotgun ? new Vector2(28,9) : new Vector2(29,8)) :
                    gear.kind == GearKind.Pistol ? new Vector2(45,14) : gear.kind == GearKind.Shotgun ? new Vector2(61,14) : gear.kind == GearKind.AssaultRifle ? new Vector2(60,14) : new Vector2(62,14);
                Vector2 socket = position + (Vector2)(rotation * SpritePoint(sprite,mount,64,32,flipY)) * scale;
                float partScale = scale * (slot == 0 ? .36f : .42f);
                Vector2 anchor = slot == 0 ? new Vector2(16,26) : new Vector2(3,16);
                Vector2 at = socket - (Vector2)(rotation * SpritePoint(parts[part],anchor,32,32,flipY)) * partScale;
                Add(layers,ref count,parts[part],Color.white,at,angle,order+1);
                layers[count - 1].scale = partScale; layers[count - 1].flipY = flipY;
            }
        }

        static void AddArmor(SurvivorLayer[] layers, ref int count, GearState gear, int part, int facing, Vector2 at, float angle, int order)
        {
            if (gear == null || (int)gear.kind < 10 || (int)gear.kind > 18) return;
            Color tint = gear.durability > 0 ? Color.white : new Color(.48f,.45f,.42f);
            Add(layers, ref count, armor[(int)gear.kind,part,facing], tint, at, angle, order);
        }

        /// <summary>Returns layers in draw order. Coordinates and all pivots share the ground origin.</summary>
        public static int Sample(PlayerState player, int appearance, int facing, float phase, bool walking, bool equipment, SurvivorLayer[] layers, bool previewWeapon = false)
        {
            if (layers == null || layers.Length < Capacity) throw new ArgumentException("A complete survivor layer buffer is required.", nameof(layers));
            Load();
            if (!available) return 0;
            int code = AppearanceCatalog.IsValid(appearance) ? appearance : AppearanceCatalog.DefaultCode;
            facing = Mathf.Clamp(facing, 0, 3);
            int top = AppearanceCatalog.Get(code, 3), style = AppearanceCatalog.Get(code, 1);
            Color skin = ColorFor(0, AppearanceCatalog.Get(code, 0));
            Color fabric = ColorFor(4, AppearanceCatalog.Get(code, 4));
            Color pants = ColorFor(5, AppearanceCatalog.Get(code, 5));
            Color shoe = ColorFor(6, AppearanceCatalog.Get(code, 6));
            var pose = BodyPose(equipment ? player : null, facing, phase, walking, previewWeapon);
            var headGear = Worn(player, 0, equipment);
            var torsoGear = Worn(player, 1, equipment);
            var legGear = Worn(player, 2, equipment);
            var footGear = Worn(player, 3, equipment);
            int count = 0;
            for (int limb = 0; limb < 2; limb++)
            {
                bool right = limb == 1, near = right == pose.rightNear;
                Vector2 shoulder = right ? pose.rightArm : pose.leftArm;
                float armAngle = right ? pose.rightArmAngle : pose.leftArmAngle;
                int order = near ? 4 : -6;
                Add(layers, ref count, arms[facing], skin, shoulder, armAngle, order);
                Add(layers, ref count, sleeves[top,facing], fabric, shoulder, armAngle, order + 1);
                AddArmor(layers, ref count, torsoGear, 2, facing, shoulder, armAngle, order + 2);
                float legAngle = right ? pose.rightLegAngle : pose.leftLegAngle;
                Vector2 hip = pose.hip + Vector2.right * ((pose.side ? (right ? 1 : -1) : (right ? 3 : -3)) * Pixel);
                Vector2 ankle = hip + (Vector2)(Quaternion.Euler(0,0,legAngle) * Vector3.down) * (8 * Pixel);
                Add(layers, ref count, legs[facing], pants, hip, legAngle, -3);
                AddArmor(layers, ref count, legGear, 3, facing, hip, legAngle, -2);
                Add(layers, ref count, shoes[facing], shoe, ankle, legAngle, -1);
                AddArmor(layers, ref count, footGear, 4, facing, ankle, legAngle, 0);
            }
            Add(layers, ref count, tops[top,facing], fabric, pose.head, 0, 1);
            AddArmor(layers, ref count, torsoGear, 1, facing, pose.head, 0, 2);
            Add(layers, ref count, heads[facing], skin, pose.head, 0, 7);
            Add(layers, ref count, faces[AppearanceCatalog.Get(code,7),facing], Color.white, pose.head, 0, 8);
            if (headGear == null && style < 5)
                Add(layers, ref count, hair[style,facing], ColorFor(2,AppearanceCatalog.Get(code,2)), pose.head, 0, 9);
            AddArmor(layers, ref count, headGear, 0, facing, pose.head, 0, 10);
            if (equipment && previewWeapon) PreviewWeapon(player,facing,pose,layers,ref count);
            for (int i = 1; i < count; i++)
            {
                var item = layers[i]; int j = i - 1;
                while (j >= 0 && layers[j].order > item.order) { layers[j + 1] = layers[j]; j--; }
                layers[j + 1] = item;
            }
            return count;
        }
    }
}
