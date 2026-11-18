using System;
using UnityEditor;
using UnityEngine;

public static class HerbalStudyChecks
{
    [MenuItem("行旅/验证药铺学徒手记")]
    public static void Run()
    {
        var notes = new HerbalStudy();
        Check(!notes.RecordHypothesis(0) && !notes.Confirm(), "空白手记不能凭空推断。");
        notes.Read();
        notes.RecordHypothesis(0);
        Check(notes.RecordHypothesis(2) && notes.Hypothesis == 2 && !notes.Confirm(), "猜测可改，不等于亲验。");
        Check(notes.Observe(0) && !notes.Observe(0) && !notes.Confirm(), "重复看叶不能代替查根。");
        var restored = HerbalStudy.Restore(JsonUtility.ToJson(notes));
        Check(restored.Hypothesis == 2 && restored.Observations == 1 && !restored.Mastered, "部分观察可恢复。");
        restored.Observe(1);
        Check(restored.CanConfirm && !restored.Mastered, "证据齐全仍需落笔确认。");
        Check(restored.Confirm() && !restored.RecordHypothesis(1) && restored.Hypothesis == 2,
            "鉴别完成后应保留原来猜错的记录。");
        Check(HerbalStudy.Restore(JsonUtility.ToJson(restored)).Mastered, "已确认的手记可恢复。");

        var fieldFirst = new HerbalStudy();
        fieldFirst.Observe(1);
        fieldFirst.Observe(0);
        Check(!fieldFirst.Confirm(), "未对照药谱时只记所见。");
        fieldFirst.Read();
        Check(!fieldFirst.Mastered && fieldFirst.Confirm(), "先走山路也能回铺对照定名。");
        CheckNegotiationAndHarvest();
        CheckFirstPractice();
        Debug.Log("药铺学徒手记验证通过。");
    }

    static void CheckNegotiationAndHarvest()
    {
        var shared = new HerbalStudy();
        Check(!shared.HasDirections && shared.RouteHint == "" && !shared.TellHypothesis() &&
            !shared.Harvest() && !shared.Deliver(), "没有线索或药材时，不能透露猜测、采药或交货。");
        Check(shared.PromiseShare() && !shared.PromiseShare() && shared.HasDirections &&
            shared.RouteHint.Length > 0 && !shared.ReadBook && shared.Observations == 0 && !shared.Mastered,
            "尚未读书也能用分账换路，听路讯不算查药。");
        shared.Read();
        shared.RecordHypothesis(1);
        Check(!shared.TellHypothesis(), "已答应分账，不能再拿线索改约。");
        shared.Observe(1);
        shared.Observe(0);
        Check(!shared.Harvest() && shared.Confirm() && shared.Harvest() &&
            !shared.Harvest() && shared.EarnedCoins == 0, "辨认并确认后可采一份，采药时尚未收到货款。");
        var carrying = HerbalStudy.Restore(JsonUtility.ToJson(shared));
        Check(carrying.PromisedShare && carrying.Harvested && !carrying.Delivered && !carrying.Harvest(),
            "重读手记能恢复药囊和分账约定。");
        Check(carrying.Deliver() && carrying.EarnedCoins == 3 && !carrying.Deliver(), "按约结算，不能重复领钱。");
        var receipt = HerbalStudy.Restore(JsonUtility.ToJson(carrying));
        Check(receipt.Delivered && !receipt.Deliver() && receipt.EarnedCoins == 3, "交货收据在读档后仍有效。");

        var disclosed = HerbalStudy.Restore("{\"readBook\":true,\"hypothesis\":0,\"observations\":1}");
        Check(disclosed.Hypothesis == 0 && disclosed.Observations == 1 && !disclosed.HasDirections &&
            !disclosed.Mastered && !disclosed.Harvested, "旧版部分手记应原样延续。");
        Check(disclosed.TellHypothesis() && disclosed.ToldHypothesis && !disclosed.PromisedShare &&
            !disclosed.PromiseShare() && !disclosed.TellHypothesis() && !disclosed.Harvest(),
            "未证实甚至猜错的线索也能换路，采药仍需亲验。");
        disclosed.RecordHypothesis(2);
        disclosed = HerbalStudy.Restore(JsonUtility.ToJson(disclosed));
        Check(disclosed.Hypothesis == 2 && disclosed.TradeNote.Contains(HerbalStudy.Habitats[0]),
            "修改手记和读档均不能改变曾经透露的猜测。");
        disclosed.Observe(1);
        Check(disclosed.Confirm() && disclosed.Harvest() && disclosed.Deliver() && disclosed.EarnedCoins == 6,
            "只透露线索并未许诺分账，交货实收六文。");

        var independent = HerbalStudy.Restore("{\"readBook\":true,\"hypothesis\":1,\"observations\":3,\"mastered\":true}");
        Check(independent.Mastered && !independent.HasDirections && !independent.TellHypothesis() && !independent.PromiseShare() &&
            !independent.Deliver() && independent.Harvest() && !independent.PromiseShare() &&
            independent.Deliver() && independent.EarnedCoins == 6, "旧版已掌握者无需再验证，独立采药可收全款。");
    }

    [MenuItem("行旅/重置药铺学徒手记", true)]
    static bool CanReset() => !EditorApplication.isPlayingOrWillChangePlaymode &&
        PlayerPrefs.HasKey(HerbalStudyController.SaveKey);

    [MenuItem("行旅/重置药铺学徒手记")]
    static void ResetJournal()
    {
        if (!EditorUtility.DisplayDialog("重置药铺学徒手记", "清空该学徒的辨药手记、收支约定、静修进展和留样记录，从头试走？", "清空手记", "保留"))
            return;
        PlayerPrefs.DeleteKey(HerbalStudyController.SaveKey);
        PlayerPrefs.Save();
    }

    static void CheckFirstPractice()
    {
        const string sale = "{\"readBook\":true,\"observations\":3,\"mastered\":true,\"harvested\":true,\"delivered\":true,\"promisedShare\":true}";
        var fresh = new HerbalStudy();
        Check(!fresh.TakeLesson() && !fresh.PracticeOneShift() && !fresh.DrySample(), "尚未交药不能动用工钱和静席。");
        var self = HerbalStudy.Restore(sale);
        Check(self.PurseCoins == 3 && self.ListenForKeeper && self.PracticeTicks == 0, "旧收据应接回原有三文。");
        self.ToggleKeeperMessages();
        for (int i = 0; i < HerbalStudy.PracticeGoal; i++) Check(self.PracticeOneShift(), "自修应逐时辰推进。");
        Check(self.FirstSense && self.PracticeTicks == 6 && self.KeeperCall == KeeperCallState.Missed &&
            !self.TakeLesson() && !self.PracticeOneShift() && self.PurseCoins == 3, "可独自感气，入门后不再耗时收费。");

        var guided = HerbalStudy.Restore(sale);
        Check(guided.TakeLesson() && !guided.TakeLesson() && guided.PurseCoins == 1 && guided.EarnedCoins == 3, "学费只付一次，原收据不变。");
        Check(guided.PracticeOneShift() && guided.PracticeProgress == 2 && !guided.PracticeOneShift(), "掌柜来唤时修习须静止等待。");
        var reopened = HerbalStudy.Restore(JsonUtility.ToJson(guided));
        Check(reopened.KeeperCall == KeeperCallState.Available && !reopened.FirstSense && reopened.PracticeTicks == 1, "中途退出只保留已完成的时辰与来唤。");
        Check(reopened.PracticeOneShift() && !reopened.DrySample(), "又坐满一时辰就错过鲜叶。");
        Check(guided.AnswerKeeper(true) && guided.ElapsedShifts == 1 && guided.DrySample() && !guided.DrySample() &&
            guided.ElapsedShifts == 2 && guided.PurseCoins == 2, "亲赴柜台耗一时辰，所得一文只结算一次。");
        Check(guided.PracticeOneShift() && guided.PracticeOneShift() && guided.FirstSense && guided.PracticeTicks == 3, "受指点者可在三个时辰初感灵气。");
        var saved = HerbalStudy.Restore(JsonUtility.ToJson(guided));
        Check(saved.FirstSense && saved.KeeperCall == KeeperCallState.Paid && saved.PurseCoins == 2 && saved.ElapsedShifts == 4,
            "三个修习时辰和一个帮工时辰须与收支一并保存。");
    }

    static void Check(bool ok, string reason)
    {
        if (!ok) throw new InvalidOperationException(reason);
    }
}
