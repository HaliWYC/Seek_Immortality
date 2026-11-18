using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum HerbPanel { Closed, Book, Journal, Plant, Trader, Ledger, Practice, Meditating, Visit }

[DefaultExecutionOrder(-30)]
public sealed class HerbalStudyController : MonoBehaviour
{
    public const string SaveKey = "Qingxi.herbal-study.v1";
    string journalKey;
    const float SecondsPerShift = 1.8f;
    float sittingTime;
    QingxiWalker walker;
    QingxiSparring sparring;
    HerbalSite[] sites;
    HerbalSite nearby;
    HerbalSite openSite;
    bool ownsPause;

    public HerbalStudy Study { get; private set; }
    public HerbPanel Panel { get; private set; }
    public string Feedback { get; private set; } = "";
    public string Prompt { get; private set; } = "";
    public int SessionRemaining { get; private set; }
    public event Action Changed;

    void Awake()
    {
        journalKey = DemoRun.Active ? DemoRun.Key("herbal") : SaveKey;
        Study = HerbalStudy.Restore(PlayerPrefs.GetString(journalKey, ""));
    }

    void Start()
    {
        walker = GetComponent<QingxiWalker>();
        sparring = FindAnyObjectByType<QingxiSparring>();
        sites = FindObjectsByType<HerbalSite>();
        if (Study.Harvested)
            foreach (var site in sites) site.ShowHarvestedPlant();
        FindAnyObjectByType<HerbJournalHud>().Bind(this);
        UpdateNearby();
    }

    void Update()
    {
        if (DemoRun.Modal) return;
        if (Panel == HerbPanel.Closed) UpdateNearby();
        var keys = Keyboard.current;
        if (keys == null || (sparring != null && sparring.InMatch)) return;

        if (Panel == HerbPanel.Closed)
        {
            if (!walker.CanUseExplorationInput) return;
            if (keys.jKey.wasPressedThisFrame) OpenJournal();
            else if (keys.eKey.wasPressedThisFrame && nearby != null) Interact();
            return;
        }
        if (keys.escapeKey.wasPressedThisFrame) { ClosePanel(); return; }
        if (keys.jKey.wasPressedThisFrame)
        {
            if (Panel == HerbPanel.Journal || Panel == HerbPanel.Ledger) ClosePanel(); else OpenJournal();
            return;
        }
        if (keys.tabKey.wasPressedThisFrame) { ToggleLedger(); return; }

        int option = keys.digit1Key.wasPressedThisFrame || keys.numpad1Key.wasPressedThisFrame ? 0 :
            keys.digit2Key.wasPressedThisFrame || keys.numpad2Key.wasPressedThisFrame ? 1 :
            keys.digit3Key.wasPressedThisFrame || keys.numpad3Key.wasPressedThisFrame ? 2 : -1;
        if (option >= 0) Choose(option);
        else if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame) ConfirmStudy();
    }

    void UpdateNearby()
    {
        HerbalSite closest = null;
        float distance = float.PositiveInfinity;
        if (walker.CanUseExplorationInput && (sparring == null || !sparring.InMatch))
        {
            foreach (var site in sites)
            {
                if (!site || !site.isActiveAndEnabled || !site.InReach(walker.transform.position)) continue;
                float current = ((Vector2)(site.transform.position - walker.transform.position)).sqrMagnitude;
                if (current >= distance) continue;
                distance = current;
                closest = site;
            }
        }
        nearby = closest;
        string next = closest == null ? "" : closest.type switch
        {
            HerbalSiteType.Pharmacopoeia => Study.KeeperCall == KeeperCallState.Available ? "E  到柜上帮掌柜留样" :
                Study.Harvested && !Study.Delivered ? "E  到药铺柜上交药" : "E  翻阅柜上的药谱",
            HerbalSiteType.LivingPlant => Study.Mastered && !Study.Harvested ? "E  留根采一份鲜药" :
                Study.Mastered ? "E  再看青纹草" : "E  察看石边的活株",
            HerbalSiteType.WanderingHerbalist => "E  与采药散修攀谈",
            HerbalSiteType.PracticeMat => "E  察看药铺东侧静席",
            _ => ""
        };
        if (next == Prompt) return;
        Prompt = next;
        Changed?.Invoke();
    }

    public void Interact()
    {
        if (Panel != HerbPanel.Closed || !CanOpen()) return;
        UpdateNearby();
        if (nearby == null) return;
        openSite = nearby;
        switch (openSite.type)
        {
            case HerbalSiteType.Pharmacopoeia:
                if (Study.Read()) SaveProgress();
                SetPanel(HerbPanel.Book, "");
                break;
            case HerbalSiteType.LivingPlant:
                SetPanel(HerbPanel.Plant, "");
                break;
            case HerbalSiteType.WanderingHerbalist:
                SetPanel(HerbPanel.Trader, "");
                break;
            case HerbalSiteType.PracticeMat:
                SetPanel(HerbPanel.Practice, "");
                break;
        }
    }

    public void OpenJournal()
    {
        if (!CanOpen() || Panel == HerbPanel.Meditating || Panel == HerbPanel.Visit) return;
        SetPanel(HerbPanel.Journal, "");
    }

    public void ToggleLedger()
    {
        if (Panel == HerbPanel.Journal) SetPanel(HerbPanel.Ledger, "");
        else if (Panel == HerbPanel.Ledger) SetPanel(HerbPanel.Journal, "");
    }

    bool CanOpen() => walker != null && (ownsPause || walker.CanUseExplorationInput) && (sparring == null || !sparring.InMatch);

    void SetPanel(HerbPanel panel, string feedback)
    {
        if (!ownsPause)
        {
            walker.SetExplorationPaused(true);
            ownsPause = true;
        }
        Panel = panel;
        Feedback = feedback;
        Prompt = "";
        Changed?.Invoke();
    }

    public void ClosePanel()
    {
        if (!ownsPause) return;
        if (Panel == HerbPanel.Visit && Study.AnswerKeeper(true)) SaveProgress();
        SessionRemaining = 0;
        sittingTime = 0f;
        Panel = HerbPanel.Closed;
        openSite = null;
        Feedback = "";
        ownsPause = false;
        if (walker != null) walker.SetExplorationPaused(false);
        Changed?.Invoke();
    }

    public void NoteHypothesis(int choice)
    {
        if (Panel != HerbPanel.Book || !AtSite(HerbalSiteType.Pharmacopoeia) || Study.Mastered ||
            choice < 0 || choice >= HerbalStudy.Habitats.Length) return;
        if (Study.RecordHypothesis(choice)) SaveProgress();
        SetPanel(HerbPanel.Journal, "猜测已记下。");
    }

    bool AtSite(HerbalSiteType type) => openSite && openSite.isActiveAndEnabled && openSite.type == type &&
        openSite.InReach(walker.transform.position);

    public void Choose(int index)
    {
        switch (Panel)
        {
            case HerbPanel.Book:
                if (index == 0 && AtSite(HerbalSiteType.Pharmacopoeia) && Study.DrySample())
                {
                    SaveProgress();
                    SetPanel(HerbPanel.Ledger, "你花了一个时辰帮忙留样，掌柜付给你一文。");
                }
                else NoteHypothesis(index);
                break;
            case HerbPanel.Plant:
                if (index == 2) Harvest(); else InspectPlant(index);
                break;
            case HerbPanel.Trader:
                Negotiate(index);
                break;
            case HerbPanel.Practice:
                PrepareSitting(index);
                break;
            case HerbPanel.Meditating:
                if (index == 0) ClosePanel();
                break;
            case HerbPanel.Visit:
                if (index == 0) ClosePanel();
                else if (index == 1 && Study.AnswerKeeper(false))
                {
                    SaveProgress();
                    SetPanel(SessionRemaining > 0 ? HerbPanel.Meditating : HerbPanel.Practice, "你谢过掌柜，记下了方才的感受。");
                }
                break;
        }
    }

    void PrepareSitting(int choice)
    {
        if (!AtSite(HerbalSiteType.PracticeMat) || !Study.Delivered || Study.FirstSense) return;
        if (choice == 2)
        {
            if (!Study.ToggleKeeperMessages()) return;
            SaveProgress();
            SetPanel(HerbPanel.Practice, Study.ListenForKeeper ? "已嘱咐伙计，掌柜有事便来唤你。" : "已嘱咐伙计，这一坐不必来唤。");
            return;
        }
        if (choice != 0 && choice != 1) return;
        int untilGoal = Mathf.CeilToInt((HerbalStudy.PracticeGoal - Study.PracticeProgress) / (float)Study.PracticeRate);
        SessionRemaining = Mathf.Min(choice == 0 ? 1 : 3, untilGoal);
        sittingTime = 0f;
        SetPanel(HerbPanel.Meditating, "");
    }

    void LateUpdate()
    {
        if (Panel != HerbPanel.Meditating) return;
        sittingTime += Mathf.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime);
        if (sittingTime < SecondsPerShift) return;
        sittingTime -= SecondsPerShift;
        if (!Study.PracticeOneShift()) { ClosePanel(); return; }
        SessionRemaining--;
        SaveProgress();
        if (Study.KeeperCall == KeeperCallState.Calling)
        {
            sittingTime = 0f;
            SetPanel(HerbPanel.Visit, "");
        }
        else if (SessionRemaining == 0)
            SetPanel(HerbPanel.Practice, Study.FirstSense ? "一缕灵气应念而动。你第一次认清了它。" : "这一坐已毕，所得已经记下。");
        else SetPanel(HerbPanel.Meditating, "");
    }

    void Negotiate(int choice)
    {
        if (choice == 2) { ClosePanel(); return; }
        if (!AtSite(HerbalSiteType.WanderingHerbalist)) return;
        if (Study.Delivered)
        {
            if (choice != 0 || !Study.TakeLesson()) return;
            SaveProgress();
            SetPanel(HerbPanel.Trader, $"散修演示了一遍听息诀。指点费 {HerbalStudy.LessonPrice} 文已入账。");
            return;
        }
        bool agreed = choice switch
        {
            0 => Study.TellHypothesis(),
            1 => Study.PromiseShare(),
            _ => false
        };
        if (!agreed) return;
        SaveProgress();
        SetPanel(HerbPanel.Trader, "已把路讯和谈妥的条件记入往来账。");
    }

    void Harvest()
    {
        if (!AtSite(HerbalSiteType.LivingPlant) || !Study.Harvest()) return;
        openSite.ShowHarvestedPlant();
        SaveProgress();
        SetPanel(HerbPanel.Plant, "剪取一份鲜药，留下根株。回青溪药铺交给掌柜。");
    }

    public void InspectPlant(int choice)
    {
        if (Panel != HerbPanel.Plant || !AtSite(HerbalSiteType.LivingPlant) || choice < 0 || choice >= HerbalStudy.FieldActions.Length) return;
        bool fresh = Study.Observe(choice);
        if (fresh) SaveProgress();
        Feedback = fresh ? HerbalStudy.Findings[choice] : "这项观察已经写入手记。";
        Changed?.Invoke();
    }

    public void ConfirmStudy()
    {
        if (!ownsPause) return;
        if (Panel == HerbPanel.Book && AtSite(HerbalSiteType.Pharmacopoeia) && Study.Deliver())
        {
            SaveProgress();
            SetPanel(HerbPanel.Ledger, $"鲜药实收 {Study.EarnedCoins} 文。掌柜准你去药铺东侧静席借读《听息诀》。");
            return;
        }
        if (Panel != HerbPanel.Journal && Panel != HerbPanel.Plant && Panel != HerbPanel.Book) return;
        if (!Study.Confirm()) return;
        SaveProgress();
        SetPanel(HerbPanel.Journal, "识得青纹草。合上手记后，可在活株旁留根采药。");
    }

    void SaveProgress()
    {
        PlayerPrefs.SetString(journalKey, JsonUtility.ToJson(Study));
        DemoRun.RecordHerbs(journalKey, Study);
        PlayerPrefs.Save();
    }

    void OnDisable() => ClosePanel();
}
