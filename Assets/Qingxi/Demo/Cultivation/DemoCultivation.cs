using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed partial class DemoCultivation
{
    public static int Bottleneck => CultivationRealms.Table.Get(RealmMajor.QiRefining, RealmPhase.Complete).MinimumQi;
    public const int FoundationGoal = 12;
    public const int MaxTurns = 7;
    public static int PillPrice => FirstVolumeItems.Find(FirstVolumeItems.FoundationPill).Price;
    public static int DewPrice => FirstVolumeItems.Find(FirstVolumeItems.MeridianDew).Price;
    public const int ArtPrice = 5;
    public const int LastMortalDay = 64 * 360;
    public static readonly string[] Elements = SpiritRoots.ElementNames;

    [SerializeField] bool manual, hired, accepted, fulfilled, prizedPicked;
    [SerializeField] int meetDay, qi, stones, days, herbs, pills, injuries, quality, attempts;
    [SerializeField] double travelMinutes;
    [SerializeField] bool dew, art, foundation;
    [SerializeField] CultivatorBag inventory;
    [SerializeField] int inventoryVersion;
    [SerializeField] bool copiedRoll;
    [SerializeField] int rollDay;
    [SerializeField] int retreatLeft;
    [SerializeField] bool receiveLetters = true, letterSeen, readingLetter;
    [SerializeField] int turn, force, firstFormed, activeInjuries, usedPills;
    [SerializeField] bool single, converted, usedDew;
    [SerializeField] int[] pool = new int[5];
    [SerializeField] string note = "";
    [SerializeField] List<string> knownArts;

    // Old saves recorded just one manual and one technique. Import them by ID on first use.
    List<string> KnownArts
    {
        get
        {
            if (knownArts == null) knownArts = new List<string>();
            if (manual && !knownArts.Contains(QingxiArts.GuiyuanId)) knownArts.Add(QingxiArts.GuiyuanId);
            if (art && !knownArts.Contains(QingxiArts.ReturnFlowId)) knownArts.Add(QingxiArts.ReturnFlowId);
            manual = false;
            art = false;
            return knownArts;
        }
    }

    public bool KnowsArt(string id) => KnownArts.Contains(id);
    public bool Manual => KnowsArt(QingxiArts.GuiyuanId);
    public bool KnowsFirebolt => KnowsArt(QingxiArts.FireboltId);
    public bool Foundation => foundation;
    public bool CanFly => Attributes(true).CanFly;
    public bool Expired => !foundation && days >= LastMortalDay;
    public int Qi => qi;
    public int Stones => Bag.Count(FirstVolumeItems.SpiritStone);
    public int Days => days;
    public int Injuries => injuries;
    public int Herbs => Bag.Count(FirstVolumeItems.JadeHerb);
    public int Pills => Bag.Count(FirstVolumeItems.FoundationPill);
    public int Quality => quality;
    public int Attempts => attempts;
    public bool Dew => Bag.Count(FirstVolumeItems.MeridianDew) > 0;
    public bool Art => KnowsArt(QingxiArts.ReturnFlowId);
    public bool Pact => accepted;
    public bool Fulfilled => fulfilled;
    public bool CopiedRoll => copiedRoll;
    public int RollDay => rollDay;
    public int PactDays => Mathf.Max(0, 180 - (days - meetDay));
    public bool CanAcceptPact => Manual && !accepted && PactDays > 0 && !Expired;
    public bool CanDeliverPact => accepted && !fulfilled && PactDays > 0 && Herbs > 0 && !Expired;
    public int RetreatLeft => retreatLeft;
    public bool ReceiveLetters => receiveLetters;
    public bool ReadingLetter => readingLetter;
    public bool InTrial => turn > 0;
    public int Turn => turn;
    public int Force => force;
    public int FormedOn => firstFormed;
    public int TrialInjuries => activeInjuries;
    public int UsedPills => usedPills;
    public bool UsedDew => usedDew;
    public bool Converted => converted;
    public string Note => note ?? "";
    public int Age => 16 + days / 360;
    public string Realm => Attributes(true).Realm.Name;
    public CultivatorAttributes Attributes(bool sensed)
    {
        var bag = Bag;
        return new CultivatorAttributes(CultivationRealms.Table.Resolve(sensed, qi, foundation), quality,
            bag.Equipped(CultivationGearSlot.Weapon), bag.Equipped(CultivationGearSlot.Clothing),
            bag.Equipped(CultivationGearSlot.Accessory));
    }
    public string QualityName => quality >= 3 ? "澄明道基" : quality == 2 ? "敦厚道基" : "初成道基";
    public int Spirit(int element) => element >= 0 && element < 5 && pool != null && pool.Length == 5 ? pool[element] : 0;
    public bool Free => !Expired && !InTrial && retreatLeft == 0 && !readingLetter;

    public CultivatorBag Bag
    {
        get
        {
            if (inventoryVersion == 0)
            {
                inventory = CultivatorBag.FromFirstVolume(stones, herbs, pills, dew);
                inventoryVersion = 1;
                stones = herbs = pills = 0;
                dew = false;
            }
            return inventory ?? (inventory = new CultivatorBag());
        }
    }

    public void Earn(int amount)
    {
        if (amount > 0) Bag.TryAdd(FirstVolumeItems.SpiritStone, Math.Min(amount, int.MaxValue - Stones));
    }

    public bool Spend(int amount) => Bag.TryRemove(FirstVolumeItems.SpiritStone, amount);

    bool ReceiveItem(string payment, int cost, string item, int amount = 1)
    {
        if (Bag.TryExchange(payment, cost, item, amount)) return true;
        note = "行囊余格不足。整理妥当再来，旧约与手中物资都还在。";
        return false;
    }

    public void AddTravelMinutes(double minutes)
    {
        if (minutes <= 0 || double.IsNaN(minutes) || double.IsInfinity(minutes) || Expired) return;
        travelMinutes += minutes;
        int whole = (int)Math.Min(1000000, Math.Floor(travelMinutes / 1440));
        travelMinutes -= whole * 1440.0;
        Advance(whole);
    }

    void Advance(int amount)
    {
        days = (int)Math.Min(foundation ? 1000000L : LastMortalDay, (long)days + amount);
        if (Expired) { retreatLeft = 0; readingLetter = false; turn = 0; }
    }

    public bool AcceptManual()
    {
        if (!Free || Manual) return false;
        KnownArts.Add(QingxiArts.GuiyuanId);
        meetDay = days;
        note = "栖霞宗知客借你《归元篇》。同来求法的姜遥约你一同留意隘口的玉髓芝。";
        return true;
    }

    public bool TakePact()
    {
        if (!Free || !CanAcceptPact) return false;
        accepted = true;
        note = "姜遥许下丹药一枚、灵石二块为酬。约定记入山友页。";
        return true;
    }

    public bool CopySectRoll()
    {
        if (!Free || !Manual || (copiedRoll && rollDay == days)) return false;
        copiedRoll = true; rollDay = days;
        note = $"你抄下了第 {days} 日的外门值事簿，两名弟子的近况已落笔。";
        return true;
    }

    // Only facts knowable on the day the player last read the sect rolls are revealed.
    public string RollEntry(bool first)
    {
        if (!copiedRoll) return "";
        int interval = first ? 360 : 540;
        int attemptsAtSnapshot = rollDay / interval;
        int pillsAtHand = first ? 2 : 1;
        int odds = (first ? 36 : 14) + 9 * Mathf.Min(12, pillsAtHand);
        for (int attempt = 1; attempt <= attemptsAtSnapshot; attempt++)
        {
            uint token = (uint)(attempt * 7321 + (first ? 241 : 581));
            token ^= token >> 16; token *= 2246822519u; token ^= token >> 13;
            if (token % 100 < odds)
                return $"{(first ? "许禾" : "陆青")}　筑基初期，第 {attempt * interval} 日成基，近日在山门讲习。";
        }
        string level = attemptsAtSnapshot > 0 || rollDay >= interval / 2 ? "炼气后期" : "炼气中期";
        string action = (rollDay / 90 + (first ? 0 : 1)) % 3 == 0 ? "在隘口寻药" :
            (rollDay / 90 + (first ? 0 : 1)) % 3 == 1 ? "留守丹房" : "在门内闭关";
        return $"{(first ? "许禾" : "陆青")}　{level}，{action}。";
    }

    public bool DeliverPact()
    {
        if (!Free || !CanDeliverPact) return false;
        if (Stones > int.MaxValue - 2)
        {
            note = "钱袋已满，腾出两块灵石的空位再来领酬。";
            return false;
        }
        if (!ReceiveItem(FirstVolumeItems.JadeHerb, 1, FirstVolumeItems.FoundationPill)) return false;
        fulfilled = true; Earn(2);
        note = "你如约交芝。姜遥分给你一枚筑基丹和两块灵石，将你的名字留在自己的寻药册里。";
        return true;
    }

    public string FellowNote
    {
        get
        {
            if (!Manual) return "栖霞山门有知客接待来访修士。";
            if (fulfilled) return "姜遥已收到玉髓芝。她在山门为你留下配丹心得，往后买归流术只收三块灵石。";
            if (PactDays == 0) return accepted ? "约期已过。姜遥托人另寻药材，山门柜上留着她的结算单。" : "姜遥自行寻药后离开山门，收药的旧帖已撤下。";
            int elapsed = days - meetDay;
            string where = elapsed < 60 ? "姜遥在山门凑盘缠，等同道捎来玉髓芝。" :
                elapsed < 90 ? "姜遥已经动身进隘，计划在相识满九十日时采下头一批芝。" :
                prizedPicked ? "姜遥进隘时头茬已被你采走，仍愿意照约换药。" : "姜遥已先取头茬玉髓芝，药圃边还留有零散的小株。";
            return where + (accepted ? $" 距交药之约尚有 {PactDays} 日。" : $" 招人寻药的帖子还留 {PactDays} 日。");
        }
    }

    public bool Gather()
    {
        if (!Free || !Manual) return false;
        bool first = !prizedPicked && days - meetDay + 15 < 90;
        if (!ReceiveItem(null, 0, FirstVolumeItems.JadeHerb, first ? 2 : 1)) return false;
        if (first) prizedPicked = true;
        Advance(15);
        note = first ? "你先于姜遥赶到芝圃，用十五日采到头茬玉髓芝两株。" : "你在石壁间守了十五日，采得玉髓芝一株。";
        return true;
    }

    public bool CollectDew()
    {
        if (!Free || !Manual || Dew || injuries == 0) return false;
        if (!ReceiveItem(null, 0, FirstVolumeItems.MeridianDew)) return false;
        Advance(30);
        note = "你候满三十个清晨，集成一瓶护脉露。下次冲关可选用。";
        return true;
    }

    public bool Work()
    {
        if (!Free || foundation) return false;
        Advance(30); Earn(3); hired = true;
        note = "你替药房照看了一月丹炉，收得三块灵石。";
        return true;
    }

    public bool BuyPill()
    {
        if (!Free || !Manual || Stones < PillPrice) return false;
        if (!ReceiveItem(FirstVolumeItems.SpiritStone, PillPrice, FirstVolumeItems.FoundationPill)) return false;
        note = "柜上取出一枚筑基丹。服丹之时由你自定。"; return true;
    }

    public bool MakePill()
    {
        if (!Free || !Manual || Herbs < 2) return false;
        if (!ReceiveItem(FirstVolumeItems.JadeHerb, 2, FirstVolumeItems.FoundationPill)) return false;
        Advance(10); note = "用两株玉髓芝合炉十日，取出一枚筑基丹。"; return true;
    }

    public int CurrentArtPrice => fulfilled ? 3 : ArtPrice;
    public bool LearnArt()
    {
        if (!Free || !Manual || Art || !Spend(CurrentArtPrice)) return false;
        KnownArts.Add(QingxiArts.ReturnFlowId);
        note = $"你习得{QingxiArts.ReturnFlow.Name}。{QingxiArts.ReturnFlow.Description}";
        return true;
    }

    public bool CanLearnFirebolt(bool inSect) => CanStudyAtGate(QingxiArts.Firebolt, inSect);

    public bool LearnFirebolt(bool inSect) => StudyAtGate(QingxiArts.Firebolt, inSect);

    public bool SeekDoctor()
    {
        int price = DewPrice;
        if (!Free || injuries == 0 || Dew || Stones < price) return false;
        if (!ReceiveItem(FirstVolumeItems.SpiritStone, price, FirstVolumeItems.MeridianDew)) return false;
        note = "医者交你一瓶护脉露，冲关前服下，可暂时镇住旧伤。"; return true;
    }

    public bool StartRetreat(int seasons, bool listen)
    {
        if (!Free || !Manual || foundation || (seasons != 1 && seasons != 3)) return false;
        var book = MainManual;
        int limit = Mathf.Min(Bottleneck, book.MaximumQi);
        if (qi >= limit) return false;
        retreatLeft = Mathf.Min(seasons, (limit - qi + book.QiPerSeason - 1) / book.QiPerSeason);
        receiveLetters = listen; note = "你收拾起居，自行排定了出关时日。"; return true;
    }

    public bool TrainSeason()
    {
        if (retreatLeft <= 0 || readingLetter || Expired) return false;
        retreatLeft--; Advance(90);
        if (Expired) return true;
        var book = MainManual;
        qi = Mathf.Min(Bottleneck, book.MaximumQi, qi + book.QiPerSeason);
        note = qi == Bottleneck ? "一季周天走完，气海已满。你可以主动准备冲关。" : $"这一季收功，已至{Realm}。";
        if (receiveLetters && !fulfilled && !letterSeen && days - meetDay >= 60 && PactDays > 0)
        {
            readingLetter = true; letterSeen = true;
        }
        return true;
    }

    public void AnswerLetter(bool leave)
    {
        if (!readingLetter) return;
        readingLetter = false;
        if (leave) EndRetreat();
    }

    public void EndRetreat()
    {
        if (retreatLeft == 0 && !readingLetter) return;
        retreatLeft = 0; readingLetter = false; note = "你推门出关。已经修成的周天仍在气海。";
    }

    public bool StartTrial(bool singleRoot, int dose, bool suppress)
    {
        if (!Free || !Manual || foundation || qi < Bottleneck || dose < 0 || dose > Mathf.Min(Pills, 3) ||
            (suppress && (!Dew || injuries == 0))) return false;
        if (pool == null || pool.Length != 5) pool = new int[5];
        Array.Clear(pool, 0, pool.Length); single = singleRoot; usedPills = dose; usedDew = suppress;
        Bag.TryRemove(FirstVolumeItems.FoundationPill, dose);
        if (suppress) Bag.TryRemove(FirstVolumeItems.MeridianDew, 1);
        activeInjuries = suppress ? 0 : injuries; attempts++;
        firstFormed = 0; force = 0; turn = 1; pool[0] = 2 * dose;
        Intake(); note = "气海启关。丹力已化，守满七转方可落定。"; return true;
    }

    void Intake()
    {
        converted = false;
        var roots = SpiritRoots.For(single);
        int count = Mathf.Max(1, 4 - activeInjuries);
        for (int i = 0; i < count; i++)
            pool[(int)roots.IntakeElement(turn, i)]++;
    }

    public bool ConvertTo(int element)
    {
        if (!InTrial || !Art || converted || element < 0 || element >= 5) return false;
        var spell = QingxiArts.ReturnFlow;
        if (spell.Effect != QingxiSpellEffect.ConvertSpirit || spell.Target != FaShuTarget.Self) return false;
        int amount = spell.ConversionAmount;
        int available = 0;
        for (int i = 0; i < 5; i++) if (i != element) available += pool[i];
        if (amount <= 0 || available < amount) return false;
        for (int i = 1, moved = 0; i <= 4 && moved < amount; i++)
        {
            int donor = (element + i) % 5;
            int use = Mathf.Min(amount - moved, pool[donor]); pool[donor] -= use; pool[element] += use; moved += use;
        }
        converted = true; note = $"{spell.Name}导入 {amount} 缕{Elements[element]}气。这一转还可凝基。"; return true;
    }

    public bool Condense(int element, bool deep)
    {
        int cost = deep ? 5 : 3;
        if (!InTrial || element < 0 || element >= 5 || pool[element] < cost) return false;
        pool[element] -= cost; force += deep ? 4 : 2;
        if (firstFormed == 0 && force >= FoundationGoal) firstFormed = turn;
        FinishTurn(); return true;
    }

    public bool Hold()
    {
        if (!InTrial) return false;
        FinishTurn(); return true;
    }

    void FinishTurn()
    {
        bool last = turn == MaxTurns;
        Advance(1);
        if (Expired) return;
        if (!last) { turn++; Intake(); note = "气机再转，余下的五行灵力一并留用。"; return; }
        if (force < FoundationGoal) { Fail(); return; }
        turn = 0; foundation = true; injuries = 0; quality = force >= 20 ? 3 : force >= 16 ? 2 : 1;
        note = $"七转功成。第 {firstFormed} 转凝就道基，最终根基 {force}，成就{QualityName}。";
    }

    public bool AbortTrial()
    {
        if (!InTrial) return false;
        Advance(1); if (!Expired) Fail(); return true;
    }

    void Fail()
    {
        turn = 0; qi = Mathf.Max(0, qi - 3); injuries++;
        note = $"气海翻涌，修为退去一季，留下第 {injuries} 层旧伤。服下的药已耗尽，可重修再来。";
    }

    public string OccupationNote => hired ? "你在药房做过炉工。" : "镇上药房和宗门丹房都缺照看炉火的人。";
}
