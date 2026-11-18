using System;
using UnityEditor;
using UnityEngine;

public static class DemoCultivationChecks
{
    [MenuItem("行旅/验证首卷修炼与筑基")]
    public static void Run()
    {
        var single = Bottleneck();
        Claim(single.StartTrial(true, 0, false) && single.Spirit(0) == 4, "天灵根同气入体");
        PlayToEnd(single);
        Claim(single.Foundation && single.FormedOn == 4 && single.Quality == 3, "不服丹的木灵根第四转凝基");

        var mixed = Bottleneck();
        mixed.Earn(13);
        Claim(mixed.LearnArt() && mixed.BuyPill() && mixed.BuyPill(), "散修可自行准备归流术与筑基丹");
        Claim(mixed.StartTrial(false, 2, false) && mixed.Pills == 0 && mixed.Spirit(1) == 1, "混灵根的气池与入关用药");
        Claim(mixed.ConvertTo(0) && !mixed.ConvertTo(0), "每转至多归流一次");
        var halfway = JsonUtility.FromJson<DemoCultivation>(JsonUtility.ToJson(mixed));
        Claim(halfway.Turn == 1 && halfway.Converted && halfway.InTrial && halfway.Pills == 0, "未完七转恢复原药量和行动");
        PlayToEnd(halfway);
        Claim(halfway.Foundation && halfway.FormedOn == 3 && halfway.Quality == 3, "准备充分的混灵根可更早凝基");

        var weak = Bottleneck();
        Claim(weak.StartTrial(false, 0, false), "可自行冒险启关");
        while (weak.InTrial) weak.Hold();
        Claim(weak.Injuries == 1 && weak.Qi == 9 && !weak.Foundation, "蓄气而不凝基会失败且境界回退");
        Train(weak, 1);
        Claim(weak.StartTrial(true, 0, false) && weak.Spirit(0) == 3 && weak.AbortTrial(), "再冲关时少一缕，强退再添伤");
        Claim(weak.Injuries == 2 && weak.CollectDew(), "带伤可在山中候护脉露");
        Train(weak, 1);
        Claim(weak.StartTrial(true, 0, true) && weak.Spirit(0) == 4 && !weak.Dew && weak.Injuries == 2, "护脉露暂压原伤");
        weak.AbortTrial();
        Claim(weak.Injuries == 3 && weak.CollectDew(), "失败仍按原伤层数累积");
        Train(weak, 1);
        weak.StartTrial(true, 0, true);
        PlayToEnd(weak);
        Claim(weak.Foundation && weak.Injuries == 0, "成基清去冲关旧伤");

        var friend = new DemoCultivation();
        friend.AcceptManual(); friend.TakePact();
        Claim(friend.Gather() && friend.Herbs == 2 && friend.DeliverPact() && !friend.DeliverPact(), "早入山可抢得头茬，约定仅交付一次");
        Claim(friend.Pills == 1 && friend.Stones == 2 && friend.CurrentArtPrice == 3, "守信能得到实际报偿");
        var late = new DemoCultivation();
        late.AcceptManual(); late.TakePact(); late.StartRetreat(3, true); late.TrainSeason();
        Claim(late.ReadingLetter && !late.TrainSeason(), "来信停在已结算的一季");
        late.AnswerLetter(true);
        Claim(late.Qi == 3 && late.RetreatLeft == 0 && late.Gather() && late.Herbs == 1, "出关已有修为保留，而姜遥已先取头茬");
        late.AddTravelMinutes(100 * 1440);
        Claim(!late.CanDeliverPact && !late.DeliverPact(), "路上经过的时日计入约期");
        Claim(late.CopySectRoll(), "可在山门抄录外门名簿");
        string snapshot = late.RollEntry(true);
        late.AddTravelMinutes(1440 * 720);
        Claim(late.RollEntry(true) == snapshot && late.CopySectRoll() && !late.CopySectRoll(), "旧抄本保留原来的事实，同日无法刷新抽签");
        var clock = new DemoCultivation();
        clock.AddTravelMinutes(1439.5); clock.AddTravelMinutes(.5);
        Claim(clock.Days == 1, "不足一日的行旅可累计");
        clock.AddTravelMinutes((double)DemoCultivation.LastMortalDay * 1440);
        Claim(clock.Expired && !clock.Work(), "寿尽后行动停止");
        Debug.Log("青岚首卷验证通过：两种灵根、七转凝基、强退旧伤、临时护脉、山友行迹、未完存档与寿元。");
    }

    static DemoCultivation Bottleneck()
    {
        var s = new DemoCultivation();
        Claim(!s.StartTrial(true, 0, false) && s.AcceptManual(), "修为未足不能启关");
        Train(s,3); Train(s,1);
        Claim(s.Qi == DemoCultivation.Bottleneck && s.CanFly, "正常修炼可达瓶颈并解锁御剑");
        return s;
    }

    static void Train(DemoCultivation s, int seasons)
    {
        Claim(s.StartRetreat(seasons, false), "可在静处入定");
        while (s.RetreatLeft > 0) Claim(s.TrainSeason(), "一季周天可结算");
    }

    static void PlayToEnd(DemoCultivation s)
    {
        while (s.InTrial)
        {
            int element = 0;
            for (int i = 1; i < 5; i++) if (s.Spirit(i) > s.Spirit(element)) element = i;
            s.ConvertTo(element);
            if (s.Spirit(element) < 3) s.Hold();
            else Claim(s.Condense(element, s.Spirit(element) >= 5), "凝基动作按同气结算");
        }
    }

    static void Claim(bool ok, string text)
    {
        if (!ok) throw new InvalidOperationException(text);
    }
}
