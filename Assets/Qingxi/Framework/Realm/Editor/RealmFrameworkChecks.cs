using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class RealmFrameworkChecks
{
    [MenuItem("行旅/验证境界与人物属性")]
    public static void Run()
    {
        var catalog = CultivationRealms.Table;
        var ids = new HashSet<string>();
        var refiningThresholds = new HashSet<int>();
        foreach (var row in catalog.Stages)
        {
            Check(!string.IsNullOrEmpty(row.Id) && !string.IsNullOrEmpty(row.Name) && ids.Add(row.Id), "境界标识或名称缺失、重复");
            Check(ReferenceEquals(row, catalog.Get(row.Major, row.Phase)), "境界与小阶重复");
            Check(row.Reaction > 0, "行动周期需要正反应值");
            if (row.Major == RealmMajor.QiRefining)
                Check(row.MinimumQi > 0 && refiningThresholds.Add(row.MinimumQi), "炼气各阶须有独立修为门槛");
        }
        Check(catalog.Resolve(false, 0, false).Phase == RealmPhase.None &&
              catalog.Resolve(true, 0, false).Phase == RealmPhase.FirstSense, "凡人与初感须可辨认");
        Check(catalog.Resolve(true, 5, false).Phase == RealmPhase.Early && !catalog.Resolve(true, 5, false).CanFly &&
              catalog.Resolve(true, 6, false).CanFly && catalog.Resolve(true, 9, false).Phase == RealmPhase.Late &&
              catalog.Resolve(true, 12, false).Phase == RealmPhase.Complete &&
              catalog.Resolve(true, 1000000, false).Major == RealmMajor.QiRefining, "修为不能越过手动筑基");

        var wood = SpiritRoots.For(true);
        var five = SpiritRoots.For(false);
        int[] weights = new int[5];
        for (int turn = 1; turn <= 5; turn++)
        for (int n = 0; n < 4; n++)
        {
            Check(wood.IntakeElement(turn, n) == SpiritElement.Wood, "木天灵根的入气元素改变");
            ++weights[(int)five.IntakeElement(turn, n)];
        }
        for (int i = 0; i < 5; i++)
            Check(weights[i] == 4 && five.Weight((SpiritElement)i) > 0, "五行顺序或杂灵根分配偏移");
        var uneven = JsonUtility.FromJson<SpiritRoots>("{\"wood\":70,\"water\":30}");
        int woodDraws = 0;
        for (int turn = 1; turn <= 500; turn++)
            if (uneven.IntakeElement(turn, 0) == SpiritElement.Wood) woodDraws++;
        Check(woodDraws == 350, "新权重应影响实际吸纳");

        // Minimal records in the format of the first volume, before character attributes existed.
        var old = JsonUtility.FromJson<DemoCultivation>("{\"qi\":9,\"quality\":0}");
        var late = old.Attributes(true);
        Check(late.Realm.Id == "qi_late" && late.MaxHealth == 20 && late.Attack == 6 && late.Reaction == 13,
            "旧版炼气后期档应直接得到可用的斗法属性");
        var foundation = JsonUtility.FromJson<DemoCultivation>("{\"qi\":12,\"foundation\":true,\"quality\":3}").Attributes(true);
        Check(foundation.Realm.Major == RealmMajor.Foundation && foundation.ManaCapacity == 200 && foundation.Reaction == 16,
            "旧档筑基品质应计入经络与行旅法力容量");

        var positions = new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.one };
        foreach (FieldParty party in new[] { FieldParty.Solo, FieldParty.Contract, FieldParty.Brigands })
        {
            var hero = new SparMatch(new bool[3,3], positions, party: party, heroAttributes: late).Units[0];
            Check(hero.MaxHp == late.MaxHealth && hero.Power == late.Attack &&
                  hero.Steps == late.Movement && hero.Reaction == late.Reaction, "护药和死斗应使用同一人物属性");
        }
        var foundationFighter = new SparMatch(new bool[3, 3], positions, party: FieldParty.Solo, heroAttributes: foundation).Units[0];
        Check(foundationFighter.MaxMana == foundation.ManaCapacity && foundationFighter.Mana == foundationFighter.MaxMana,
            "野外斗法和御剑应使用同一个法力上限。");

        var journey = new TravelModel();
        var saved = journey.Capture();
        saved.mana = 41.25f;
        journey.SetManaCapacity(late.ManaCapacity);
        Check(journey.Restore(saved) && journey.Mana == 41.25f, "旧版途中余额须原值保留");
        journey.SetManaCapacity(foundation.ManaCapacity);
        Check(journey.Mana == 41.25f, "突破扩充容量时不可自动回满");
        journey.Rest();
        Check(journey.Mana == 200 && journey.CanRest == false, "调息按现境界补足法力");
        var restored = new TravelModel();
        restored.SetManaCapacity(foundation.ManaCapacity);
        Check(restored.Restore(journey.Capture()) && restored.Mana == 200, "过百的法力在续程后还应保留");
        CheckSharedMana(late, positions);
        Debug.Log("境界、五行、人物属性、野外斗法与行旅法力验证通过。");
    }

    static void CheckSharedMana(CultivatorAttributes late, Vector2Int[] positions)
    {
        var journey = new TravelModel();
        journey.Interact();
        journey.ToggleFlight();
        journey.StartRoute(TravelRoute.Road);
        journey.TickAuto(2f);
        float balance = journey.Mana;
        Check(balance < journey.ManaCapacity && balance != TravelModel.DisplayMana(balance),
            "实际御剑产生小数余额，应原样带入后续流程。");

        var oldSave = new DemoCharacter { regionData = JsonUtility.ToJson(journey.Capture()) };
        Check(oldSave.Mana == balance, "第一次读取旧档，继承御剑后的精确法力。");
        var converted = JsonUtility.FromJson<DemoCharacter>(JsonUtility.ToJson(oldSave));
        converted.regionData = JsonUtility.ToJson(new TravelModel().Capture());
        Check(converted.Mana == balance, "有独立法力余额后，不再从旧舆图覆盖当前值。");
        var depleted = journey.Capture();
        depleted.mana = 0;
        Check(new DemoCharacter { regionData = JsonUtility.ToJson(depleted) }.Mana == 0, "旧档中的零法力不能视为未初始化。");

        var fight = new SparMatch(new bool[3, 3], positions, party: FieldParty.Solo, heroAttributes: late, heroMana: balance);
        var hero = fight.Units[0];
        fight.PreparePlayerSpell(QingxiArts.Firebolt);
        var enemy = fight.Units[2];
        Check(hero.Mana == balance && hero.MaxMana == late.ManaCapacity && fight.SelectPlayerSpell(hero, QingxiArts.Firebolt),
            "以御剑后的实际余额入战，法力上限与当前境界一致。");
        Check(fight.PreviewAttackDamage(hero, enemy.Cell) > 0 && hero.Mana == balance,
            "预览施法不会消费共享法力。");
        Check(fight.Attack(hero, enemy.Cell) && hero.Mana == balance - QingxiArts.Firebolt.ManaCost,
            "确认施法只从原有余额扣除完整费用，小数部分留在池里。");
        fight.Yield();
        Check(hero.Mana == balance - QingxiArts.Firebolt.ManaCost, "撤离不得回填已经花掉的法力。");
        var nearFull = new TravelModel().Capture();
        nearFull.mana = 99.9995f;
        Check(journey.Restore(nearFull) && journey.CanRest, "只要尚未满法力且能落脚，就有机会调息。");

        var practice = new SparMatch(new bool[3, 3], positions, party: FieldParty.Practice, heroAttributes: late, heroMana: 0);
        Check(practice.Units[0].Mana == 12 && practice.Units[0].MaxMana == 12,
            "角色的野外余量和境界不会改动安全切磋使用的统一额度。");
    }

    static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
