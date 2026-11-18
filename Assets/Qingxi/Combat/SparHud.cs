using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SparHud : MonoBehaviour
{
    [SerializeField] RectTransform topBar;
    [SerializeField] RectTransform bottomBar;
    [SerializeField] Text place;
    [SerializeField] Text phase;
    [SerializeField] Text participant;
    [SerializeField] Text cursorInfo;
    [SerializeField] Text feedbackLine;
    [SerializeField] Text commands;
    [SerializeField] Button primary;
    [SerializeField] Button secondary;
    [SerializeField] Text primaryLabel;
    [SerializeField] Text secondaryLabel;
    [SerializeField] Button nearbyButton;
    [SerializeField] RectTransform notice;
    [SerializeField] Text noticeText;

    QingxiSparring controller;
    float noticeUntil;
    Button fieldOffer, fieldHerb;
    Text fieldHerbLabel;

    void Awake()
    {
        primary.onClick.AddListener(PrimaryAction);
        secondary.onClick.AddListener(SecondaryAction);
        nearbyButton.onClick.AddListener(StartChallenge);
        fieldOffer = CopyButton(nearbyButton, new Vector2(0, 50), "G   松林护药", () => controller?.OpenFieldOffer());
        fieldHerb = CopyButton(primary, new Vector2(0, -48), "止血散  Q", () => controller?.UseFieldHerb());
        fieldHerbLabel = fieldHerb.GetComponentInChildren<Text>(true);
        HideBattle();
    }

    static Button CopyButton(Button template, Vector2 offset, string label, UnityEngine.Events.UnityAction action)
    {
        var button = Instantiate(template, template.transform.parent);
        button.name = label;
        ((RectTransform)button.transform).anchoredPosition += offset;
        button.GetComponentInChildren<Text>(true).text = label;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
        button.gameObject.SetActive(false);
        return button;
    }

    public void Bind(QingxiSparring sparring) => controller = sparring;

    void StartChallenge()
    {
        if (controller != null) controller.BeginSpar();
    }

    void PrimaryAction()
    {
        if (controller == null || !controller.InMatch) return;
        if (controller.Finished) controller.LeaveSpar();
        else if (controller.ConfirmingYield) controller.ConfirmYield();
        else controller.FinishTurn();
    }

    void SecondaryAction()
    {
        if (controller != null) controller.ToggleYield();
    }

    public void SetNearby(bool value)
    {
        bool show = value && controller != null && !controller.InMatch;
        if (show != nearbyButton.gameObject.activeSelf)
            nearbyButton.gameObject.SetActive(show);
    }

    public void SetFieldNearby(bool show)
    {
        if (fieldOffer.gameObject.activeSelf != show) fieldOffer.gameObject.SetActive(show);
    }

    public void Show(QingxiSparring sparring, string feedback)
    {
        controller = sparring;
        topBar.gameObject.SetActive(true);
        bottomBar.gameObject.SetActive(true);
        nearbyButton.gameObject.SetActive(false);
        fieldOffer.gameObject.SetActive(false);
        notice.gameObject.SetActive(false);

        var match = sparring.Match;
        var unit = sparring.Selected;
        var target = match.At(sparring.Cursor);
        string stage;
        switch (match.Status)
        {
            case SparStatus.PlayerTurn: stage = "我方行动"; break;
            case SparStatus.OpponentTurn: stage = "对方行动"; break;
            case SparStatus.Won: stage = "我方获胜"; break;
            case SparStatus.Lost: stage = "对方获胜"; break;
            default: stage = "我方认输"; break;
        }
        place.text = match.RescueRounds > 0 ? $"阿荇重伤  ·  可救援 {match.RescueRounds} 回合" : sparring.BattlePlace;
        if (match.InField && match.Status == SparStatus.Yielded) stage = "撤出山林";
        phase.text = $"第 {match.Round} 回合  /  {stage}";
        participant.text = unit == null ? "当前无人行动" :
            $"{unit.Name}    气力 {unit.Hp}/{unit.MaxHp}    移步 {(unit.Moved ? "已用" : unit.Steps.ToString())}    施术 {(unit.Acted ? "已用" : $"射程 {unit.Range}")}";
        cursorInfo.text = target != null && !target.Friendly
            ? $"{target.Name}   气力 {target.Hp}/{target.MaxHp}   射程 {target.Range}" : "";

        primary.interactable = match.Status == SparStatus.PlayerTurn || sparring.Finished || sparring.ConfirmingYield;
        fieldHerb.gameObject.SetActive(match.Party == FieldParty.Contract && !sparring.Finished &&
            !sparring.ConfirmingYield && match.CanCommand(unit) && unit == match.Companion && !match.BandageUsed);
        fieldHerb.interactable = unit != null && !unit.Acted && unit.Hp < unit.MaxHp;
        secondary.gameObject.SetActive(!sparring.Finished);
        if (sparring.Finished)
        {
            primaryLabel.text = "返回原地  E";
            feedbackLine.text = match.Message;
            int pay = QingxiSparring.FieldPay(match);
            commands.text = match.InField ? (pay == 4 ? "兽蜕三枚灵石，保住私符另获一枚。委托已入档。" :
                pay > 0 ? "兽蜕兑得三枚灵石，委托已入档。" : "你舍弃了兽蜕，结局已入档。") : "切磋点到为止。离场后可以继续探索。";
        }
        else if (sparring.ConfirmingYield)
        {
            primaryLabel.text = match.InField ? "确认撤离" : "确认认输";
            secondaryLabel.text = match.InField ? "继续斗法" : "继续切磋";
            feedbackLine.text = !match.InField ? "现在认输并结束本次切磋？" : match.RescueRounds > 0 ?
                (match.CanCarryNow ? "你们能带上阿荇，撤离将放弃全部兽蜕。" : "无人能扶起阿荇，此时撤离他将身亡。") : "放弃兽蜕，撤回官道？";
            commands.text = match.InField ? "Enter  确认撤离       Esc  留下" : "Enter  确认认输       Esc  继续切磋";
        }
        else
        {
            primaryLabel.text = "结束回合";
            secondaryLabel.text = match.InField ? "撤离  Esc" : "认输";
            feedbackLine.text = string.IsNullOrEmpty(feedback) ? match.Message : feedback;
            commands.text = match.RescueRounds > 0 ? "你或木傀与阿荇相邻后选撤离，可舍弃兽蜕带他回去。" :
                match.CompanionDied ? "阿荇已身亡。可选择撤离或继续驱兽。" :
                "点格行棋   /   Tab  换人   ·   方向键  选格   ·   空格  行动   ·   Enter  结束回合";
        }
        fieldHerbLabel.text = match.Party == FieldParty.Brigands ? "祭符  Q" : "止血散  Q";
        if (match.Party == FieldParty.Brigands)
        {
            fieldHerb.gameObject.SetActive(!sparring.Finished && !sparring.ConfirmingYield &&
                match.WardReady && match.CanCommand(unit) && unit == match.Units[0] && match.Units[2].Alive);
            fieldHerb.interactable = unit != null && !unit.Acted;
            if (sparring.Finished) commands.text = BrigandTerms.Load().result;
            else if (sparring.ConfirmingYield)
                feedbackLine.text = "舍去至多两枚灵石，绕行一日脱身？";
            else commands.text = match.WardReady ? "本人施术前按 Q 祭符   ·   Esc 弃财脱身   ·   气力耗尽则身亡" :
                match.FormationBroken ? "护身阵已破   ·   Esc 弃财脱身   ·   气力耗尽则身亡" :
                "灰衣客护阵每击抵三点   ·   Esc 弃财脱身   ·   气力耗尽则身亡";
        }
    }

    public void HideBattle()
    {
        topBar.gameObject.SetActive(false);
        bottomBar.gameObject.SetActive(false);
        nearbyButton.gameObject.SetActive(false);
        fieldOffer.gameObject.SetActive(false);
        fieldHerb.gameObject.SetActive(false);
        notice.gameObject.SetActive(false);
    }

    public bool CoversBoard(Vector2 pointerScreen) =>
        topBar.gameObject.activeSelf &&
        (RectTransformUtility.RectangleContainsScreenPoint(topBar, pointerScreen) ||
         RectTransformUtility.RectangleContainsScreenPoint(bottomBar, pointerScreen));

    public void Explain(string message)
    {
        noticeText.text = message;
        notice.gameObject.SetActive(true);
        noticeUntil = Time.unscaledTime + 4f;
    }

    void Update()
    {
        if (notice.gameObject.activeSelf && Time.unscaledTime >= noticeUntil)
            notice.gameObject.SetActive(false);
    }
}
