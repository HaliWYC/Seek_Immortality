using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class SparHud
{
    sealed class JournalRow
    {
        public Text Heading;
        public Text Actions;
    }

    readonly List<JournalRow> journalRows = new List<JournalRow>();
    Button journalButton;
    Image journalOverlay;
    Text journalState;
    ScrollRect journalScroll;
    RectTransform journalEntries;
    SparMatch journalMatch;

    public bool JournalOpen => journalOverlay != null && journalOverlay.gameObject.activeSelf;

    void CreateJournal()
    {
        journalButton = CopyButton(primary, Vector2.zero, "战斗记录  L", () => controller?.ToggleJournal());
        var button = (RectTransform)journalButton.transform;
        button.SetParent(topBar, false);
        PlaceTopControl(journalButton, 0, 117);
        PlaceTopControl(practice, 126, 117);

        journalOverlay = Panel(topBar.parent, "战斗记录遮罩", new Color32(8, 16, 14, 171));
        journalOverlay.raycastTarget = true;
        journalOverlay.rectTransform.anchorMin = Vector2.zero;
        journalOverlay.rectTransform.anchorMax = Vector2.one;
        journalOverlay.rectTransform.offsetMin = journalOverlay.rectTransform.offsetMax = Vector2.zero;

        var border = Panel(journalOverlay.transform, "战斗记录外框", RailInk).rectTransform;
        border.anchorMin = new Vector2(.2f, .19f);
        border.anchorMax = new Vector2(.8f, .83f);
        border.offsetMin = border.offsetMax = Vector2.zero;
        var paper = Panel(border, "战斗记录底板", new Color32(29, 46, 40, 255)).rectTransform;
        paper.anchorMin = Vector2.zero;
        paper.anchorMax = Vector2.one;
        paper.offsetMin = Vector2.one;
        paper.offsetMax = -Vector2.one;

        var title = MakeLabel(paper, "战斗记录标题", 26, TextAnchor.MiddleLeft);
        title.text = "战斗记录";
        title.color = Gold;
        TopRow(title.rectTransform, 22, 230, 8, 53);
        journalState = MakeLabel(paper, "战斗记录状态", 16, TextAnchor.MiddleRight);
        journalState.color = SecondaryInk;
        TopRow(journalState.rectTransform, 150, 151, 8, 53);

        var close = CopyButton(primary, Vector2.zero, "关闭  Esc", () => controller?.ToggleJournal());
        var closeRect = (RectTransform)close.transform;
        closeRect.SetParent(paper, false);
        closeRect.anchorMin = closeRect.anchorMax = Vector2.one;
        closeRect.anchoredPosition = new Vector2(-20, -12);
        FitLabel(close);
        close.gameObject.SetActive(true);

        var body = Panel(paper, "战斗记录列表", new Color32(23, 37, 33, 255)).rectTransform;
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(20, 20);
        body.offsetMax = new Vector2(-20, -63);
        CreateJournalScroll(body);
        journalOverlay.gameObject.SetActive(false);
    }

    void CreateJournalScroll(RectTransform body)
    {
        journalScroll = body.gameObject.AddComponent<ScrollRect>();
        journalScroll.horizontal = false;
        journalScroll.movementType = ScrollRect.MovementType.Clamped;
        journalScroll.scrollSensitivity = 44;

        var viewport = Panel(body, "可视范围", Color.clear);
        viewport.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.rectTransform.anchorMin = Vector2.zero;
        viewport.rectTransform.anchorMax = Vector2.one;
        viewport.rectTransform.offsetMin = new Vector2(14, 9);
        viewport.rectTransform.offsetMax = new Vector2(-26, -9);
        journalScroll.viewport = viewport.rectTransform;

        journalEntries = Panel(viewport.transform, "回合明细", Color.clear).rectTransform;
        journalEntries.anchorMin = Vector2.up;
        journalEntries.anchorMax = Vector2.one;
        journalEntries.pivot = Vector2.up;
        journalEntries.offsetMin = journalEntries.offsetMax = Vector2.zero;
        var layout = journalEntries.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        journalEntries.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        journalScroll.content = journalEntries;

        var track = Panel(body, "滚动条", new Color32(55, 76, 67, 255));
        track.raycastTarget = true;
        track.rectTransform.anchorMin = Vector2.right;
        track.rectTransform.anchorMax = Vector2.one;
        track.rectTransform.offsetMin = new Vector2(-18, 9);
        track.rectTransform.offsetMax = new Vector2(-8, -9);
        var thumb = Panel(track.transform, "滚动把手", RailInk);
        thumb.rectTransform.anchorMin = Vector2.zero;
        thumb.rectTransform.anchorMax = Vector2.one;
        thumb.rectTransform.offsetMin = thumb.rectTransform.offsetMax = Vector2.zero;
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = thumb;
        scrollbar.handleRect = thumb.rectTransform;
        journalScroll.verticalScrollbar = scrollbar;
    }

    void BindJournal(SparMatch match)
    {
        if (journalMatch != match)
        {
            SetJournalOpen(false);
            journalMatch = match;
        }
        journalButton.gameObject.SetActive(match != null);
        journalButton.interactable = controller != null && !controller.ConfirmingYield && !controller.PracticeMenuOpen;
    }

    void HideJournal()
    {
        SetJournalOpen(false);
        journalMatch = null;
        journalButton.gameObject.SetActive(false);
    }

    public void SetJournalOpen(bool open)
    {
        if (open && journalMatch == null) return;
        journalOverlay.gameObject.SetActive(open);
        if (!open) return;
        journalOverlay.transform.SetAsLastSibling();
        RebuildJournal();
        Canvas.ForceUpdateCanvases();
        journalScroll.StopMovement();
        journalScroll.verticalNormalizedPosition = 0;
    }

    void RebuildJournal()
    {
        var turns = journalMatch.Journal;
        bool finished = journalMatch.Status != SparStatus.PlayerTurn && journalMatch.Status != SparStatus.OpponentTurn;
        journalState.text = finished ? "斗法已结束" : "斗法已暂停";
        var actions = new StringBuilder();
        for (int i = 0; i < turns.Count; i++)
        {
            if (i == journalRows.Count) journalRows.Add(CreateJournalRow());
            var row = journalRows[i];
            row.Heading.transform.parent.gameObject.SetActive(true);
            var turn = turns[i];
            row.Heading.text = $"第 {turn.Number} 回合　·　{turn.Actor.Name}　·　共移动 {turn.Steps} 格";
            row.Heading.color = turn.Actor.Friendly ? Jade : Vermilion;
            actions.Clear();
            foreach (var entry in turn.Actions)
                AppendJournalEntry(actions, entry);
            row.Actions.text = actions.Length > 0 ? actions.ToString() :
                i == turns.Count - 1 && !finished ? "本回合尚无动作" : "本回合未移动或出招";
        }
        for (int i = turns.Count; i < journalRows.Count; i++)
            journalRows[i].Heading.transform.parent.gameObject.SetActive(false);
    }

    static void AppendJournalEntry(StringBuilder actions, SparJournalEntry entry)
    {
        if (actions.Length > 0) actions.AppendLine();
        if (entry.StatusEvents.Count > 0 && entry.StatusEvents[0].Kind == SparStatusEventKind.BurnTick)
            actions.Append("行动开始　·　");
        actions.Append(entry.Name);
        if (entry.Steps > 0) actions.Append($" {entry.Steps} 格");
        if (entry.Target != null && entry.HealthChanges.Count == 0) actions.Append($"　→　{entry.Target.Name}");
        bool targetLostHealth = false;
        foreach (var change in entry.HealthChanges)
        {
            if (change.Amount == 0) continue;
            if (change.Target == entry.Target && change.Amount < 0) targetLostHealth = true;
            actions.Append($"　·　{change.Target.Name}");
            actions.Append(change.Amount > 0 ? $"恢复 {change.Amount} 点气血" : $"受到 {-change.Amount} 点伤害");
        }
        foreach (var status in entry.StatusEvents)
        {
            switch (status.Kind)
            {
                case SparStatusEventKind.BindApplied:
                    actions.AppendLine().Append($"　缚枝附加　·　{status.Target.Name}下次本人行动少走 {status.Amount} 格");
                    break;
                case SparStatusEventKind.BurnApplied:
                    actions.AppendLine().Append($"　灼烧附加　·　{status.Target.Name}行动开始时扣 {status.Amount} 点气血，剩余 {status.Remaining} 次");
                    break;
                case SparStatusEventKind.BurnTick:
                    actions.Append($"　·　剩余 {status.Remaining} 次触发");
                    break;
                case SparStatusEventKind.WardApplied:
                    actions.AppendLine().Append($"　护体生效　·　{status.Target.Name}每次直击最多挡 {status.Amount} 点，剩余 {status.Remaining} 次");
                    break;
                case SparStatusEventKind.WardBlock:
                    actions.AppendLine().Append($"　护体触发　·　{status.Target.Name}抵挡 {status.Amount} 点伤害，剩余 {status.Remaining} 次");
                    if (!targetLostHealth) actions.Append("，本次未损气血");
                    break;
            }
        }
    }

    JournalRow CreateJournalRow()
    {
        var row = new GameObject("回合记录", typeof(RectTransform), typeof(VerticalLayoutGroup));
        row.transform.SetParent(journalEntries, false);
        var layout = row.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 2;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        Text heading = MakeLabel(row.transform, "回合", 22, TextAnchor.UpperLeft);
        heading.supportRichText = false;
        heading.verticalOverflow = VerticalWrapMode.Overflow;
        Text actions = MakeLabel(row.transform, "已结算动作", 20, TextAnchor.UpperLeft);
        actions.supportRichText = false;
        actions.verticalOverflow = VerticalWrapMode.Overflow;
        actions.color = SecondaryInk;
        return new JournalRow { Heading = heading, Actions = actions };
    }
}
