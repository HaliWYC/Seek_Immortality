using System;
using UnityEditor;
using UnityEngine;

public static class FieldEncounterChecks
{
    [MenuItem("行旅/验证松林护药")]
    public static void Run()
    {
        Verify();
        Debug.Log("松林护药验证通过。");
    }

    public static void Verify()
    {
        SparMatchChecks.Verify();
        var solo = NewFight(FieldParty.Solo);
        Check(solo.Units.Count == 3 && solo.Companion == null, "独行仅有本人及木傀。");
        solo.Yield();
        Check(solo.Status == SparStatus.Yielded, "可无伤撤走。");

        var ally = NewFight(FieldParty.Ally);
        var escort = ally.Companion;
        Check(!ally.CanCommand(escort) && !ally.Attack(escort, ally.Units[2].Cell), "友军保留行动权。");
        int beastHealth = ally.Units[2].Hp;
        PlayRound(ally);
        Check(ally.Units[2].Hp < beastHealth, "友军在己方行动后自行施术。");

        var contract = NewFight(FieldParty.Contract);
        Check(contract.CanCommand(contract.Companion), "契友可直接指挥。");
        PlayRound(contract);
        Check(contract.UseAgreedHerb(contract.Companion) && !contract.UseAgreedHerb(contract.Companion) &&
            !contract.TalismanUsed, "止血散按约限量，护符保留。");

        var rescue = NewFight(FieldParty.Contract);
        WaitForWound(rescue);
        Check(rescue.TalismanUsed && rescue.RescueRounds == 2 && rescue.At(rescue.Companion.Cell) == rescue.Companion,
            "护符自行发动，倒地后展示两个完整行动回合。");
        Check(rescue.CanCarryNow, "相邻同伴可带人。");
        rescue.Yield();
        Check(rescue.CompanionCarried && !rescue.CompanionDied, "撤退时救出同行。");

        var abandon = NewFight(FieldParty.Contract, false);
        WaitForWound(abandon);
        Check(!abandon.CanCarryNow, "隔着数格不能带人。");
        abandon.Yield();
        Check(abandon.CompanionDied, "弃下伤友将失去同行。");

        var expiry = NewFight(FieldParty.Contract);
        WaitForWound(expiry);
        PlayRound(expiry);
        Check(expiry.RescueRounds == 1 && !expiry.CompanionDied, "最后一个回合仍可施救。");
        PlayRound(expiry);
        Check(expiry.CompanionDied && expiry.RescueRounds == 0, "追击误期时伤重身亡。");

        var victory = NewFight(FieldParty.Contract);
        for (int n = 0; n < 4 && victory.Status == SparStatus.PlayerTurn; n++)
        {
            var beast = victory.Units[2];
            foreach (var unit in victory.Units)
            {
                if (victory.Status != SparStatus.PlayerTurn) break;
                if (unit.Direct) victory.Attack(unit, beast.Cell);
            }
            PlayRound(victory);
        }
        Check(victory.Status == SparStatus.Won && !victory.TalismanUsed && !victory.CompanionDied,
            "配合行动能在同伴耗尽护符前获胜。");
        VerifyBrigands();
    }

    static void VerifyBrigands()
    {
        var cells = new[] { new Vector2Int(1, 1), new Vector2Int(3, 1), new Vector2Int(3, 2), new Vector2Int(4, 2) };
        var unready = Duel(cells, false, false);
        Check(!unready.UseFieldWard(unready.Units[0]), "没有备符不能凭空破阵。");
        int hits = unready.Units[2].Hp;
        unready.Attack(unready.Units[0], cells[2]);
        Check(hits - unready.Units[2].Hp == 3, "炼气后期的寻常术法被护阵削弱。");
        unready.EndTurn();
        unready.Yield();
        Check(unready.Status == SparStatus.Yielded, "敌方行动前仍可逃离。");

        var blind = Duel(cells, true, false);
        Check(blind.UseFieldWard(blind.Units[0]) && blind.Units[2].Hp == 25 && !blind.FormationBroken, "未验阵时灵符只能损六。");
        var plan = Duel(cells, true, true);
        Check(!plan.UseFieldWard(plan.Units[1]) && plan.UseFieldWard(plan.Units[0]) && !plan.UseFieldWard(plan.Units[0]) &&
            plan.Units[2].Hp == 18 && plan.FormationBroken, "取证、备符、本人出手才能破阵，每符限一次。");
        for (int i = 0; i < 10 && plan.Status == SparStatus.PlayerTurn; i++)
        {
            foreach (var actor in plan.Units)
            {
                if (!plan.CanCommand(actor)) continue;
                SparUnit enemy = plan.Units[2].Alive ? plan.Units[2] : plan.Units[3];
                if (!plan.CanAttack(actor, enemy.Cell))
                {
                    Vector2Int best = actor.Cell;
                    int gap = 99;
                    for (int x=0;x<plan.Columns;x++)
                    for (int y=0;y<plan.Rows;y++)
                    {
                        var cell = new Vector2Int(x, y);
                        int distance = Math.Abs(x-enemy.Cell.x)+Math.Abs(y-enemy.Cell.y);
                        if (plan.CanMove(actor, cell) && distance < gap) { best=cell; gap=distance; }
                    }
                    plan.Move(actor,best);
                }
                plan.Attack(actor, enemy.Cell);
            }
            PlayRound(plan);
        }
        Check(plan.Status == SparStatus.Won, "炼气后期带底牌可越阶击退筑基劫修。");
        var doomed = Duel(cells, false, false);
        for (int i=0;i<15 && doomed.Status==SparStatus.PlayerTurn;i++) PlayRound(doomed);
        Check(doomed.Status == SparStatus.Lost, "应战后停在阵里可致身亡。");
    }

    static SparMatch Duel(Vector2Int[] cells, bool ready, bool studied) =>
        new SparMatch(new bool[6, 5], cells, party: FieldParty.Brigands, cultivationBonus: 3,
            preparedWard: ready, studiedWard: studied);

    static SparMatch NewFight(FieldParty party, bool close = true)
    {
        var deployment = new[]
        {
            close ? new Vector2Int(2, 0) : new Vector2Int(0, 0),
            close ? new Vector2Int(3, 1) : new Vector2Int(0, 1),
            new Vector2Int(3, 2), new Vector2Int(2, 1)
        };
        return new SparMatch(new bool[6, 5], deployment, party: party, cultivationBonus: 3);
    }

    static void WaitForWound(SparMatch match)
    {
        for (int i = 0; i < 8 && match.RescueRounds == 0 && match.Status == SparStatus.PlayerTurn; i++)
            PlayRound(match);
        Check(match.RescueRounds > 0, "伤友须能在真实攻击流程中倒地。");
    }

    static void PlayRound(SparMatch match)
    {
        match.EndTurn();
        for (int i = 0; i < match.Units.Count && match.Status == SparStatus.OpponentTurn; i++) match.StepOpponent();
    }

    static void Check(bool valid, string label)
    {
        if (!valid) throw new InvalidOperationException(label);
    }
}
