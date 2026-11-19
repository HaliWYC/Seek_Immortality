using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Sprites;

public sealed partial class SparHud
{
    sealed class HeadBar
    {
        public RectTransform root;
        public Image health;
        public Text caption;
        public StatusBadge[] badges;
        public Vector2 head;
    }

    sealed class InitiativePortrait
    {
        public RectTransform token;
        public Image frame, wire, point;
        public RawImage portrait;
        public Text name, reaction, progress;
        public Sprite sprite;
        public int index;
        public float fraction, railX;
        public int displayedTenths = -1;
    }

    struct PortraitGroup
    {
        public int Count;
        public float Position;
    }

    const float InitiativeHeaderHeight = 35f;
    const float InitiativePanelHeight = 116f;
    const float InitiativeWidth = 842f;
    const float RailY = 48f;
    const float RailInset = 64f;
    const float PortraitY = 28f;
    const float PortraitSize = 32f;
    const float PortraitSpacing = 114f;
    static readonly Color Jade = new Color32(101, 203, 161, 255);
    static readonly Color Vermilion = new Color32(235, 141, 114, 255);
    static readonly Color Gold = new Color32(255, 210, 119, 255);
    static readonly Color RailInk = new Color32(168, 147, 101, 255);
    static readonly Color SecondaryInk = new Color32(201, 222, 209, 255);
    static readonly Color OutlineInk = new Color32(17, 29, 26, 245);
    static readonly Color CreamInk = new Color32(255, 245, 218, 255);
    readonly List<HeadBar> heads = new List<HeadBar>();
    readonly List<HeadBar> visibleHeads = new List<HeadBar>();
    readonly List<InitiativePortrait> initiativePortraits = new List<InitiativePortrait>();
    readonly List<InitiativePortrait> livingPortraits = new List<InitiativePortrait>();
    readonly List<PortraitGroup> portraitGroups = new List<PortraitGroup>();
    RectTransform headLayer, initiativeQueue;
    Text timeline;
    Camera battleCamera;
    int displayedTenth = -1;
    string phaseStage;

    void CreateHeadLayer()
    {
        headLayer = new GameObject("棋子状态", typeof(RectTransform)).GetComponent<RectTransform>();
        headLayer.SetParent(topBar.parent, false);
        headLayer.SetAsFirstSibling();
        headLayer.anchorMin = Vector2.zero;
        headLayer.anchorMax = Vector2.one;
        headLayer.offsetMin = headLayer.offsetMax = Vector2.zero;
    }

    void CreateInitiativePanel()
    {
        if (topBar.TryGetComponent<Image>(out var background)) background.enabled = false;
        var oldAccent = topBar.Find("地名边线");
        if (oldAccent) oldAccent.gameObject.SetActive(false);
        TopRow(place.rectTransform, 5, 530, 0, 31);
        place.fontSize = 18;
        place.color = CreamInk;
        OutlineInitiativeLabel(place);
        phase.fontSize = 17;
        phase.color = SecondaryInk;
        OutlineInitiativeLabel(phase);

        initiativeQueue = new GameObject("行动时间轴", typeof(RectTransform)).GetComponent<RectTransform>();
        initiativeQueue.SetParent(topBar, false);
        initiativeQueue.anchorMin = initiativeQueue.anchorMax = new Vector2(.5f, 1);
        initiativeQueue.pivot = new Vector2(.5f, 1);
        initiativeQueue.sizeDelta = new Vector2(InitiativeWidth, 69f);
        initiativeQueue.anchoredPosition = new Vector2(0, -30f);

        var railShadow = Panel(initiativeQueue, "刻度尺描边", OutlineInk).rectTransform;
        TopRow(railShadow, RailInset - 1f, RailInset - 1f, RailY - 2f, RailY + 2f);
        var rail = Panel(initiativeQueue, "刻度尺", new Color32(245, 220, 162, 255)).rectTransform;
        TopRow(rail, RailInset, RailInset, RailY - 1, RailY + 1);
        for (int value = 0; value <= 100; value += 25)
        {
            var anchor = new Vector2(value / 100f, .5f);
            var mark = Panel(rail, "刻度 " + value, CreamInk).rectTransform;
            mark.anchorMin = mark.anchorMax = anchor;
            mark.sizeDelta = new Vector2(2, 8);
            mark.anchoredPosition = Vector2.zero;
            var label = MakeLabel(rail, "刻度数值", 13, TextAnchor.MiddleCenter);
            PlaceTimelineElement(label.rectTransform, anchor, 0, 12, 40, 18);
            label.text = value.ToString();
            label.color = CreamInk;
            OutlineInitiativeLabel(label);
        }

        timeline = MakeLabel(topBar, "斗法行动顺序", 15, TextAnchor.MiddleCenter);
        PlaceTimelineElement(timeline.rectTransform, new Vector2(.5f, 1), 0, 107, 1080, 18);
        timeline.color = SecondaryInk;
        timeline.resizeTextForBestFit = true;
        timeline.resizeTextMinSize = 12;
        timeline.resizeTextMaxSize = 15;
        timeline.verticalOverflow = VerticalWrapMode.Truncate;
        OutlineInitiativeLabel(timeline);
    }

    static void OutlineInitiativeLabel(Text label)
    {
        if (!label.TryGetComponent<Outline>(out var outline)) outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = OutlineInk;
        outline.effectDistance = new Vector2(1.15f, -1.15f);
    }

    static void PlaceTopControl(Button button, float right, float width)
    {
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-right, 0);
        rect.sizeDelta = new Vector2(width, 30f);
        FitLabel(button);
        var label = button.GetComponentInChildren<Text>(true);
        label.fontSize = 15;
        label.color = CreamInk;
        button.targetGraphic.color = new Color32(29, 47, 42, 232);
    }

    void LayoutTopControls(SparMatch match)
    {
        bool canRetry = match.Party == FieldParty.Practice && !controller.ConfirmingYield;
        float right = canRetry ? 257f : 132f;
        phase.rectTransform.anchorMin = phase.rectTransform.anchorMax = phase.rectTransform.pivot = Vector2.one;
        phase.rectTransform.anchoredPosition = new Vector2(-right, 0);
        phase.rectTransform.sizeDelta = new Vector2(300f, 30f);
    }

    bool CoversTopControls(Vector2 point) => TopControlAt(journalButton, point) || TopControlAt(practice, point);

    static bool TopControlAt(Button button, Vector2 point) => button && button.isActiveAndEnabled && button.IsInteractable() &&
        RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform, point);

    static void TopRow(RectTransform row, float left, float right, float top, float bottom)
    {
        row.anchorMin = Vector2.up;
        row.anchorMax = Vector2.one;
        row.offsetMin = new Vector2(left, -bottom);
        row.offsetMax = new Vector2(-right, -top);
    }

    Text MakeLabel(Transform parent, string label, int fontSize, TextAnchor alignment)
    {
        var text = Instantiate(commands, parent);
        text.name = label;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        return text;
    }

    static Image Panel(Transform parent, string label, Color color)
    {
        var image = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Image Meter(RectTransform parent, string name, float y, float height, float inset)
    {
        var track = Panel(parent, name, new Color32(74, 89, 82, 255)).rectTransform;
        track.anchorMin = Vector2.zero;
        track.anchorMax = Vector2.right;
        track.offsetMin = new Vector2(inset, y);
        track.offsetMax = new Vector2(-inset, y + height);
        var fill = Panel(track, "余量", Color.white);
        var rect = fill.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0, .5f);
        return fill;
    }

    InitiativePortrait CreateInitiativePortrait(int index)
    {
        var wire = Panel(initiativeQueue, "蓄势连线", RailInk);
        var point = Panel(initiativeQueue, "实际蓄势位置", RailInk);
        PlaceTimelineElement(wire.rectTransform, Vector2.up, 0, RailY, 1, 1);
        PlaceTimelineElement(point.rectTransform, Vector2.up, 0, RailY, 6, 6);

        var token = new GameObject("行动头像", typeof(RectTransform)).GetComponent<RectTransform>();
        token.SetParent(initiativeQueue, false);
        PlaceTimelineElement(token, Vector2.up, 0, PortraitY, 0, 0);
        var frame = Panel(token, "头像边框", Jade);
        PlaceTimelineElement(frame.rectTransform, new Vector2(.5f, .5f), 0, 0, PortraitSize, PortraitSize);
        var background = Panel(frame.transform, "头像底", new Color32(21, 34, 31, 255)).rectTransform;
        PlaceTimelineElement(background, new Vector2(.5f, .5f), 0, 0, PortraitSize - 4, PortraitSize - 4);

        var portrait = new GameObject("头像", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
        portrait.transform.SetParent(background, false);
        portrait.raycastTarget = false;
        PlaceTimelineElement(portrait.rectTransform, new Vector2(.5f, .5f), 0, 0, PortraitSize - 5, PortraitSize - 5);

        var name = MakeLabel(token, "姓名", 16, TextAnchor.MiddleCenter);
        name.resizeTextForBestFit = true;
        name.resizeTextMinSize = 9;
        name.resizeTextMaxSize = 16;
        name.verticalOverflow = VerticalWrapMode.Truncate;
        PlaceTimelineElement(name.rectTransform, new Vector2(.5f, .5f), 0, -25, 112, 18);
        OutlineInitiativeLabel(name);

        var progress = MakeLabel(token, "蓄势数值", 14, TextAnchor.MiddleLeft);
        progress.horizontalOverflow = HorizontalWrapMode.Overflow;
        PlaceTimelineElement(progress.rectTransform, new Vector2(.5f, .5f), 50, -7, 60, 19);
        OutlineInitiativeLabel(progress);
        var reaction = MakeLabel(token, "反应数值", 13, TextAnchor.MiddleLeft);
        reaction.horizontalOverflow = HorizontalWrapMode.Overflow;
        reaction.color = SecondaryInk;
        PlaceTimelineElement(reaction.rectTransform, new Vector2(.5f, .5f), 50, 8, 60, 17);
        OutlineInitiativeLabel(reaction);

        return new InitiativePortrait { token = token, frame = frame, portrait = portrait, point = point, wire = wire,
            name = name, progress = progress, reaction = reaction, index = index };
    }

    static void PlaceTimelineElement(RectTransform rect, Vector2 anchor, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
    }

    static void SetPortrait(InitiativePortrait marker, Sprite sprite, bool beast)
    {
        if (marker.sprite == sprite) return;
        marker.sprite = sprite;
        marker.portrait.texture = sprite ? sprite.texture : null;
        if (!sprite) return;
        var uv = DataUtility.GetOuterUV(sprite);
        float width = uv.z - uv.x;
        float height = uv.w - uv.y;
        // Crop the existing battle sprite to fill a square with its upper body.
        float crop = Mathf.Min(sprite.rect.width * .86f, sprite.rect.height * (beast ? .86f : .61f));
        float cropX = width * crop / sprite.rect.width;
        float cropY = height * crop / sprite.rect.height;
        float x = Mathf.Clamp(width * (beast ? .70f : .50f) - cropX * .5f, 0, width - cropX);
        float y = Mathf.Clamp(height * (beast ? .53f : .69f) - cropY * .5f, 0, height - cropY);
        marker.portrait.uvRect = new Rect(uv.x + x, uv.y + y, cropX, cropY);
    }

    void BindInitiative(SparMatch match, string stage)
    {
        phaseStage = stage;
        displayedTenth = -1;
        bool running = !controller.Finished || controller.PlayingAction;
        topBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, running ? InitiativePanelHeight : InitiativeHeaderHeight);
        LayoutTopControls(match);
        initiativeQueue.gameObject.SetActive(running);
        timeline.gameObject.SetActive(running);
        if (!running) return;

        int count = match.Units.Count;
        while (initiativePortraits.Count < count)
            initiativePortraits.Add(CreateInitiativePortrait(initiativePortraits.Count));
        livingPortraits.Clear();
        for (int i = 0; i < initiativePortraits.Count; i++)
        {
            var marker = initiativePortraits[i];
            bool alive = i < count && match.Units[i].Alive;
            marker.token.gameObject.SetActive(alive);
            marker.wire.gameObject.SetActive(alive);
            marker.point.gameObject.SetActive(alive);
            if (!alive) continue;

            var unit = match.Units[i];
            bool ready = unit == match.ActiveUnit && !controller.ChargingInitiative;
            Color color = ready ? Gold : unit.Friendly ? Jade : Vermilion;
            marker.token.name = unit.Name + " · 行动头像";
            marker.point.name = unit.Name + " · 实际蓄势位置";
            int titleSeparator = unit.Name.IndexOf(" · ", System.StringComparison.Ordinal);
            marker.name.text = titleSeparator >= 0 ? unit.Name.Substring(0, titleSeparator) : unit.Name;
            marker.name.color = ready ? Gold : Color.white;
            marker.reaction.text = $"反应 {unit.Reaction}";
            marker.frame.color = color;
            marker.point.color = color;
            marker.wire.color = new Color(color.r, color.g, color.b, .84f);
            marker.progress.color = ready ? Gold : Color.white;
            marker.displayedTenths = -1;
            SetPortrait(marker, controller.Board.PieceSprite(i), match.InField && match.Party != FieldParty.Brigands && i == 2);
            livingPortraits.Add(marker);
        }

        var order = new StringBuilder("出手预告（息）");
        bool needsArrow = controller.ChargingInitiative;
        if (needsArrow) order.Append($"　{match.ActiveUnit.Name} {match.CombatTime:0.#}");
        foreach (var turn in match.Upcoming)
        {
            order.Append(needsArrow ? "　→　" : "　");
            order.Append($"{turn.Actor.Name} {turn.At:0.#}");
            needsArrow = true;
        }
        timeline.text = order.ToString();
        UpdateInitiativeValues(match, controller.DisplayCombatTime);
    }

    void UpdateInitiativeValues(SparMatch match, double shown)
    {
        float left = RailInset;
        float right = Mathf.Max(left, initiativeQueue.rect.width - RailInset);
        foreach (var marker in livingPortraits)
        {
            marker.fraction = match.ChargeAt(match.Units[marker.index], shown);
            marker.railX = Mathf.Lerp(left, right, marker.fraction);
            marker.point.rectTransform.anchoredPosition = new Vector2(marker.railX, -RailY);

            int tenths = Mathf.FloorToInt(marker.fraction * 1000f);
            if (tenths == marker.displayedTenths) continue;
            marker.displayedTenths = tenths;
            marker.progress.text = $"{tenths / 10f:0.#}/100";
        }

        livingPortraits.Sort((a, b) =>
        {
            int progress = a.fraction.CompareTo(b.fraction);
            return progress != 0 ? progress : a.index.CompareTo(b.index);
        });
        float spacing = livingPortraits.Count < 2 ? 0 :
            Mathf.Min(PortraitSpacing, (right - left) / (livingPortraits.Count - 1));
        PlacePortraits(left, right, spacing);
    }

    void PlacePortraits(float left, float right, float spacing)
    {
        // Fit collision groups around their real charge points, limiting how far
        // any portrait is moved. The sorted indices reserve one gap per neighbour.
        portraitGroups.Clear();
        for (int i = 0; i < livingPortraits.Count; i++)
        {
            var group = new PortraitGroup { Count = 1, Position = livingPortraits[i].railX - i * spacing };
            while (portraitGroups.Count > 0)
            {
                var previous = portraitGroups[portraitGroups.Count - 1];
                if (previous.Position <= group.Position) break;
                int total = previous.Count + group.Count;
                group.Position = (previous.Position * previous.Count + group.Position * group.Count) / total;
                group.Count = total;
                portraitGroups.RemoveAt(portraitGroups.Count - 1);
            }
            portraitGroups.Add(group);
        }

        int index = 0;
        float lastPosition = right - spacing * (livingPortraits.Count - 1);
        foreach (var group in portraitGroups)
        {
            float position = Mathf.Clamp(group.Position, left, lastPosition);
            for (int member = 0; member < group.Count; member++, index++)
            {
                var marker = livingPortraits[index];
                marker.token.anchoredPosition = new Vector2(position + index * spacing, -PortraitY);
                Vector2 from = marker.point.rectTransform.anchoredPosition;
                Vector2 to = marker.token.anchoredPosition + Vector2.down * (PortraitSize * .5f);
                Vector2 delta = to - from;
                var wire = marker.wire.rectTransform;
                wire.anchoredPosition = from + delta * .5f;
                wire.sizeDelta = new Vector2(delta.magnitude, 1.5f);
                wire.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
        }
    }

    HeadBar CreateHeadBar()
    {
        var root = Panel(headLayer, "参战者气血", new Color32(25, 37, 36, 248)).rectTransform;
        root.pivot = new Vector2(.5f, 0);
        root.sizeDelta = new Vector2(106, 32);
        var caption = MakeLabel(root, "当前气血", 18, TextAnchor.MiddleCenter);
        caption.horizontalOverflow = HorizontalWrapMode.Overflow;
        TopRow(caption.rectTransform, 5, 5, 1, 24);
        return new HeadBar
        {
            root = root,
            health = Meter(root, "气血", 4, 4, 5),
            caption = caption,
            badges = new[]
            {
                CreateStatusBadge(root, "缚枝", "缚", Jade),
                CreateStatusBadge(root, "灼烧", "灼", BurnInk),
                CreateStatusBadge(root, "护体", "护", WardInk)
            }
        };
    }

    void BindHeads(SparMatch match)
    {
        headLayer.gameObject.SetActive(true);
        if (!battleCamera) battleCamera = Camera.main;
        while (heads.Count < match.Units.Count) heads.Add(CreateHeadBar());
        for (int i = 0; i < heads.Count; i++)
        {
            var bar = heads[i];
            var unit = i < match.Units.Count ? match.Units[i] : null;
            bool wounded = unit != null && unit == match.Companion && !unit.Alive && match.RescueRounds > 0;
            bool visible = unit != null && (unit.Alive || wounded);
            bar.root.gameObject.SetActive(visible);
            if (!visible) continue;
            bar.root.name = unit.Name + " · 气血";
            string health = $"气血 {unit.Hp} / {unit.MaxHp}";
            bar.caption.text = wounded ? $"{health}　余 {match.RescueRounds} 动救援" : health;
            BindStatusBadges(bar, match, unit);
            bar.caption.color = wounded ? Vermilion : Color.white;
            bar.health.rectTransform.localScale = new Vector3((float)unit.Hp / unit.MaxHp, 1, 1);
            bar.health.color = unit.Friendly ? Jade : Vermilion;
        }
    }

    void LateUpdate()
    {
        if (!topBar.gameObject.activeSelf || controller == null || !controller.InMatch) return;
        var match = controller.Match;
        double shown = controller.DisplayCombatTime;
        int tenth = Mathf.FloorToInt((float)shown * 10 + .0001f);
        if (tenth != displayedTenth)
        {
            displayedTenth = tenth;
            phase.text = $"第 {match.TurnNumber} 动　{tenth / 10f:0.#} 息　·　{phaseStage}";
        }
        if (initiativeQueue.gameObject.activeSelf) UpdateInitiativeValues(match, shown);
        UpdateBattleFrost();
        if (!headLayer.gameObject.activeSelf || !battleCamera) return;

        visibleHeads.Clear();
        float scale = Mathf.Clamp(960f / Mathf.Max(1, Screen.width), 1, 1.32f);
        for (int i = 0; i < match.Units.Count; i++)
        {
            var bar = heads[i];
            if (!bar.root.gameObject.activeSelf) continue;
            var screen = battleCamera.WorldToScreenPoint(controller.Board.HeadPosition(i));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(headLayer, screen, null, out var head);
            bar.head = head;
            bar.root.localScale = new Vector3(scale, scale, 1);
            visibleHeads.Add(bar);
        }
        LayoutWorldHeads(scale);
        UpdateStatusTooltip();
    }
}
