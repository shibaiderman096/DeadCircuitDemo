using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        const float AppearancePreviewScale = 192f;
        const float AppearanceConfirmationTimeout = 6f;
        static readonly int[] AppearancePreviewFacings = { 3, 1, 2, 0 };
        static readonly string[] AppearancePreviewDirections = { "正面", "背面", "左侧", "右侧" };

        RectTransform appearancePanel, appearancePreviewOrigin, appearanceFootRule;
        readonly SurvivorLayer[] appearanceLayers = new SurvivorLayer[SurvivorVisuals.Capacity];
        readonly UnityEngine.UI.Image[] appearanceImages = new UnityEngine.UI.Image[SurvivorVisuals.Capacity];
        readonly TextMeshProUGUI[] appearanceValues = new TextMeshProUGUI[AppearanceCatalog.FieldCount];
        readonly TextMeshProUGUI[] appearanceCounts = new TextMeshProUGUI[AppearanceCatalog.FieldCount];
        readonly UnityEngine.UI.Image[] appearanceSwatches = new UnityEngine.UI.Image[AppearanceCatalog.FieldCount];
        readonly UiButton[] appearancePrevious = new UiButton[AppearanceCatalog.FieldCount];
        readonly UiButton[] appearanceNext = new UiButton[AppearanceCatalog.FieldCount];
        readonly UiButton[] appearanceDirections = new UiButton[4];
        UiButton appearanceInventoryButton, appearanceBackButton, appearanceApplyButton;
        UiButton appearanceWalkingButton, appearanceEquipmentButton;
        TextMeshProUGUI appearancePreviewCaption, appearancePreviewEmpty, appearanceStatus;
        int appearanceDraftCode = AppearanceCatalog.DefaultCode, appearanceFacing = 3, appearanceSubmittedCode;
        bool appearanceWalking, appearanceEquipment = true, appearanceAwaitingConfirmation, appearanceConfirmationTimedOut;
        float appearancePhase, appearanceSubmittedAt;

        void MakeAppearanceButton(RectTransform inventoryCard)
        {
            if (inventoryCard == null || appearanceInventoryButton != null) return;
            appearanceInventoryButton = Button("Open Appearance", inventoryCard, "主角外观",
                826, 18, 176, 32, OpenAppearance);
            SetButtonArt(appearanceInventoryButton, "ui_equip", 20);
        }

        void MakeAppearance(RectTransform root)
        {
            if (root == null || appearancePanel != null) return;
            appearancePanel = Backdrop("Appearance Panel", root, new Color32(4, 12, 8, 225));
            RectTransform card = Box("Appearance Card", appearancePanel, 0, 0, 1120, 680, Panel, true);
            Center(card, 1120, 680);
            PanelRule(card, 1120, 66);
            Label("Appearance Heading", card, "主角外观", 22, 16, 760, 36, 25, Ink, FontStyles.Bold);
            appearanceBackButton = Button("Close Appearance", card, "返回背包", 948, 18, 150, 32, CloseAppearance);
            SetButtonArt(appearanceBackButton, "ui_back", 20);

            RectTransform preview = Box("Appearance Preview Surface", card, 22, 80, 350, 468, Recessed);
            Label("Appearance Preview Heading", preview, "角色预览 · 草稿", 16, 12, 318, 28, 18, Accent, FontStyles.Bold);
            appearancePreviewCaption = Label("Appearance Preview Caption", preview, "", 16, 42, 318, 24, 13, Muted);
            RectTransform stage = Box("Appearance Preview Stage", preview, 16, 74, 318, 284, Panel);
            stage.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            appearanceFootRule = Box("Appearance Foot Rule", stage, 48, 264, 222, 1, Edge);
            appearancePreviewEmpty = Label("Appearance Preview Empty", stage, "角色信息同步中",
                14, 108, 290, 68, 16, Muted);
            appearancePreviewEmpty.alignment = TextAlignmentOptions.Center;
            appearancePreviewOrigin = NewRect("Appearance Preview Origin", stage);
            appearancePreviewOrigin.anchorMin = appearancePreviewOrigin.anchorMax = new Vector2(.5f, 0f);
            appearancePreviewOrigin.pivot = new Vector2(.5f, 0f);
            appearancePreviewOrigin.anchoredPosition = new Vector2(0f, 20f);
            appearancePreviewOrigin.sizeDelta = Vector2.zero;
            for (int i = 0; i < appearanceImages.Length; i++)
            {
                RectTransform layer = NewRect("Appearance Layer " + i, appearancePreviewOrigin);
                layer.anchorMin = layer.anchorMax = Vector2.zero;
                var image = layer.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.type = UnityEngine.UI.Image.Type.Simple;
                image.useSpriteMesh = false;
                image.raycastTarget = false;
                image.enabled = false;
                appearanceImages[i] = image;
            }
            for (int i = 0; i < appearanceDirections.Length; i++)
            {
                int facing = AppearancePreviewFacings[i];
                appearanceDirections[i] = Button("Appearance Direction " + facing, preview, AppearancePreviewDirections[i],
                    16 + i * 80, 372, 70, 32, () => SetAppearanceFacing(facing));
            }
            appearanceWalkingButton = Button("Appearance Walking", preview, "", 16, 412, 152, 36, () =>
            {
                appearanceWalking = !appearanceWalking;
                if (!appearanceWalking) appearancePhase = 0f;
                RefreshAppearancePreviewControls();
                PaintAppearancePreview(bridge == null ? null : bridge.LocalPlayer);
            });
            appearanceEquipmentButton = Button("Appearance Equipment", preview, "", 180, 412, 154, 36, () =>
            {
                appearanceEquipment = !appearanceEquipment;
                RefreshAppearancePreviewControls();
                PaintAppearancePreview(bridge == null ? null : bridge.LocalPlayer);
            });

            RectTransform options = Box("Appearance Options Surface", card, 388, 80, 710, 468, Recessed);
            Label("Appearance Options Heading", options, "外观选项", 16, 12, 310, 28, 18, Accent, FontStyles.Bold);
            Label("Appearance Options Hint", options, "左右切换 · 即时预览", 340, 14, 354, 24, 14, Muted);
            for (int i = 0; i < AppearanceCatalog.FieldCount; i++)
            {
                int field = i;
                RectTransform row = Box("Appearance Field " + field, options, 16, 52 + i * 49, 678, 44, Panel);
                Label("Appearance Field Label " + field, row, AppearanceCatalog.Label(field),
                    10, 0, 120, 44, 16, Ink);
                appearancePrevious[i] = Button("Appearance Previous " + field, row, "←",
                    138, 4, 44, 36, () => CycleAppearance(field, -1));
                bool colorField = field == 0 || field == 2 || field == 4 || field == 5 || field == 6;
                if (colorField)
                    appearanceSwatches[i] = Box("Appearance Color Chip " + field, row, 204, 12, 20, 20, Ink)
                        .GetComponent<UnityEngine.UI.Image>();
                appearanceValues[i] = Label("Appearance Field Value " + field, row, "",
                    colorField ? 232 : 190, 0, colorField ? 310 : 352, 44, 17, Accent, FontStyles.Bold);
                appearanceValues[i].alignment = TextAlignmentOptions.Center;
                appearanceCounts[i] = Label("Appearance Field Count " + field, row, "",
                    544, 0, 64, 44, 13, Muted);
                appearanceCounts[i].alignment = TextAlignmentOptions.Center;
                appearanceNext[i] = Button("Appearance Next " + field, row, "→",
                    618, 4, 50, 36, () => CycleAppearance(field, 1));
            }

            Label("Appearance Notice", card,
                "调整仅影响草稿；应用后同步给其他玩家，并随角色存档。\n实际穿戴的护甲会覆盖对应部位；穿戴开关只影响预览，不会卸下装备。",
                22, 558, 1076, 44, 14, Muted);
            Box("Appearance Footer Rule", card, 22, 606, 1076, 1, Edge);
            UiButton reset = Button("Reset Appearance Draft", card, "重置默认草稿", 22, 622, 180, 38, () =>
            {
                appearanceDraftCode = AppearanceCatalog.DefaultCode;
                appearanceConfirmationTimedOut = false;
                RefreshAppearanceOptions();
                RefreshAppearance(bridge == null ? null : bridge.World, bridge == null ? null : bridge.LocalPlayer);
                PaintAppearancePreview(bridge == null ? null : bridge.LocalPlayer);
            });
            SetButtonArt(reset, "ui_refresh", 20);
            appearanceStatus = Label("Appearance Draft Status", card, "", 220, 618, 490, 44, 14, Amber);
            UiButton cancel = Button("Cancel Appearance", card, "取消并返回", 730, 622, 178, 38, CloseAppearance);
            SetButtonArt(cancel, "ui_back", 20);
            appearanceApplyButton = Button("Apply Appearance", card, "应用外观", 920, 622, 178, 38, ApplyAppearance, true);
            SetButtonArt(appearanceApplyButton, "ui_equip", 20);
            appearanceApplyButton.button.interactable = false;
            RefreshAppearanceOptions();
            RefreshAppearancePreviewControls();
            appearancePanel.gameObject.SetActive(false);
        }

        void OpenAppearance()
        {
            PlayerState player = bridge == null ? null : bridge.LocalPlayer;
            if (appearancePanel == null || bridge == null || !bridge.IsRunning || bridge.World == null ||
                player == null || !player.connected || player.hp <= 0f) return;

            // SetPanel closes all modals before reopening this one. Draft lifetime is explicit.
            appearanceDraftCode = AppearanceCatalog.IsValid(player.appearance) ? player.appearance : AppearanceCatalog.DefaultCode;
            appearanceFacing = 3;
            appearanceWalking = false;
            appearanceEquipment = true;
            appearancePhase = 0f;
            appearanceAwaitingConfirmation = appearanceConfirmationTimedOut = false;
            RefreshAppearanceOptions();
            RefreshAppearancePreviewControls();
            SetPanel(appearancePanel, true);
            if (!appearancePanel.gameObject.activeInHierarchy) return;
            appearancePanel.SetAsLastSibling();
            RefreshAppearance(bridge.World, bridge.LocalPlayer);
            PaintAppearancePreview(bridge.LocalPlayer);
            if (eventSystem != null) eventSystem.SetSelectedGameObject(appearanceBackButton.button.gameObject);
        }

        void CloseAppearance()
        {
            SetPanel(inventory, true);
            if (inventory.gameObject.activeInHierarchy && eventSystem != null && appearanceInventoryButton != null)
                eventSystem.SetSelectedGameObject(appearanceInventoryButton.button.gameObject);
        }

        void CycleAppearance(int field, int delta)
        {
            if (appearancePanel == null || !appearancePanel.gameObject.activeInHierarchy ||
                field < 0 || field >= AppearanceCatalog.FieldCount || !AppearanceCatalog.IsValid(appearanceDraftCode)) return;
            int count = AppearanceCatalog.Count(field);
            if (count <= 0) return;
            int current = AppearanceCatalog.Get(appearanceDraftCode, field);
            appearanceDraftCode = AppearanceCatalog.With(appearanceDraftCode, field, (current + delta + count) % count);
            appearanceConfirmationTimedOut = false;
            RefreshAppearanceOptions();
            RefreshAppearance(bridge == null ? null : bridge.World, bridge == null ? null : bridge.LocalPlayer);
            PaintAppearancePreview(bridge == null ? null : bridge.LocalPlayer);
        }

        void RefreshAppearanceOptions()
        {
            bool valid = AppearanceCatalog.IsValid(appearanceDraftCode);
            for (int i = 0; i < appearanceValues.Length; i++)
            {
                int count = AppearanceCatalog.Count(i);
                int value = valid ? AppearanceCatalog.Get(appearanceDraftCode, i) : 0;
                Set(appearanceValues[i], valid && count > 0 ? AppearanceCatalog.Name(i, value) : "无效选项");
                Set(appearanceCounts[i], valid && count > 0 ? (value + 1) + " / " + count : "—");
                if (appearanceSwatches[i] != null)
                    appearanceSwatches[i].color = valid && count > 0 ? SurvivorVisuals.ColorFor(i, value) : Edge;
                appearancePrevious[i].button.interactable = appearanceNext[i].button.interactable = valid && count > 1;
            }
        }

        void SetAppearanceFacing(int facing)
        {
            appearanceFacing = facing;
            RefreshAppearancePreviewControls();
            PaintAppearancePreview(bridge == null ? null : bridge.LocalPlayer);
        }

        void RefreshAppearancePreviewControls()
        {
            string direction = "正面";
            for (int i = 0; i < appearanceDirections.Length; i++)
            {
                bool selected = AppearancePreviewFacings[i] == appearanceFacing;
                Tint(appearanceDirections[i], selected ? Selected : Raised);
                if (selected) direction = AppearancePreviewDirections[i];
            }
            Set(appearanceWalkingButton.label, appearanceWalking ? "行走预览：开" : "行走预览：关");
            Set(appearanceEquipmentButton.label, appearanceEquipment ? "显示穿戴：开" : "显示穿戴：关");
            Tint(appearanceWalkingButton, appearanceWalking ? Selected : Raised);
            Tint(appearanceEquipmentButton, appearanceEquipment ? Selected : Raised);
            Set(appearancePreviewCaption, direction + (appearanceWalking ? " · 行走" : " · 静止") +
                (appearanceEquipment ? " · 当前穿戴" : " · 基础外观"));
        }

        bool CanApplyAppearance(PlayerState player, WorldState world)
        {
            return bridge != null && bridge.IsRunning && world != null && player != null && player.connected &&
                player.hp > 0f && AppearanceCatalog.IsValid(appearanceDraftCode);
        }

        void ApplyAppearance()
        {
            PlayerState player = bridge == null ? null : bridge.LocalPlayer;
            if (appearancePanel == null || !appearancePanel.gameObject.activeInHierarchy ||
                !CanApplyAppearance(player, bridge == null ? null : bridge.World) || appearanceAwaitingConfirmation) return;
            appearanceSubmittedCode = appearanceDraftCode;
            appearanceSubmittedAt = Time.unscaledTime;
            appearanceAwaitingConfirmation = true;
            appearanceConfirmationTimedOut = false;
            Send("appearance", appearanceDraftCode);
            RefreshAppearance(bridge.World, bridge.LocalPlayer);
        }

        void RefreshAppearance(WorldState world, PlayerState player)
        {
            if (appearancePanel == null || !appearancePanel.gameObject.activeInHierarchy) return;
            bool available = CanApplyAppearance(player, world);
            if (appearanceAwaitingConfirmation && available && player.appearance == appearanceSubmittedCode)
                appearanceAwaitingConfirmation = false;
            else if (appearanceAwaitingConfirmation && Time.unscaledTime - appearanceSubmittedAt >= AppearanceConfirmationTimeout)
            {
                appearanceAwaitingConfirmation = false;
                appearanceConfirmationTimedOut = true;
            }

            appearanceApplyButton.button.interactable = available && !appearanceAwaitingConfirmation && player.appearance != appearanceDraftCode;
            Set(appearanceApplyButton.label, appearanceAwaitingConfirmation ? "等待确认…" : "应用外观");
            string status;
            if (bridge == null || !bridge.IsRunning || world == null || player == null) status = "等待角色与世界同步";
            else if (!player.connected) status = "连接已断开，暂时无法应用";
            else if (player.hp <= 0f) status = "角色倒下，暂时无法应用";
            else if (!AppearanceCatalog.IsValid(appearanceDraftCode)) status = "草稿无效，请重置默认外观";
            else if (appearanceAwaitingConfirmation) status = "已发送请求 · 等待主机确认";
            else if (player.appearance == appearanceDraftCode) status = "与当前角色外观一致";
            else if (appearanceConfirmationTimedOut) status = "主机尚未确认，可重试应用";
            else status = "尚未应用的草稿 · 返回会放弃修改";
            Set(appearanceStatus, status);
            appearanceStatus.color = available && !appearanceAwaitingConfirmation && player.appearance == appearanceDraftCode ? Accent : Amber;
        }

        void TickAppearancePreview()
        {
            if (appearancePanel == null || !appearancePanel.gameObject.activeInHierarchy) return;
            if (appearanceWalking)
                appearancePhase = Mathf.Repeat(appearancePhase + Time.unscaledDeltaTime * 7f, Mathf.PI * 2f);
            PaintAppearancePreview(bridge == null ? null : bridge.LocalPlayer);
        }

        void PaintAppearancePreview(PlayerState player)
        {
            if (appearancePreviewOrigin == null) return;
            int count = 0;
            if (player != null && AppearanceCatalog.IsValid(appearanceDraftCode))
                count = Mathf.Clamp(SurvivorVisuals.Sample(player, appearanceDraftCode, appearanceFacing, appearancePhase,
                    appearanceWalking, appearanceEquipment, appearanceLayers, true), 0, appearanceImages.Length);
            float previewScale = FitAppearancePreview(count);
            bool visible = false;
            for (int i = 0; i < appearanceImages.Length; i++)
            {
                UnityEngine.UI.Image image = appearanceImages[i];
                if (i >= count)
                {
                    image.enabled = false;
                    continue;
                }
                SurvivorLayer layer = appearanceLayers[i];
                Sprite sprite = layer.sprite;
                if (sprite == null || sprite.pixelsPerUnit <= 0f || sprite.rect.width <= 0f || sprite.rect.height <= 0f)
                {
                    image.enabled = false;
                    continue;
                }
                image.sprite = sprite;
                image.color = layer.color;
                RectTransform rect = image.rectTransform;
                Vector2 size = sprite.rect.size;
                rect.pivot = new Vector2(sprite.pivot.x / size.x, sprite.pivot.y / size.y);
                rect.sizeDelta = size * (previewScale * layer.scale / sprite.pixelsPerUnit);
                rect.anchoredPosition = layer.position * previewScale;
                rect.localRotation = Quaternion.Euler(0f, 0f, layer.angle);
                rect.localScale = new Vector3(1f, layer.flipY ? -1f : 1f, 1f);
                image.enabled = true;
                visible = true;
            }
            appearancePreviewEmpty.gameObject.SetActive(!visible);
        }

        float FitAppearancePreview(int count)
        {
            var stage = appearancePreviewOrigin.parent as RectTransform;
            if (stage == null || count == 0) return AppearancePreviewScale;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < count; i++)
            {
                SurvivorLayer layer = appearanceLayers[i];
                Sprite sprite = layer.sprite;
                if (sprite == null || sprite.pixelsPerUnit <= 0f) continue;
                Quaternion rotation = Quaternion.Euler(0f, 0f, layer.angle);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector2 point = new Vector2((corner & 1) * sprite.rect.width - sprite.pivot.x,
                        ((corner >> 1) & 1) * sprite.rect.height - sprite.pivot.y) * (layer.scale / sprite.pixelsPerUnit);
                    if (layer.flipY) point.y = -point.y;
                    point = layer.position + (Vector2)(rotation * point);
                    minimum = Vector2.Min(minimum, point);
                    maximum = Vector2.Max(maximum, point);
                }
            }
            if (float.IsInfinity(minimum.x)) return AppearancePreviewScale;
            const float padding = 12f;
            Vector2 available = stage.rect.size - Vector2.one * (padding * 2f);
            Vector2 size = maximum - minimum;
            float scale = Mathf.Min(AppearancePreviewScale, available.x / Mathf.Max(.01f, size.x),
                available.y / Mathf.Max(.01f, size.y));
            appearancePreviewOrigin.anchoredPosition = new Vector2(
                Mathf.Clamp(0f, -stage.rect.width * .5f + padding - minimum.x * scale, stage.rect.width * .5f - padding - maximum.x * scale),
                Mathf.Clamp(20f, padding - minimum.y * scale, stage.rect.height - padding - maximum.y * scale));
            appearanceFootRule.anchoredPosition = new Vector2(48f, appearancePreviewOrigin.anchoredPosition.y - stage.rect.height);
            return scale;
        }
    }
}
