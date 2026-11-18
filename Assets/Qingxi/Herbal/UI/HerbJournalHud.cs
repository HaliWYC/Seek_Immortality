using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HerbJournalHud : MonoBehaviour
{
    [SerializeField] Button prompt;
    [SerializeField] Text promptLabel;
    [SerializeField] GameObject backdrop;
    [SerializeField] Text pageName;
    [SerializeField] Text leftTitle;
    [SerializeField] Text leftSubtitle;
    [SerializeField] Text leftText;
    [SerializeField] Text rightTitle;
    [SerializeField] Text rightText;
    [SerializeField] Text footer;
    [SerializeField] Button close;
    [SerializeField] Text closeLabel;
    [SerializeField] Button journal;
    [SerializeField] Text journalLabel;
    [SerializeField] Button confirm;
    [SerializeField] Text confirmLabel;
    [SerializeField] GameObject specimenFrame;
    [SerializeField] Image specimenImage;
    [SerializeField] Sprite[] specimenStages;
    [SerializeField] GameObject practiceTrack;
    [SerializeField] Image[] practiceMarks;
    [SerializeField] Button[] actions;
    [SerializeField] Text[] actionLabels;

    HerbalStudyController controller;

    void Awake()
    {
        prompt.onClick.AddListener(() => controller?.Interact());
        close.onClick.AddListener(() => controller?.ClosePanel());
        journal.onClick.AddListener(FlipJournal);
        confirm.onClick.AddListener(() => controller?.ConfirmStudy());
        for (int i = 0; i < actions.Length; i++)
        {
            int index = i;
            actions[i].onClick.AddListener(() => controller?.Choose(index));
        }
    }

    public void Bind(HerbalStudyController source)
    {
        if (controller != null) controller.Changed -= Refresh;
        controller = source;
        if (isActiveAndEnabled && controller != null) controller.Changed += Refresh;
        Refresh();
    }

    void OnEnable()
    {
        if (controller != null)
        {
            controller.Changed -= Refresh;
            controller.Changed += Refresh;
            Refresh();
        }
    }

    void OnDisable()
    {
        if (controller != null) controller.Changed -= Refresh;
    }

    void FlipJournal()
    {
        if (controller == null) return;
        if (controller.Panel == HerbPanel.Journal || controller.Panel == HerbPanel.Ledger)
            controller.ToggleLedger();
        else
            controller.OpenJournal();
    }

    public void Refresh()
    {
        if (controller == null)
        {
            prompt.gameObject.SetActive(false);
            backdrop.SetActive(false);
            return;
        }
        HerbPanel page = controller.Panel;
        bool reading = page != HerbPanel.Closed;
        string hint = controller.Prompt;
        prompt.gameObject.SetActive(!reading && !string.IsNullOrEmpty(hint));
        if (prompt.gameObject.activeSelf) promptLabel.text = hint;
        backdrop.SetActive(reading);
        if (!reading) return;

        var study = controller.Study;
        bool canConfirm = study.CanConfirm && (page == HerbPanel.Book || page == HerbPanel.Journal || page == HerbPanel.Plant);
        bool canDeliver = page == HerbPanel.Book && study.Harvested && !study.Delivered;
        confirm.gameObject.SetActive(canConfirm || canDeliver);
        confirmLabel.text = canDeliver ? $"交药领 {study.SaleProceeds} 文   Enter" : "对照手记，确认掌握   Enter";
        bool inRetreat = page == HerbPanel.Meditating || page == HerbPanel.Visit;
        journal.gameObject.SetActive(!inRetreat);
        close.gameObject.SetActive(page != HerbPanel.Visit);
        closeLabel.text = page == HerbPanel.Meditating ? "收功  Esc" : "合上  Esc";
        journalLabel.text = page == HerbPanel.Journal ? "往来账  Tab" : page == HerbPanel.Ledger ? "草木录  Tab" : "翻看手记  J";
        bool practice = page == HerbPanel.Practice || inRetreat;
        pageName.text = practice ? "青溪药铺   /   静席" : "青溪药铺   /   " + (DemoRun.Active ? DemoRun.Name : "药铺学徒");
        practiceTrack.SetActive(practice && study.Delivered);
        if (practiceTrack.activeSelf)
            for (int i = 0; i < practiceMarks.Length; i++)
                practiceMarks[i].color = i < study.PracticeProgress
                    ? new Color32(68, 111, 83, 255) : new Color32(210, 201, 168, 255);
        bool examining = page == HerbPanel.Plant;
        specimenFrame.SetActive(examining);
        if (examining) specimenImage.sprite = specimenStages[study.Observations & 3];
        RectTransform textArea = leftText.rectTransform;
        textArea.anchoredPosition = new Vector2(34f, examining ? -347f : practiceTrack.activeSelf ? -238f : -203f);
        float lowerEdge = confirm.gameObject.activeSelf ? 424f : 488f;
        textArea.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, lowerEdge + textArea.anchoredPosition.y);
        foreach (Button action in actions) action.gameObject.SetActive(false);

        switch (page)
        {
            case HerbPanel.Book:
                leftTitle.text = HerbalStudy.BookTitle;
                leftSubtitle.text = "案上藏卷";
                leftText.text = HerbalStudy.BookText;
                ShowShop(study);
                break;
            case HerbPanel.Plant:
                leftTitle.text = "溪边的活株";
                leftSubtitle.text = study.Harvested ? "采前留图" : "现场观察";
                leftText.text = study.Harvested ? "剪过的茎旁还留着新叶。根须仍扎在石隙里，这一丛留待来年。" : FieldCaption(study.Observations);
                rightTitle.text = "亲手查验";
                rightText.text = study.Hypothesis >= 0 ? study.HypothesisNote : "你可以先记下眼前的迹象，再与药谱比对。";
                for (int i = 0; i < HerbalStudy.FieldActions.Length; i++)
                {
                    bool checkedAlready = (study.Observations & (1 << i)) != 0;
                    SetChoice(i, HerbalStudy.FieldActions[i] + (checkedAlready ? "  ·  已查" : ""), !checkedAlready);
                }
                if (study.Mastered) SetChoice(2, study.Harvested ? "已采一份，余株留根" : "留根采一份鲜药", !study.Harvested);
                footer.text = study.Mastered ? HarvestNote(study) : study.StatusNote;
                break;
            case HerbPanel.Journal:
                leftTitle.text = "随身手记";
                leftSubtitle.text = "记主 · " + (DemoRun.Active ? DemoRun.Name : "药铺学徒");
                leftText.text = (DemoRun.Wanderer ? "我游历至青溪，在药铺接了一桩辨药的差事。掌柜借我白布和竹签，让我带回一份亲验的鲜药。" :
                    HerbalStudy.Background) + "\n\n生境猜测\n" + study.HypothesisNote;
                rightTitle.text = "亲验笔录";
                rightText.text = study.EvidenceNote + "\n\n掌握情况\n" + study.StatusNote;
                footer.text = study.Mastered ? HarvestNote(study) : study.ReadBook ? "药谱摘记  ·  已翻阅" : "药谱摘记  ·  尚未翻阅";
                break;
            case HerbPanel.Trader:
                ShowTrader(study);
                break;
            case HerbPanel.Ledger:
                ShowLedger(study);
                break;
            case HerbPanel.Practice:
            case HerbPanel.Meditating:
            case HerbPanel.Visit:
                ShowPractice(page, study);
                break;
        }
        if (!string.IsNullOrEmpty(controller.Feedback)) footer.text = controller.Feedback;
        FitRightPage();
    }

    void ShowShop(HerbalStudy study)
    {
        if (study.Delivered)
        {
            rightTitle.text = "药铺收据";
            rightText.text = study.TradeNote;
            if (study.KeeperCall == KeeperCallState.Available)
            {
                rightTitle.text = "柜上留样";
                rightText.text = "掌柜已摆好药纸。趁叶上的水色未褪，把这一份鲜药夹好留作药样。";
                SetChoice(0, "帮工一时辰，领一文");
            }
            footer.text = study.FirstSense ? $"钱囊现有 {study.PurseCoins} 文。静席所记可翻往来账。" :
                "掌柜把《听息诀》借给你了。药铺东侧静席可以自修，东门外的散修也肯指点。";
            return;
        }
        if (study.Mastered)
        {
            rightTitle.text = "柜上收药";
            rightText.text = study.Harvested ? study.TradeNote : $"伙计认这一味青纹草，鲜药每份收 {HerbalStudy.HerbPrice} 文。只剪一份鲜叶，根和嫩芽留在山里。";
            footer.text = HarvestNote(study);
            return;
        }
        rightTitle.text = "我推测它生在……";
        rightText.text = study.Hypothesis >= 0 ? study.HypothesisNote : "从这段记载里，你觉得哪里值得去找？选一处，把猜测记入手记。";
        for (int i = 0; i < HerbalStudy.Habitats.Length; i++)
            SetChoice(i, HerbalStudy.Habitats[i] + (study.Hypothesis == i ? "  ·  已记" : ""));
        footer.text = "药谱可以重读，猜测可以改写。";
    }

    void ShowTrader(HerbalStudy study)
    {
        leftTitle.text = "背篓散修";
        leftSubtitle.text = "山路口述  ·  当面交涉";
        SetChoice(2, study.HasDirections || study.Mastered ? "告辞" : "先告辞，自己探路");
        if (study.Delivered)
        {
            leftSubtitle.text = "听息诀  ·  当面请教";
            rightTitle.text = "入门指点";
            leftText.text = "散修将背篓放在脚边，等一阵风过，才问你：「枝叶已经不响了。方才指间那点凉意，也跟着散了么？」";
            if (study.FirstSense)
                rightText.text = "听你说起初次辨得的灵气，散修点点头：「这一回，是你自己听出来的。」";
            else if (study.Taught)
                rightText.text = "散修先前教过你分辨那一缕气息。回药铺东侧静席，可照诀练习。";
            else
            {
                rightText.text = "「收你两文，我带你辨一次。下回坐静，就知道该留意什么了。」";
                SetChoice(0, $"请教听息诀，付 {HerbalStudy.LessonPrice} 文", study.PurseCoins >= HerbalStudy.LessonPrice);
            }
            footer.text = study.FirstSense ? $"已记初感灵气。钱囊 {study.PurseCoins} 文。" :
                study.Taught ? $"指点已经付清。每时辰进 {study.PracticeRate} 格。" :
                $"钱囊 {study.PurseCoins} 文。自修每时辰进 1 格，受指点后进 2 格。";
            return;
        }
        if (study.HasDirections || study.Mastered)
        {
            leftText.text = study.HasDirections ? study.RouteHint : study.Harvested ?
                "散修扶了扶背篓，看你已经采了药，便让开路。" : "「你既认过活株，也就记得来路。药铺只收鲜药，回去剪一份便是。」";
            rightTitle.text = "这趟的约定";
            rightText.text = study.TradeNote;
            footer.text = study.HasDirections ? "这段路讯已抄在随身手记的往来账里。" : HarvestNote(study);
            return;
        }
        leftText.text = "「山脚那几条岔道我走得熟。你若肯说自己猜药长在什么地方，我便说该怎么走。」\n\n「也可以收好手记。等你卖了这趟药，我们平分药钱。」";
        rightTitle.text = "拿什么换路讯？";
        bool canTell = study.Hypothesis >= 0;
        rightText.text = canTell ? $"可透露的猜测\n「{HerbalStudy.Habitats[study.Hypothesis]}」" :
            "手记里还没有生境猜测。你可以改谈分账，也可以等写下猜测再来。";
        SetChoice(0, "透露这一条猜测，换取路讯", canTell);
        SetChoice(1, "保留手记，约定平分药钱");
        footer.text = $"鲜药收价 {HerbalStudy.HerbPrice} 文。若约定平分，交药时两人各得 {HerbalStudy.SharedPayment} 文。";
    }

    void ShowLedger(HerbalStudy study)
    {
        leftTitle.text = "采药往来账";
        leftSubtitle.text = "青溪药铺  ·  收支与约定";
        leftText.text = study.TradeNote;
        rightTitle.text = "山路口述";
        rightText.text = study.HasDirections ? study.RouteHint : "这趟还没有向人换过路讯。";
        footer.text = $"柜上收价   鲜药一份 {HerbalStudy.HerbPrice} 文";
        if (!study.Delivered) return;

        if (study.Taught) leftText.text += $"\n听息诀指点　付 {HerbalStudy.LessonPrice} 文";
        if (study.KeeperCall == KeeperCallState.Paid) leftText.text += "\n留样帮工一时辰　收 1 文";
        leftText.text += $"\n\n钱囊现有　{study.PurseCoins} 文";
        rightText.text += "\n\n" + (study.FirstSense ? $"静修 {study.PracticeTicks} 时辰　凡人 · 初感灵气" :
            $"药铺东侧静席存有《听息诀》。感气 {study.PracticeProgress}/{HerbalStudy.PracticeGoal}，已修 {study.PracticeTicks} 时辰。");
        footer.text = study.KeeperCall == KeeperCallState.Available ? "掌柜在等你留样，须在下个修习时辰前赶到。" :
            study.KeeperCall == KeeperCallState.Missed ? "鲜叶已经晾起，留样的时机已过。" : "东门外的背篓散修熟悉听息诀。";
    }

    void ShowPractice(HerbPanel page, HerbalStudy study)
    {
        leftTitle.text = study.FirstSense ? "初感灵气" : "听息诀";
        if (!study.Delivered)
        {
            leftSubtitle.text = "药铺东侧  ·  静席";
            leftText.text = "蒲团边放着一卷系起的旧书，封上题着《听息诀》。";
            rightTitle.text = "掌柜的书卷";
            rightText.text = "等你把认过的鲜药带回铺里交账，掌柜就肯借出这卷入门功诀。";
            footer.text = "可先回药铺翻药谱，或出镇查找活株。";
            return;
        }
        leftSubtitle.text = $"感气 {study.PracticeProgress} / {HerbalStudy.PracticeGoal}　·　累计静修 {study.PracticeTicks} 时辰";
        string sensation = study.FirstSense ? "庭中无风，指间却有一缕清凉往来。凝神再试，那缕气息仍在。" :
            study.PracticeProgress == 0 ? "初坐时，耳边只有树叶和市声。书上说，待这些声音远去，再留意那一缕极淡的清意。" :
            study.PracticeProgress < 4 ? "市声渐远。偶尔能觉出一丝清凉，细辨时又消散了。" :
            "一缕清意在静坐时反复浮现。你已经能将它与穿过庭院的风分开。";
        leftText.text = sensation + (study.FirstSense ? "\n\n修行所记　凡人 · 初感灵气" :
            $"\n\n{(study.Taught ? "散修指点" : "依卷自修")}　每时辰进 {study.PracticeRate} 格");
        if (page == HerbPanel.Visit)
        {
            rightTitle.text = "掌柜遣人来唤";
            rightText.text = "「鲜叶还润，掌柜想请你到柜上留样。要忙一个时辰，给一文工钱。」";
            SetChoice(0, "现在收功，去药铺");
            SetChoice(1, controller.SessionRemaining > 0 ? "谢过来人，继续静修" : "婉拒帮工，结束本次静修");
            footer.text = "已经修得的进展会保留。留样须在下个修习时辰前赶到。";
            return;
        }
        if (page == HerbPanel.Meditating)
        {
            rightTitle.text = "正在静修";
            rightText.text = $"本次还余 {controller.SessionRemaining} 个时辰。每坐满一个时辰，便记下这段所得。";
            SetChoice(0, "提前收功");
            footer.text = "可随时收功，已坐满的时辰和感气进展都会保留。";
            return;
        }
        rightTitle.text = study.FirstSense ? "听息小成" : "安排这次静修";
        if (study.FirstSense)
        {
            rightText.text = "你将这一丝可以反复辨认的灵气记入手记，收起了入门的书卷。";
            footer.text = "静修所得已记入往来账。";
            return;
        }
        rightText.text = study.Taught ? "散修教过的法子，可以在这里练习。" :
            $"可依卷自修，也可去东门外花 {HerbalStudy.LessonPrice} 文请散修指点。";
        SetChoice(0, "坐一时辰");
        SetChoice(1, "坐三时辰");
        if (study.KeeperCall == KeeperCallState.Waiting)
            SetChoice(2, study.ListenForKeeper ? "掌柜来唤　听取（可切换）" : "掌柜来唤　谢绝（可切换）");
        footer.text = study.KeeperCall == KeeperCallState.Available ? "掌柜在等你留样，再坐满一个时辰便会错过。" :
            "时辰坐满才记所得。可随时收功。";
    }

    static string HarvestNote(HerbalStudy study)
    {
        if (study.Delivered) return $"鲜药已交，实收 {study.EarnedCoins} 文。";
        if (study.Harvested) return "药囊中有一份鲜药，可回青溪药铺柜上交货。";
        return "青纹草已辨明，可在活株旁留根采一份鲜药。";
    }

    static string FieldCaption(int observations)
    {
        switch (observations)
        {
            case 1: return "翻过的鲜叶仍连着茎，叶背的两条青纹清晰可辨。";
            case 2: return "苔已拨开，能看清湿润根土和石间水痕。";
            case 3: return "叶背双纹与根旁石隙都已查清，现场所见已记入手记。";
            default: return "草还生在土里，叶背和根际都藏着可以察验的细节。";
        }
    }

    void SetChoice(int index, string label, bool available = true)
    {
        actions[index].gameObject.SetActive(true);
        actions[index].interactable = available;
        actionLabels[index].text = (index + 1) + "   " + label;
    }

    void FitRightPage()
    {
        float bottom = -437f;
        foreach (Button action in actions)
        {
            if (!action.gameObject.activeSelf) continue;
            bottom = ((RectTransform)action.transform).anchoredPosition.y;
            break;
        }
        var area = rightText.rectTransform;
        area.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, area.anchoredPosition.y - bottom - 5f);
    }
}
