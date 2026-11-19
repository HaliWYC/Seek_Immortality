using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed partial class SparHud
{
    sealed class StatusBadge
    {
        public RectTransform Root;
        public Image Frame;
        public Text Count;
        public string Title, Description;
    }

    const float StatusBadgeWidth = 38f;
    const float StatusBadgeHeight = 25f;
    const float StatusBadgeGap = 4f;
    const float StatusTooltipMargin = 8f;
    static readonly Color BurnInk = new Color32(250, 160, 102, 255);
    static readonly Color WardInk = new Color32(132, 204, 234, 255);
    static readonly Vector2 StatusTooltipSize = new Vector2(298, 110);
    Image statusTooltip;
    Text statusTitle, statusDescription;

    StatusBadge CreateStatusBadge(RectTransform parent, string name, string icon, Color ink)
    {
        var frame = Panel(parent, name + "状态图标", ink);
        var root = frame.rectTransform;
        PlaceTimelineElement(root, new Vector2(1, .5f), 0, 0, StatusBadgeWidth, StatusBadgeHeight);
        var face = Panel(root, "图标底色", new Color32(27, 44, 41, 255)).rectTransform;
        PlaceTimelineElement(face, new Vector2(.5f, .5f), 0, 0, StatusBadgeWidth - 2, StatusBadgeHeight - 2);
        var glyph = MakeLabel(face, "状态字", 17, TextAnchor.MiddleCenter);
        glyph.text = icon;
        glyph.color = ink;
        TopRow(glyph.rectTransform, 2, 15, 0, StatusBadgeHeight - 2);
        var count = MakeLabel(face, "剩余次数", 14, TextAnchor.MiddleCenter);
        count.color = Color.white;
        count.horizontalOverflow = HorizontalWrapMode.Overflow;
        TopRow(count.rectTransform, 19, 1, 0, StatusBadgeHeight - 2);
        root.gameObject.SetActive(false);
        return new StatusBadge { Root = root, Frame = frame, Count = count, Title = name };
    }

    void BindStatusBadges(HeadBar bar, SparMatch match, SparUnit unit)
    {
        var bind = match.BindStatusFor(unit);
        var burn = match.BurnStatusFor(unit);
        var ward = match.WardStatusFor(unit);
        string bindTiming = bind.AppliesNow && !controller.ChargingInitiative ?
            "本次行动结束后解除" : "下次本人行动生效";
        SetStatusBadge(bar.badges[0], bind.RemainingTurns, bind.RemainingTurns > 0 ?
            $"移动上限减少 {bind.MovePenalty} 格\n剩余 {bind.RemainingTurns} 次本人行动\n{bindTiming}" : null);
        SetStatusBadge(bar.badges[1], burn.Remaining, burn.Remaining > 0 ?
            $"本人行动开始时扣 {burn.Amount} 点气血\n剩余 {burn.Remaining} 次触发\n重新施加可刷新次数" : null);
        SetStatusBadge(bar.badges[2], ward.Remaining, ward.Remaining > 0 ?
            $"每次直击最多抵挡 {ward.Amount} 点伤害\n剩余 {ward.Remaining} 次抵挡\n抵挡次数可跨回合保留" : null);

        int visible = 0;
        for (int i = bar.badges.Length - 1; i >= 0; i--)
        {
            var badge = bar.badges[i];
            if (!badge.Root.gameObject.activeSelf) continue;
            badge.Root.anchoredPosition = new Vector2(-StatusBadgeGap - StatusBadgeWidth * .5f -
                visible * (StatusBadgeWidth + StatusBadgeGap), 0);
            visible++;
        }
        float badgeSpace = visible * (StatusBadgeWidth + StatusBadgeGap);
        bar.root.sizeDelta = new Vector2(Mathf.Max(106, bar.caption.preferredWidth + 14) + badgeSpace, 32);
        TopRow(bar.caption.rectTransform, 5, 5 + badgeSpace, 1, 24);
        var track = (RectTransform)bar.health.transform.parent;
        track.offsetMax = new Vector2(-5 - badgeSpace, track.offsetMax.y);
    }

    static void SetStatusBadge(StatusBadge badge, int remaining, string description)
    {
        badge.Root.gameObject.SetActive(remaining > 0);
        if (remaining <= 0) return;
        badge.Count.text = remaining.ToString();
        badge.Description = description;
    }

    bool CanHoverStatus => controller != null && controller.InMatch && headLayer &&
        headLayer.gameObject.activeInHierarchy && !DemoRun.Modal && !controller.PracticeMenuOpen &&
        !controller.ConfirmingYield && !JournalOpen;

    StatusBadge HoveredStatusBadge(Vector2 screenPoint)
    {
        if (CoversTopControls(screenPoint) || CoversActionDock(screenPoint)) return null;

        foreach (var bar in heads)
            foreach (var badge in bar.badges)
                if (badge.Root.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(badge.Root, screenPoint))
                    return badge;
        return null;
    }

    bool CoversStatusPointer(Vector2 screenPoint) =>
        CanHoverStatus && (HoveredStatusBadge(screenPoint) != null ||
            (statusTooltip && statusTooltip.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(statusTooltip.rectTransform, screenPoint)));

    internal static Rect BoundsOnLayer(RectTransform panel, RectTransform layer)
    {
        if (!panel || !panel.gameObject.activeInHierarchy) return default;
        var lower = layer.InverseTransformPoint(panel.TransformPoint(panel.rect.min));
        var upper = layer.InverseTransformPoint(panel.TransformPoint(panel.rect.max));
        return Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
    }

    internal static bool OverlapsHorizontally(Rect bounds, float left, float right) =>
        bounds.width > 0 && bounds.height > 0 && left < bounds.xMax && right > bounds.xMin;

    void LayoutWorldHeads(float scale)
    {
        // Stagger nearby labels while letting ones outside the dock stay close to their fighter.
        visibleHeads.Sort((a, b) => a.head.y.CompareTo(b.head.y));
        Rect area = headLayer.rect;
        Rect top = BoundsOnLayer(topBar, headLayer);
        Rect dock = BoundsOnLayer(bottomBar, headLayer);
        float ceiling = top.yMin - 3;
        for (int i = 0; i < visibleHeads.Count; i++)
        {
            var bar = visibleHeads[i];
            var position = bar.head;
            float halfWidth = bar.root.sizeDelta.x * scale * .5f + 3;
            position.x = Mathf.Clamp(position.x, area.xMin + halfWidth, area.xMax - halfWidth);
            position.y = Mathf.Max(position.y, HeadFloor(area, dock, position.x, halfWidth));
            for (int j = 0; j < i; j++)
            {
                var previous = visibleHeads[j].root;
                float spacing = (previous.sizeDelta.x + bar.root.sizeDelta.x) * scale * .5f + 2;
                if (Mathf.Abs(previous.anchoredPosition.x - position.x) < spacing)
                    position.y = Mathf.Max(position.y, previous.anchoredPosition.y + previous.sizeDelta.y * scale + 2);
            }
            bar.root.anchoredPosition = position;
        }

        // If a group reaches the rail, make room downward from its highest label.
        for (int i = visibleHeads.Count - 1; i >= 0; i--)
        {
            var root = visibleHeads[i].root;
            var position = root.anchoredPosition;
            float height = root.sizeDelta.y * scale;
            position.y = Mathf.Min(position.y, ceiling - height);
            for (int j = i + 1; j < visibleHeads.Count; j++)
            {
                var next = visibleHeads[j].root;
                float spacing = (next.sizeDelta.x + root.sizeDelta.x) * scale * .5f + 2;
                if (Mathf.Abs(next.anchoredPosition.x - position.x) < spacing)
                    position.y = Mathf.Min(position.y, next.anchoredPosition.y - height - 2);
            }
            float halfWidth = root.sizeDelta.x * scale * .5f + 3;
            position.y = Mathf.Max(position.y, Mathf.Min(ceiling - height, HeadFloor(area, dock, position.x, halfWidth)));
            root.anchoredPosition = position;
        }
    }

    static float HeadFloor(Rect area, Rect dock, float x, float halfWidth) =>
        (OverlapsHorizontally(dock, x - halfWidth, x + halfWidth) ? dock.yMax : area.yMin) + 3;

    void CreateStatusTooltip()
    {
        statusTooltip = Panel(headLayer, "状态说明", Jade);
        var tooltip = statusTooltip.rectTransform;
        tooltip.anchorMin = tooltip.anchorMax = new Vector2(.5f, .5f);
        tooltip.pivot = Vector2.up;
        tooltip.sizeDelta = StatusTooltipSize;
        var background = Panel(tooltip, "说明底色", new Color32(20, 34, 29, 255)).rectTransform;
        background.anchorMin = Vector2.zero;
        background.anchorMax = Vector2.one;
        background.offsetMin = Vector2.one;
        background.offsetMax = -Vector2.one;

        statusTitle = MakeLabel(background, "状态名称", 18, TextAnchor.MiddleLeft);
        TopRow(statusTitle.rectTransform, 11, 11, 5, 30);
        statusDescription = MakeLabel(background, "状态效果与持续时间", 16, TextAnchor.MiddleLeft);
        statusDescription.color = Color.white;
        TopRow(statusDescription.rectTransform, 11, 11, 32, 104);
    }

    void UpdateStatusTooltip()
    {
        var mouse = Mouse.current;
        var hovered = CanHoverStatus && mouse != null ? HoveredStatusBadge(mouse.position.ReadValue()) : null;
        if (hovered == null)
        {
            if (statusTooltip) statusTooltip.gameObject.SetActive(false);
            return;
        }

        if (!statusTooltip) CreateStatusTooltip();
        var tooltip = statusTooltip.rectTransform;
        statusTooltip.color = statusTitle.color = hovered.Frame.color;
        statusTitle.text = hovered.Title;
        statusDescription.text = hovered.Description;
        if (!statusTooltip.gameObject.activeSelf)
        {
            tooltip.SetAsLastSibling();
            statusTooltip.gameObject.SetActive(true);
        }

        var owner = (RectTransform)hovered.Root.parent;
        Vector3 badge = headLayer.InverseTransformPoint(hovered.Root.position);
        float start = headLayer.InverseTransformPoint(owner.TransformPoint(owner.rect.xMin, 0, 0)).x;
        float end = headLayer.InverseTransformPoint(owner.TransformPoint(owner.rect.xMax, 0, 0)).x;
        Rect area = headLayer.rect;
        float left = area.xMin + StatusTooltipMargin;
        float right = Mathf.Max(left, area.xMax - StatusTooltipMargin - StatusTooltipSize.x);
        float x = end + StatusTooltipMargin;
        if (x > right) x = start - StatusTooltipMargin - StatusTooltipSize.x;
        x = Mathf.Clamp(x, left, right);
        Rect dock = BoundsOnLayer(bottomBar, headLayer);
        float top = BoundsOnLayer(topBar, headLayer).yMin - StatusTooltipMargin;
        float floor = OverlapsHorizontally(dock, x, x + StatusTooltipSize.x) ? dock.yMax : area.yMin;
        float bottom = floor + StatusTooltipMargin + StatusTooltipSize.y;
        tooltip.anchoredPosition = new Vector2(x,
            Mathf.Clamp(badge.y + 16, Mathf.Min(bottom, top), top));
    }
}
