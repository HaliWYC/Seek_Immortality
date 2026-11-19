using System;
using UnityEngine;

public static class GateLoadoutChecks
{
    public static void Run()
    {
        var fresh = new DemoCultivation();
        Require(fresh.MainManual == null && fresh.PreparedSpells.Count == 0, "凡人不能开局携带尚未学过的功法或术法");
        Require(!fresh.EquipMain(QingxiArts.EmberManual) && !fresh.TogglePrepared(QingxiArts.Vine), "未学术法不能装备");
        Require(!fresh.StudyAtGate(QingxiArts.EmberManual, true) && fresh.AcceptManual(), "借卷前不能研习旁的传承");
        Require(fresh.MainManual == QingxiArts.Guiyuan && !fresh.StudyAtGate(QingxiArts.EmberManual, false), "归元篇默认主修，学习新卷须亲往山门");
        Require(fresh.StudyAtGate(QingxiArts.EmberManual, true) && fresh.EquipMain(QingxiArts.EmberManual), "山门新主修可学可换");
        Require(fresh.Days == DemoCultivation.ManualTrainingDays && !fresh.StudyAtGate(QingxiArts.EmberManual, true), "已习不能再耗日");
        Require(fresh.StartRetreat(1, false) && !fresh.EquipMain(QingxiArts.Guiyuan) && fresh.TrainSeason() &&
            fresh.Qi == QingxiArts.EmberManual.QiPerSeason && fresh.EquipMain(QingxiArts.Guiyuan), "闭关期间用所选主修结算，出关可改回");
        Require(fresh.StudyAtGate(QingxiArts.Vine, true) && fresh.LearnFirebolt(true) && fresh.StudyAtGate(QingxiArts.Pulse, true), "三门战棋术均可亲自修习");
        Require(fresh.PreparedSpells.Count == 2 && fresh.IsPrepared(QingxiArts.VineId) && !fresh.IsPrepared(QingxiArts.PulseId) &&
            !fresh.TogglePrepared(QingxiArts.Pulse), "满两招后学习不覆盖原来选择");
        Require(fresh.TogglePrepared(QingxiArts.Vine) && fresh.TogglePrepared(QingxiArts.Pulse) &&
            fresh.EquipMain(QingxiArts.EmberManual), "撤一门术后可列入另一门");
        var restored = JsonUtility.FromJson<DemoCultivation>(JsonUtility.ToJson(fresh));
        Require(restored.MainManual == QingxiArts.EmberManual && restored.PreparedSpells[0] == QingxiArts.Firebolt &&
            restored.PreparedSpells[1] == QingxiArts.Pulse && restored.KnowsArt(QingxiArts.VineId), "读档恢复主修、备招顺序和未选的已学法术");
        Require(restored.TogglePrepared(QingxiArts.Firebolt) && restored.TogglePrepared(QingxiArts.Pulse) &&
            JsonUtility.FromJson<DemoCultivation>(JsonUtility.ToJson(restored)).PreparedSpells.Count == 0, "本人可以留空备招，读档不补回");
        var legacy = JsonUtility.FromJson<DemoCultivation>("{\"manual\":true,\"art\":true,\"knownArts\":[\"spell.firebolt\"]}");
        Require(legacy.MainManual == QingxiArts.Guiyuan && legacy.Art && legacy.PreparedSpells.Count == 1 &&
            legacy.PreparedSpells[0] == QingxiArts.Firebolt && !legacy.TogglePrepared(QingxiArts.ReturnFlow), "旧档自动恢复归元主修与火弹，冲关归流术不占备招");
        Debug.Log("栖霞习术、改修、备招与旧档迁移验证通过。");
    }

    static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
