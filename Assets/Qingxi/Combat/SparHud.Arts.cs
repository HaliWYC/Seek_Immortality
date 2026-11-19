using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class SparHud
{
    const float DockWidth = 744f;
    const float DockGutter = 12f;
    const float CarryWidth = 94f;
    const float SlotGap = 6f;
    const float SlotTop = 57f;
    const float SlotHeight = 39f;
    static readonly WuXing[] Elements = { WuXing.锐金, WuXing.灵木, WuXing.弱水, WuXing.离火, WuXing.厚土 };
    static readonly string[] ElementColors = { "F1D799", "A5DEAE", "A9D9F4", "FFA68B", "E7BF9C" };
    static readonly string[] CarryKeys = { "Z", "X" };
    readonly List<Button> spells = new List<Button>();
    readonly Button[] carried = new Button[CultivatorBag.CarrySlotCount];
    Text daoCang, currentPayment;
    Button payment, practice;

    void CreateArtsBar()
    {
        bottomBar.anchorMin = bottomBar.anchorMax = new Vector2(.5f, 0);
        bottomBar.pivot = new Vector2(.5f, 0);
        bottomBar.anchoredPosition = new Vector2(0, 8);
        bottomBar.sizeDelta = new Vector2(DockWidth, 145);
        bottomBar.GetComponent<Image>().color = new Color32(22, 35, 31, 238);
        var accent = bottomBar.Find("棋手边线") as RectTransform;
        if (accent) TopRow(accent, 0, 0, 0, 2);

        daoCang = Instantiate(commands, bottomBar);
        daoCang.name = "五行道藏";
        daoCang.supportRichText = true;
        daoCang.fontSize = 15;
        daoCang.raycastTarget = false;
        currentPayment = Instantiate(daoCang, bottomBar);
        currentPayment.name = "本次付气";
        currentPayment.alignment = TextAnchor.MiddleRight;
        currentPayment.color = Gold;
        payment = CopyButton(primary, Vector2.zero, "换气  R", () => controller?.CycleSpellPayment());
        FitLabel(payment);
        for (int i = 0; i < carried.Length; i++)
        {
            int slot = i;
            var button = CopyButton(primary, Vector2.zero, "携带槽 " + CarryKeys[i],
                () => controller?.SelectCarriedSlot(slot));
            FitLabel(button);
            carried[i] = button;
        }
        FitLabel(primary);
        FitLabel(secondary);
        FitLabel(fieldHerb);
        practice = CopyButton(primary, Vector2.zero, "配招重试  P", () => controller?.TogglePracticeMenu());
        RectTransform rect = (RectTransform)practice.transform;
        rect.SetParent(topBar, false);
        rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-335, -5);
        practice.GetComponentInChildren<Text>(true).text = "配招重试  P";
    }

    bool CoversActionDock(Vector2 screenPoint) => bottomBar.gameObject.activeInHierarchy &&
        RectTransformUtility.RectangleContainsScreenPoint(bottomBar, screenPoint);

    void LayoutActionDock(QingxiSparring sparring)
    {
        const float right = DockWidth - DockGutter;
        const float actionsLeft = right - 218;
        bool hasArts = daoCang.gameObject.activeSelf;
        bool showHerb = fieldHerb.gameObject.activeSelf;
        bottomBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, hasArts ? 145 : 90);

        PlaceAt((RectTransform)primary.transform, right - 114, 8, 114, 26);
        PlaceAt((RectTransform)secondary.transform, actionsLeft, 8, 97, 26);
        PlaceAt((RectTransform)fieldHerb.transform, actionsLeft - 108, 8, 102, 26);
        float titleWidth = (showHerb ? actionsLeft - 114 : actionsLeft - 6) - DockGutter;
        PlaceAt(participant.rectTransform, DockGutter, 7, titleWidth, 28);
        participant.fontSize = 17;
        participant.resizeTextForBestFit = true;
        participant.resizeTextMinSize = 13;
        participant.resizeTextMaxSize = 17;
        participant.verticalOverflow = VerticalWrapMode.Truncate;

        bool showTarget = hasArts && cursorInfo.text.Length > 0;
        cursorInfo.gameObject.SetActive(showTarget);
        cursorInfo.alignment = TextAnchor.MiddleRight;
        cursorInfo.fontSize = 14;
        currentPayment.gameObject.SetActive(hasArts && !showTarget && sparring.AwaitingPlayer &&
            sparring.Match.ActiveUnit == sparring.Match.Units[0] && sparring.Match.SpellSelected);
        PlaceAt(currentPayment.rectTransform, right - 235, 35, 235, 20);
        PlaceAt(cursorInfo.rectTransform, right - 235, 35, 235, 20);
        PlaceAt(daoCang.rectTransform, DockGutter, 35, 400, 20);
        PlaceAt((RectTransform)payment.transform, 420, 35, 79, 20);
        payment.GetComponentInChildren<Text>(true).fontSize = 14;

        PlaceAt(feedbackLine.rectTransform, DockGutter, hasArts ? 97 : 35, right - DockGutter, hasArts ? 32 : 28);
        feedbackLine.fontSize = 15;
        feedbackLine.resizeTextForBestFit = true;
        feedbackLine.resizeTextMinSize = 13;
        feedbackLine.resizeTextMaxSize = 15;
        feedbackLine.verticalOverflow = VerticalWrapMode.Truncate;
        PlaceAt(commands.rectTransform, DockGutter, hasArts ? 129 : 64, right - DockGutter, hasArts ? 15 : 24);
        commands.fontSize = 13;
    }

    static void PlaceAt(RectTransform rect, float x, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
        rect.anchoredPosition = new Vector2(x, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    static void FitLabel(Button button)
    {
        var label = button.GetComponentInChildren<Text>(true);
        var rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(4, 0);
        rect.offsetMax = new Vector2(-4, 0);
        label.fontSize = 16;
        label.alignment = TextAnchor.MiddleCenter;
        label.supportRichText = true;
    }

    void ShowArts(QingxiSparring sparring)
    {
        var match = sparring.Match;
        bool practiceAvailable = match.Party == FieldParty.Practice && !sparring.ConfirmingYield;
        practice.gameObject.SetActive(practiceAvailable);
        bool show = match.HasDaoCang && !sparring.Finished && !sparring.ConfirmingYield;
        bool heroTurn = sparring.AwaitingPlayer && match.ActiveUnit == match.Units[0];
        bool showCarry = show && match.HasCarriedSlots;
        daoCang.gameObject.SetActive(show);
        if (show && heroTurn && match.SpellSelected)
        {
            var cost = match.CastCostText;
            int effect = cost.IndexOf(" · ", System.StringComparison.Ordinal);
            currentPayment.text = "付气  " + (effect < 0 ? cost : cost.Substring(0, effect));
        }
        payment.gameObject.SetActive(show && heroTurn && match.HasPaymentChoice);
        int count = show ? match.PlayerSpells.Count + 1 : 0;
        while (spells.Count < count)
        {
            int slot = spells.Count;
            Button button = CopyButton(primary, Vector2.zero, "招式 " + slot, () => controller.SelectSpellSlot(slot));
            FitLabel(button);
            spells.Add(button);
        }
        for (int i = 0; i < spells.Count; i++) spells[i].gameObject.SetActive(i < count);
        ShowCarried(sparring, showCarry, heroTurn);
        if (!show) return;

        string intake = "道藏";
        for (int i = 0; i < Elements.Length; i++)
        {
            var element = Elements[i];
            int gained = match.Intake(element);
            string gain = gained > 0 ? $" +{gained}" : "";
            intake += $"  <color=#{ElementColors[i]}>{element} {match.DaoCang(element)}{gain}</color>";
        }
        daoCang.text = intake;

        float carrySpace = showCarry ? (CarryWidth + SlotGap) * carried.Length : 0;
        float width = Mathf.Min(171, (DockWidth - 2 * DockGutter - carrySpace + SlotGap) / count);
        for (int i = 0; i < count; i++)
        {
            var spell = i == 0 ? null : match.PlayerSpells[i - 1];
            var button = spells[i];
            bool fromRelic = match.IsRelicSpell(spell);
            PlaceAt((RectTransform)button.transform, DockGutter + i * width, SlotTop, width - SlotGap, SlotHeight);
            bool selected = i == 0 ? !match.SpellSelected : match.SpellSelected && match.PlayerSpell == spell;
            string reason = !heroTurn ? "" : spell ? match.SpellUnavailableReason(match.ActiveUnit, spell) :
                match.ActiveUnit.ActionPoints < SparMatch.StrikeCost ? $"需要 {SparMatch.StrikeCost} 点行动力" : "";
            bool available = heroTurn && reason.Length == 0;
            // An unaffordable spell stays inspectable, with its exact reason in the feedback line.
            button.interactable = heroTurn;
            button.targetGraphic.color = selected && available ? Gold : !available ? new Color32(153, 157, 142, 255) :
                fromRelic ? new Color32(158, 201, 196, 255) : new Color32(179, 202, 176, 255);
            string cost = spell ? $"法力{spell.ManaCost} · {CompactElementNames(spell.CostText)}" : $"行动力{SparMatch.StrikeCost}";
            if (spell && spell.ActionPointCost > 0) cost += $" · 行动{spell.ActionPointCost}";
            if (reason.Length > 0) cost = reason.Contains("本人行动") ? "冷却中" : CompactElementNames(reason);
            string title = spell ? spell.Name : "常击";
            string aim = fromRelic ? "法宝" : spell && spell.Target == FaShuTarget.Self ? "自身" :
                $"{(spell ? spell.Range : match.Units[0].Range)}格";
            SetActionCardLabel(button, $"{i} {title} {aim}", cost);
        }
    }

    static string CompactElementNames(string text) => text.Replace("锐金", "金").Replace("灵木", "木")
        .Replace("弱水", "水").Replace("离火", "火").Replace("厚土", "土").Replace("同类", "同").Replace("＋", "+");

    static void SetActionCardLabel(Button button, string heading, string detail)
    {
        var label = button.GetComponentInChildren<Text>(true);
        label.fontSize = 15;
        label.lineSpacing = .82f;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.text = $"{heading}\n<size=12>{detail}</size>";
    }

    void ShowCarried(QingxiSparring sparring, bool show, bool heroTurn)
    {
        for (int slot = 0; slot < carried.Length; slot++)
        {
            var button = carried[slot];
            button.gameObject.SetActive(show);
            if (!show) continue;
            float left = DockWidth - DockGutter - CarryWidth * carried.Length - SlotGap * (carried.Length - 1);
            PlaceAt((RectTransform)button.transform, left + (CarryWidth + SlotGap) * slot, SlotTop, CarryWidth, SlotHeight);
            var item = sparring.Match.Carried(slot);
            bool available = heroTurn && sparring.Match.CanUseCarried(sparring.Match.ActiveUnit, slot);
            button.interactable = heroTurn;
            button.targetGraphic.color = sparring.PendingCarriedSlot == slot && available ? Gold :
                available ? new Color32(158, 201, 196, 255) : new Color32(150, 162, 153, 255);
            string name = item == null ? "空槽" : item.Name;
            string detail = item == null ? "携带" : "携带 ×1";
            SetActionCardLabel(button, $"{CarryKeys[slot]} {name}", detail);
        }
    }
}
