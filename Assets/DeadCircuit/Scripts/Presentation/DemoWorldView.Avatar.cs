using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoWorldView
    {
        sealed class SurvivorRig
        {
            public Transform root;
            public readonly SpriteRenderer[] renderers = new SpriteRenderer[SurvivorVisuals.Capacity];
            public readonly SurvivorLayer[] layers = new SurvivorLayer[SurvivorVisuals.Capacity];
            public Vector2 grip;
            public int facing;
        }

        bool DrawPlayerAvatar(Visual visual, PlayerState player, int facing, bool walking)
        {
            if (visual.avatar == null)
            {
                var rig = new SurvivorRig(); visual.avatar = rig;
                rig.root = new GameObject("Survivor six-part body").transform;
                rig.root.SetParent(visual.root.transform, false);
                for (int i = 0; i < rig.renderers.Length; i++)
                {
                    rig.renderers[i] = NewSprite("Skin and wearable layer " + i, rig.root, 0);
                    rig.renderers[i].enabled = false;
                }
            }
            var avatar = visual.avatar;
            int count = SurvivorVisuals.Sample(player, player.appearance, facing, visual.walk, walking, true, avatar.layers);
            avatar.root.gameObject.SetActive(count > 0);
            if (count == 0) return false;
            avatar.facing = facing;
            avatar.grip = SurvivorVisuals.WeaponGrip(player, facing, visual.walk, walking);
            avatar.root.localRotation = Quaternion.Euler(0, 0, player.hp <= 0 ? -80 : 0);
            visual.body.enabled = false;
            Color damage = DamageColor(visual, player.hp > 0 ? Color.white : new Color(.48f,.53f,.50f,.65f));
            for (int i = 0; i < avatar.renderers.Length; i++)
            {
                var renderer = avatar.renderers[i];
                renderer.enabled = i < count;
                if (i >= count) continue;
                var layer = avatar.layers[i];
                renderer.sprite = layer.sprite;
                renderer.color = layer.color * damage;
                renderer.sortingOrder = visual.body.sortingOrder + layer.order;
                renderer.transform.localPosition = layer.position;
                renderer.transform.localRotation = Quaternion.Euler(0, 0, layer.angle);
                renderer.transform.localScale = Vector3.one * layer.scale;
                renderer.flipX = false; renderer.flipY = layer.flipY;
            }
            return true;
        }
    }
}
