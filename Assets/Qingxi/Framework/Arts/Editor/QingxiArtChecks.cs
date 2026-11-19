using System;
using UnityEditor;
using UnityEngine;

public static class QingxiArtChecks
{
    public static void Run()
    {
        var book = QingxiArts.Guiyuan;
        var fire = QingxiArts.Firebolt;
        var flow = QingxiArts.ReturnFlow;
        var ward = QingxiArts.Ward;
        Check(book.MaximumQi == DemoCultivation.Bottleneck && book.QiPerSeason > 0, "归元篇的上限应与炼气圆满相符。");
        Check(fire.Target == FaShuTarget.Enemy && fire.Effect == QingxiSpellEffect.Burn && fire.Duration == FaShuDuration.Once &&
            fire.EffectTurns == 2 && fire.EffectAmount == 2 && QingxiArts.EmberManual.BurnBonus == 1 &&
            flow.Effect == QingxiSpellEffect.ConvertSpirit && flow.ConversionAmount == 2, "功法与术法资源应解析为相应的规则。");
        Check(ward.IsBattleSpell && ward.Target == FaShuTarget.Self && ward.Effect == QingxiSpellEffect.Ward &&
            ward.EffectTurns == 2 && ward.EffectAmount == 2 && ward.GeneratingBonus == 1, "护体诀是有两次抵挡余量的自身术。");
        Check(fire.ManaCost == 5 && fire.ActionPointCost == 0 && QingxiArts.Vine.ManaCost == 3 &&
            QingxiArts.Pulse.ManaCost == 4, "演练的术法采用所配置的法力和行动力消耗。");

        var fresh = new DemoCultivation();
        Check(!fresh.LearnFirebolt(true) && fresh.Days == 0 && fresh.AcceptManual(), "未借归元篇不能修习火弹术。");
        Check(!fresh.LearnFirebolt(false) && fresh.Days == 0, "离开山门不能随知客学火弹术。");
        Check(fresh.LearnFirebolt(true) && fresh.Days == fire.TrainingDays && !fresh.LearnFirebolt(true), "学术成功消耗一日，只能领悟一次。");
        Check(fresh.StartRetreat(1, false) && fresh.TrainSeason() && fresh.Qi == book.QiPerSeason, "闭关按所习功法的修速结算。");
        var fromOld = JsonUtility.FromJson<DemoCultivation>("{\"manual\":true,\"art\":true}");
        Check(fromOld.Manual && fromOld.Art && !fromOld.KnowsFirebolt, "老存档保留归元与归流，火弹须另习。");
        var restored = JsonUtility.FromJson<DemoCultivation>(JsonUtility.ToJson(fresh));
        Check(restored.Manual && restored.KnowsFirebolt && !restored.Art && restored.Qi == book.QiPerSeason, "新习法门按 ID 读回，未习法门不能入册。");

        bool clear = false;
        var cells = new[] { Vector2Int.zero, new Vector2Int(0, 1), new Vector2Int(4, 0), new Vector2Int(5, 1) };
        var match = new SparMatch(new bool[6, 2], cells, (from, to) => false, (from, to) => clear);
        var hero = match.Units[0];
        AwaitTurn(match, hero);
        Check(!match.TogglePlayerSpell(hero), "未准备火诀的普通试阵没有换招选项。");
        match.PreparePlayerSpell(flow);
        Check(!match.TogglePlayerSpell(hero), "冲关归流术不能当作攻敌法术。");
        match.ConfigurePlayerLoadout(SpiritRoots.FireEarth, QingxiArts.EmberManual, new[] { fire }, 0);
        Check(!match.TogglePlayerSpell(match.Units[1]) && match.TogglePlayerSpell(hero), "火弹术由本人选择。");
        Check(!match.Attack(hero, cells[1]) && !match.Attack(hero, cells[2]) &&
            match.SpellSelected && hero.ActionPoints == hero.MaxActionPoints && hero.Mana == hero.MaxMana,
            "目标错误或被挡视线不会扣行动力或法力。");
        clear = true;
        int hp = match.Units[2].Hp;
        Check(match.AttackRange(hero) == fire.Range && match.Attack(hero, cells[2]) && !match.SpellSelected &&
            hp - match.Units[2].Hp == hero.Power + fire.ExtraPower, "真实施术入口消费行动并按术威和法术效果命中。");
        Check(hero.ActionPoints == hero.MaxActionPoints && hero.Mana == hero.MaxMana - fire.ManaCost &&
            match.DaoCang(WuXing.离火) == 2 && !match.SelectPlayerSpell(hero, fire),
            "火弹只消耗法力和道藏，冷却在施放后立即生效。");
        Check(match.NextSpellTurn == hero.TurnsStarted + fire.CooldownTurns + 1, "火弹的恢复时间应以施法者自己的行动计。");
        for (int i = 0; i < fire.CooldownTurns; i++)
        {
            match.EndTurn();
            AwaitTurn(match, hero);
            Check(!match.CanSelectSpell(hero) && match.AttackRange(hero) == hero.Range, "恢复期间仍可常击。");
        }
        match.EndTurn();
        AwaitTurn(match, hero);
        Check(match.CanSelectSpell(hero) && hero.Mana == hero.MaxMana - fire.ManaCost,
            "冷却完成后可以重新施法，已花费的法力不会自行回补。");
        CheckElementalCasting();
        CheckSparringCombo();
        CheckSpellResources();
        CheckStatusCombinations();
        Debug.Log("青岚功法、法力、道藏、相生相克、缚枝、灼烧与护体规则验证通过。");
    }

    static void CheckElementalCasting()
    {
        var cells = new[] { Vector2Int.zero, new Vector2Int(0, 2), new Vector2Int(1, 0), new Vector2Int(5, 2) };
        var spells = new[] { QingxiArts.Pulse, QingxiArts.Firebolt, QingxiArts.ReturnFlow, QingxiArts.Pulse };
        var match = new SparMatch(new bool[6, 3], cells, canTraverse: (from, to) => false);
        match.ConfigurePlayerLoadout(SpiritRoots.For(false), QingxiArts.Guiyuan, spells, 0);
        var hero = match.Units[0];
        var rival = match.Units[2];
        Check(match.PlayerSpells.Count == 2 && match.Intake(WuXing.厚土) == 1 && match.Intake(WuXing.离火) == 2 &&
            match.Intake(WuXing.弱水) == 2 && match.Intake(WuXing.灵木) == 1, "六次依根抽取可复现；冲关术与重复术不占战斗术位。");

        Check(match.SelectPlayerSpell(hero, QingxiArts.Pulse) && match.HasPaymentChoice, "同类道藏须能选择不同的实际五行。");
        Check(match.CastCostText.Contains("相克") && match.PreviewAttackDamage(hero, rival.Cell) == hero.Power + 2,
            "厚土配灵木相克，加成需出现在命中预估上。");
        Check(match.CyclePayment(hero) && match.CyclePayment(hero) && match.CastCostText.Contains("相生") &&
            match.PreviewAttackDamage(hero, rival.Cell) == hero.Power, "改配离火后，相克伤害预估必须消失。");
        Check(!match.SelectPlayerSpell(hero, QingxiArts.ReturnFlow) && match.SpellSelected &&
            match.CastCostText.Contains("相生"), "无法换招时，被选术法的配气保持原样。");
        Check(match.SelectPlayerSpell(hero, null) && match.DaoCang(WuXing.厚土) == 1 && match.DaoCang(WuXing.离火) == 2 &&
            match.SelectPlayerSpell(hero, QingxiArts.Pulse), "撤回或更换配气不能消耗道藏。");
        Check(!match.Attack(hero, cells[1]) && match.DaoCang(WuXing.灵木) == 1 && match.DaoCang(WuXing.厚土) == 1, "提前选择或无效目标不扣气。");
        int hp = rival.Hp;
        Check(match.Attack(hero, rival.Cell) && rival.Hp == hp - hero.Power - 2 && match.DaoCang(WuXing.灵木) == 0 &&
            match.DaoCang(WuXing.厚土) == 0 && match.DaoCang(WuXing.离火) == 2 &&
            hero.ActionPoints == hero.MaxActionPoints && hero.Mana == hero.MaxMana - QingxiArts.Pulse.ManaCost &&
            !match.SelectPlayerSpell(hero, QingxiArts.Pulse), "合气指只扣实付道藏，厚土耗尽后不能再选。");
        match.EndTurn();
        AwaitTurn(match, hero);
        int total = 0;
        for (int i = 0; i < 5; i++)
        {
            Check(match.DaoCang((WuXing)i) == match.Intake((WuXing)i), "重新引气覆盖旧五行。");
            total += match.DaoCang((WuXing)i);
        }
        Check(total == 6, "下一次本人行动不继承上次余气。");

        var heal = new SparMatch(new bool[6, 3], cells, canTraverse: (from, to) => false);
        heal.ConfigurePlayerLoadout(SpiritRoots.For(false), QingxiArts.Guiyuan, spells, 33);
        hero = heal.Units[0];
        heal.EndTurn();
        AwaitTurn(heal, hero);
        hp = hero.Hp;
        Check(hero.MaxHp - hp >= 2 && heal.SelectPlayerSpell(hero, QingxiArts.Pulse) && heal.CastCostText.Contains("相生"),
            "受击后可选厚土与锐金相生。");
        Check(heal.Attack(hero, cells[2]) && hero.Hp == hp + 2 && heal.DaoCang(WuXing.锐金) == 0 &&
            heal.DaoCang(WuXing.厚土) == 0, "实际相生回复气血，仅扣所配金土。");
        Check(heal.LastHealthChanges.Count == 2 && heal.LastHealthChanges[0].Target == heal.Units[2] &&
            heal.LastHealthChanges[0].Amount == -hero.Power && heal.LastHealthChanges[1].Target == hero &&
            heal.LastHealthChanges[1].Amount == 2 && heal.LastHealthChanges[1].Spell == QingxiArts.Pulse,
            "同一记合气指分别报告目标扣血和施法者实际回复。");
        var pulseEntry = heal.Journal[heal.TurnNumber - 1].Actions[0];
        Check(heal.Attack(hero, cells[2]) && heal.LastHealthChanges.Count == 1 && pulseEntry.Name == QingxiArts.Pulse.Name &&
            pulseEntry.HealthChanges.Count == 2 && pulseEntry.HealthChanges[0].Amount == -hero.Power &&
            pulseEntry.HealthChanges[1].Amount == 2,
            "一记法术的实际伤害与回复记录保留在原动作中，不会被后来命中的数值覆盖。");

        var grove = new SparMatch(new bool[6, 3], cells, canTraverse: (from, to) => false);
        grove.ConfigurePlayerLoadout(SpiritRoots.For(true), QingxiArts.SpringManual, new[] { QingxiArts.Vine, QingxiArts.Firebolt }, 0);
        hero = grove.Units[0];
        rival = grove.Units[2];
        Check(grove.Intake(WuXing.灵木) == 6 && grove.Intake(WuXing.离火) == 0 && !grove.SelectPlayerSpell(hero, QingxiArts.Firebolt),
            "纯木根配青息经只得灵木，不能施火弹。");
        Check(grove.SelectPlayerSpell(hero, QingxiArts.Vine) && grove.BindStatusFor(rival).RemainingTurns == 0 &&
            grove.Attack(hero, rival.Cell) && grove.DaoCang(WuXing.灵木) == 4,
            "缚枝应结算灵木与命中。");
        var pending = grove.BindStatusFor(rival);
        Check(pending.MovePenalty == QingxiArts.Vine.EffectAmount && pending.RemainingTurns == 1 && !pending.AppliesNow,
            "缚枝命中后显示具体减步，待目标下一次行动生效。");
        Check(grove.SelectPlayerSpell(hero, QingxiArts.Vine) && grove.Attack(hero, rival.Cell) &&
            hero.ActionPoints == hero.MaxActionPoints && hero.Mana == hero.MaxMana - 2 * QingxiArts.Vine.ManaCost &&
            grove.DaoCang(WuXing.灵木) == 2 && rival.Hp == 1, "零冷却术法在道藏和法力充足时可于同回合再施放。");
        grove.EndTurn();
        Check(grove.ActiveUnit == rival && rival.MovementLeft == rival.MaxActionPoints - QingxiArts.Vine.EffectAmount &&
            rival.ActionPoints == rival.MaxActionPoints,
            "被缚敌人下次行动少走两格，同时保留出招所需行动力，重复缚枝不会叠加。");
        var active = grove.BindStatusFor(rival);
        Check(active.AppliesNow && active.RemainingTurns == 1 && active.MovePenalty == pending.MovePenalty,
            "目标在行动时仍显示缚枝及本次行动的减步。");
        grove.StepOpponent();
        Check(grove.BindStatusFor(rival).RemainingTurns == 0, "被缚目标行动结束后立即隐藏缚枝。");
        AwaitTurn(grove, hero);
        grove.EndTurn();
        Check(grove.ActiveUnit == rival && rival.MovementLeft == rival.MaxActionPoints, "缚枝只持续一次本人行动。");

        var alternate = new SparMatch(new bool[6, 3], cells);
        alternate.ConfigurePlayerLoadout(SpiritRoots.For(true), QingxiArts.EmberManual, spells, 0);
        Check(alternate.Intake(WuXing.灵木) == 4 && alternate.Intake(WuXing.离火) == 2 &&
            alternate.SelectPlayerSpell(alternate.Units[0], QingxiArts.Firebolt), "同一木根改修赤炉，依根四木，加功法二火。");
    }

    static void CheckSparringCombo()
    {
        var cells = new[] { new Vector2Int(1, 1), Vector2Int.zero, new Vector2Int(5, 3), new Vector2Int(3, 1) };
        var match = new SparMatch(new bool[6, 4], cells);
        SparLoadout.PracticeOptions()[2].ApplyTo(match);
        var hero = match.ActiveUnit;
        var guard = match.Units[3];
        var retreat = new Vector2Int(1, 3);
        int turn = match.TurnNumber;
        Check(match.SelectPlayerSpell(hero, QingxiArts.Vine) && match.Attack(hero, guard.Cell) &&
            hero.ActionPoints == 6 && hero.Mana == 9 && match.DaoCang(WuXing.灵木) == 2, "青息切磋首轮缚枝只花法力和灵木。");
        Check(match.PreviewPath(hero, retreat).Count == 3 && hero.ActionPoints == 6 && match.Move(hero, retreat) &&
            hero.ActionPoints == 4 && hero.Mana == 9, "两格移动只扣两点行动力。");
        Check(match.SelectPlayerSpell(hero, QingxiArts.Pulse) && match.PreviewAttackDamage(hero, guard.Cell) == 6 &&
            match.Attack(hero, guard.Cell) && hero.ActionPoints == 4 && hero.Mana == 5 && guard.Hp == 1 &&
            match.DaoCang(WuXing.灵木) == 1 && match.DaoCang(WuXing.厚土) == 1 && match.TurnNumber == turn,
            "缚枝、走两格、合气指可在首轮完整连用，法力和行动力按各行动分别扣除。");
    }

    static void CheckSpellResources()
    {
        var cells = new[] { new Vector2Int(1, 1), Vector2Int.zero, new Vector2Int(3, 0), new Vector2Int(5, 4) };
        var demanding = UnityEngine.Object.Instantiate(QingxiArts.Vine);
        try
        {
            var settings = new SerializedObject(demanding);
            settings.FindProperty("actionPointCost").intValue = 2;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var match = new SparMatch(new bool[7, 6], cells);
            var hero = match.Units[0];
            var target = match.Units[3];
            var destination = new Vector2Int(4, 4);
            match.ConfigurePlayerLoadout(SpiritRoots.For(true), QingxiArts.SpringManual, new[] { QingxiArts.Vine, demanding }, 0);
            Check(match.Move(hero, destination) && hero.ActionPoints == 0, "可把行动力全部用于走六格。");
            Check(!match.Attack(hero, target.Cell) && !match.SelectPlayerSpell(hero, demanding) &&
                match.SpellUnavailableReason(hero, demanding).Contains("行动力"), "行动力为零时，常击和点名消耗行动力的术法都不可用。");
            Check(match.SelectPlayerSpell(hero, QingxiArts.Vine) && match.CurrentAttackActionCost(hero) == 0 &&
                match.Attack(hero, target.Cell) && hero.ActionPoints == 0 && hero.Mana == 9,
                "行动力为零时，普通术法仍按法力及道藏结算。");

            var expensive = new SparMatch(new bool[7, 6], cells, canTraverse: (a, b) => false,
                clearShot: (from, to) => from == cells[0]);
            hero = expensive.Units[0];
            settings.FindProperty("manaCost").intValue = hero.MaxMana;
            settings.ApplyModifiedPropertiesWithoutUndo();
            expensive.ConfigurePlayerLoadout(SpiritRoots.For(true), QingxiArts.SpringManual, new[] { QingxiArts.Vine, demanding }, 0);
            Check(expensive.SelectPlayerSpell(hero, demanding) && expensive.CurrentAttackActionCost(hero) == 2 &&
                expensive.Attack(hero, cells[2]) && hero.ActionPoints == hero.MaxActionPoints - 2 &&
                hero.Mana == 0 && expensive.DaoCang(WuXing.灵木) == 4,
                "特殊术法同时扣除资产所指定的行动力、法力与五行。");
            Check(!expensive.SelectPlayerSpell(hero, QingxiArts.Vine) && expensive.SpellUnavailableReason(hero, QingxiArts.Vine).Contains("法力"),
                "仅有行动力和道藏无法施放缺少法力的术。");
            expensive.EndTurn();
            AwaitTurn(expensive, hero);
            Check(hero.ActionPoints == hero.MaxActionPoints && hero.Mana == 0 &&
                expensive.DaoCang(WuXing.灵木) == 6 && !expensive.CanSelectSpell(hero),
                "回合开始补足行动力、新分配道藏，但维持法力余额。");
        }
        finally { UnityEngine.Object.DestroyImmediate(demanding); }
    }

    static void CheckStatusCombinations()
    {
        var formation = new[] { Vector2Int.zero, new Vector2Int(0, 4), new Vector2Int(1, 0), new Vector2Int(5, 4) };
        var burn = new SparMatch(new bool[6, 5], formation, canTraverse: (_, _) => false) { DeferTurnOpening = true };
        SparLoadout.PracticeOptions()[4].ApplyTo(burn);
        var hero = burn.Units[0];
        var foe = burn.Units[2];
        var fire = QingxiArts.Firebolt;
        Check(burn.SelectPlayerSpell(hero, fire) && burn.CastCostText.Contains("灼烧") &&
            burn.Attack(hero, foe.Cell) && foe.Hp == 3 && burn.BurnStatusFor(foe).Amount == 3 &&
            burn.BurnStatusFor(foe).Remaining == 2, "赤炉诀只增强灼烧，火弹命中时扣直接伤害并挂上两次灼烧。");
        burn.EndTurn();
        Check(burn.OpeningPending && burn.ActiveUnit == foe && foe.Hp == 3 && !burn.TryPlanOpponentAction(out _),
            "等到受术者自己的行动才触发灼烧，等待开场结算时无法提前出手。");
        Check(burn.ResolveTurnOpening() && !burn.ResolveTurnOpening() && burn.ActiveUnit == foe && foe.Hp == 0,
            "实际结算一次，倒地者仍留在本次开场直到播放完成。");
        var tick = burn.Journal[1].Actions[0];
        Check(tick.StatusEvents.Count == 1 && tick.StatusEvents[0].Kind == SparStatusEventKind.BurnTick &&
            tick.HealthChanges.Count == 1 && tick.HealthChanges[0].Amount == -3 &&
            burn.FinishTurnOpening() && burn.ActiveUnit != foe, "灼烧按本次行动归档，击倒后跳过该棋手。");

        var ward = QingxiArts.Ward;
        var defense = new SparMatch(new bool[6, 5], formation, canTraverse: (_, _) => false);
        SparLoadout.PracticeOptions()[4].ApplyTo(defense);
        hero = defense.Units[0];
        foe = defense.Units[2];
        Check(defense.SelectPlayerSpell(hero, ward) && defense.CastCostText.Contains("相生") &&
            !defense.Attack(hero, foe.Cell) && hero.Mana == hero.MaxMana && defense.Attack(hero, hero.Cell) &&
            hero.Mana == hero.MaxMana - ward.ManaCost && hero.ActionPoints == hero.MaxActionPoints &&
            defense.WardStatusFor(hero).Amount == 3 && defense.WardStatusFor(hero).Remaining == 2,
            "厚土配离火相生提高每次抵挡，自身预览和无效目标均不扣资源。");
        defense.EndTurn();
        var opening = defense.Journal[1];
        Check(defense.TryPlanOpponentAction(out var direct) && defense.ExecuteOpponentAction(direct), "巡山弟子能在原地发起直击。");
        Check(defense.WardStatusFor(hero).Remaining == 1 && hero.Hp == hero.MaxHp && opening.Actions.Count == 1 &&
            opening.Actions[0].HealthChanges.Count == 0 && opening.Actions[0].StatusEvents[0].Kind == SparStatusEventKind.WardBlock &&
            opening.Actions[0].StatusEvents[0].Amount == 3, "完全挡下时记录抵挡额，气血不变，没有零点伤害。");
        defense.FinishOpponentTurn();
        AwaitTurn(defense, hero);
        Check(defense.WardStatusFor(hero).Remaining == 1, "其他人行动和本人再行动都不会扣除护体次数。");
        defense.EndTurn();
        Check(defense.TryPlanOpponentAction(out direct) && defense.ExecuteOpponentAction(direct), "巡山弟子再次发起直击。");
        Check(defense.WardStatusFor(hero).Remaining == 0 && hero.Hp == hero.MaxHp, "第二次直击耗尽护体。");
        defense.FinishOpponentTurn();
        AwaitTurn(defense, hero);
        defense.EndTurn();
        Check(defense.TryPlanOpponentAction(out direct) && defense.ExecuteOpponentAction(direct), "护体耗尽后发起第三次直击。");
        Check(hero.Hp == hero.MaxHp - foe.Power, "用尽护体后按原伤害扣气血。");

        var plain = new SparMatch(new bool[6, 5], formation, canTraverse: (_, _) => false);
        SparLoadout.PracticeOptions()[4].ApplyTo(plain);
        hero = plain.Units[0];
        Check(plain.SelectPlayerSpell(hero, ward) && plain.CyclePayment(hero) &&
            !plain.CastCostText.Contains("相生") && plain.Attack(hero, hero.Cell), "可用同类厚土配气试出基础护体。");
        plain.EndTurn();
        var partial = plain.Journal[1];
        Check(plain.TryPlanOpponentAction(out direct) && plain.ExecuteOpponentAction(direct), "基础护体承受直击。");
        Check(partial.Actions[0].StatusEvents[0].Amount == 2 && partial.Actions[0].HealthChanges[0].Amount == -1 &&
            hero.Hp == hero.MaxHp - 1, "部分抵挡分别记录挡下的 2 点与实际扣掉的 1 点。");
    }

    static void AwaitTurn(SparMatch match, SparUnit unit)
    {
        for (int i = 0; i < 24 && !match.CanCommand(unit); i++)
        {
            if (match.Status == SparStatus.OpponentTurn) match.StepOpponent();
            else match.EndTurn();
        }
        Check(match.CanCommand(unit), "演练应轮到测试修士。");
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
