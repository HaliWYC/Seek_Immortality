using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent, DefaultExecutionOrder(50)]
public sealed partial class SparHud : MonoBehaviour
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

    void Awake()
    {
        primary.onClick.AddListener(PrimaryAction);
        secondary.onClick.AddListener(SecondaryAction);
        // The board owns Enter/Space. A pointer-selected button must not also receive that submit.
        primary.navigation = secondary.navigation = new Navigation { mode = Navigation.Mode.None };
        nearbyButton.onClick.AddListener(StartChallenge);
        fieldOffer = CopyButton(nearbyButton, new Vector2(0, 50), "G   松林护药", () => controller?.OpenFieldOffer());
        fieldHerb = CopyButton(primary, new Vector2(0, -48), "止血散  Q", () => controller?.UseFieldHerb());
        CreateHeadLayer();
        CreateInitiativePanel();
        CreateArtsBar();
        CreateJournal();
        CreateBattleFrost();
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

    public void Bind(QingxiSparring sparring)
    {
        controller = sparring;
        sparring.Board.BindCombatEffects(GetComponent<Canvas>(), commands.font, topBar, bottomBar);
    }

    void StartChallenge()
    {
        if (controller != null) controller.BeginSpar();
    }

    void PrimaryAction()
    {
        if (controller == null || !controller.InMatch || JournalOpen || controller.OrderInputThisFrame) return;
        if (controller.CanLeaveSpar) controller.LeaveSpar();
        else if (controller.ConfirmingYield) controller.ConfirmYield();
        else controller.ConfirmOrEndTurn();
    }

    void SecondaryAction()
    {
        if (controller != null) controller.CancelOrYield();
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
        BindJournal(match);
        var unit = sparring.Selected;
        var target = match.At(sparring.Cursor);
        string stage;
        switch (match.Status)
        {
            case SparStatus.PlayerTurn: stage = sparring.ChargingInitiative ? "蓄势中" : "可指挥"; break;
            case SparStatus.OpponentTurn: stage = sparring.ChargingInitiative ? "蓄势中" : match.ActiveUnit.Friendly ? "友军行动" : "敌手行动"; break;
            case SparStatus.Won: stage = "我方获胜"; break;
            case SparStatus.Lost: stage = "对方获胜"; break;
            default: stage = "我方认输"; break;
        }
        place.text = match.RescueRounds > 0 ? $"阿荇重伤  ·  你还有 {match.RescueRounds} 次本人行动可救援" : sparring.BattlePlace;
        if (match.InField && match.Status == SparStatus.Yielded) stage = "撤出山林";
        if (match.Party == FieldParty.Practice && !string.IsNullOrEmpty(sparring.CurrentLoadoutName))
            place.text += "　·　" + sparring.CurrentLoadoutName;
        if (sparring.PlayingAction)
        {
            switch (sparring.PlaybackPhase)
            {
                case BattlePlaybackPhase.Movement: stage = "行走中"; break;
                case BattlePlaybackPhase.Windup: stage = "出招中"; break;
                case BattlePlaybackPhase.Impact: stage = "结算中"; break;
                case BattlePlaybackPhase.Closing: stage = "收势中"; break;
                case BattlePlaybackPhase.Opening: stage = "行动前结算"; break;
            }
        }
        BindInitiative(match, stage);
        string manaLabel = match.InField ? "法力" : "切磋法力";
        participant.text = unit == null ? "" : sparring.ChargingInitiative ?
            $"正在蓄势　·　{unit.Name}将在 {match.CombatTime:0.#} 息出手" :
            $"{unit.Name}　行动力 {unit.ActionPoints}/{unit.MaxActionPoints}　{manaLabel} {TravelModel.DisplayMana(unit.Mana)}/{unit.MaxMana}　可走 {unit.MovementLeft} 格";
        cursorInfo.text = target != null && !target.Friendly
            ? $"{target.Name}　气血 {target.Hp}/{target.MaxHp}　射程 {target.Range}" : "";

        primary.interactable = sparring.AwaitingPlayer || sparring.CanLeaveSpar || sparring.ConfirmingYield;
        fieldHerb.gameObject.SetActive(match.Party == FieldParty.Contract && !sparring.Finished &&
            !sparring.ConfirmingYield && match.CanCommand(unit) && unit == match.Companion && !match.BandageUsed);
        fieldHerb.interactable = sparring.AwaitingPlayer && match.CanUseAgreedHerb(unit);
        ShowArts(sparring);
        BindHeads(match);
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
            primaryLabel.text = sparring.PlayingAction ? stage : "结束回合";
            secondaryLabel.text = match.InField ? "撤离  Esc" : "认输";
            feedbackLine.text = string.IsNullOrEmpty(feedback) ? match.Message : feedback;
            commands.text = match.RescueRounds > 0 ? "你或木傀与阿荇相邻后选撤离，可舍弃兽蜕带他回去。" :
                match.CompanionDied ? "阿荇已身亡。可选择撤离或继续驱兽。" :
                "点格预览，再点确认　·　方向键选格　·　空格行动　·　Enter 收势　·　Tab 查看下位";
        }
        if (match.Party == FieldParty.Brigands)
        {
            if (sparring.Finished) commands.text = BrigandTerms.Load().result;
            else if (sparring.ConfirmingYield)
                feedbackLine.text = "舍去至多两枚灵石，绕行一日脱身？";
            else commands.text = match.WardReady ? "从携带槽祭破阵符   ·   Esc 弃财脱身   ·   气血耗尽则身亡" :
                match.FormationBroken ? "护身阵已破   ·   Esc 弃财脱身   ·   气血耗尽则身亡" :
                "灰衣客护阵每击抵三点   ·   Esc 弃财脱身   ·   气血耗尽则身亡";
        }
        if (sparring.HasOrder && !sparring.Finished && !sparring.ConfirmingYield)
        {
            primaryLabel.text = "确认行动";
            secondaryLabel.text = "取消  Esc";
            feedbackLine.text = sparring.OrderDescription;
            commands.text = "再点目标、按空格或 Enter 确认　　改点另选　　Esc 或右键取消";
        }
        if (sparring.Finished && match.InField && !DemoRun.Slain)
        {
            var hero = match.Units[0];
            commands.text += $"　·　返回时保留法力 {TravelModel.DisplayMana(hero.Mana)}/{hero.MaxMana}";
        }
        LayoutActionDock(sparring);
    }

    public void HideBattle()
    {
        HideJournal();
        HideBattleFrost();
        topBar.gameObject.SetActive(false);
        bottomBar.gameObject.SetActive(false);
        nearbyButton.gameObject.SetActive(false);
        fieldOffer.gameObject.SetActive(false);
        fieldHerb.gameObject.SetActive(false);
        headLayer.gameObject.SetActive(false);
        notice.gameObject.SetActive(false);
    }

    public bool CoversBoard(Vector2 pointerScreen) =>
        JournalOpen || (topBar.gameObject.activeSelf &&
        (CoversTopControls(pointerScreen) || CoversActionDock(pointerScreen) || CoversStatusPointer(pointerScreen)));

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
