using System;
using UnityEngine;

public static class SparInitiativeChecks
{
    public static void Run()
    {
        // Two intact sides with blocked sight can wait indefinitely. Inspect a full
        // hundred breaths, including the simultaneous arrivals at the end.
        var cells = new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(4, 0), new Vector2Int(4, 1) };
        var fight = new SparMatch(new bool[5, 2], cells, clearShot: (from, to) => false);
        Check(fight.ActiveUnit == fight.Units[0] && !fight.CanCommand(fight.Units[1]), "最先蓄满者可行动，队友尚未轮到。");
        double halfway = fight.CombatTime / 2;
        foreach (var unit in fight.Units)
        {
            Check(fight.ChargeAt(unit, 0d) == 0f, "开场所有行动条都从零蓄起。");
            Check(Math.Abs(fight.ChargeAt(unit, halfway) - halfway * unit.Reaction / 100d) < 1e-6,
                "每条的增长量应随角色实际反应变化。");
        }
        int[] seen = new int[4];
        int simultaneous = 0;
        while (fight.CombatTime <= 100d)
        {
            int current = Array.IndexOf(cells, fight.ActiveUnit.Cell);
            seen[current]++;
            if (fight.CombatTime == 100d) Check(current == simultaneous++, "同刻按布阵顺序行动。");
            var next = fight.Upcoming[0];
            var outgoing = fight.ActiveUnit;
            double previous = fight.CombatTime;
            Check(fight.ChargeAt(outgoing, previous) > 0.99999f, "出手者在决策时行动条保持满格。");
            int countBefore = fight.TurnNumber;
            // Looking ahead or idling never expends a turn.
            Check(ReferenceEquals(fight.Upcoming[0].Actor, next.Actor) && fight.TurnNumber == countBefore, "预览不能推进时间。");
            if (fight.Status == SparStatus.PlayerTurn) fight.EndTurn(); else fight.StepOpponent();
            Check(fight.ActiveUnit == next.Actor && Math.Abs(fight.CombatTime - next.At) < 1e-8, "预测的棋手和时刻必须与实际一致。");
            Check(fight.TurnNumber == countBefore + 1, "每次只推进一个角色。");
            Check(fight.PreviousCombatTime == previous && fight.ChargeAt(outgoing, previous) < 0.00001f,
                "行动结束便清空本人的条，其余棋手从同一时刻续蓄。");
        }
        for (int i = 0; i < seen.Length; i++) Check(seen[i] == fight.Units[i].Reaction, "等长时间内的出手次数随反应变化。");
        Check(simultaneous == 4, "同刻抵达的每人都有一次行动。");
        Debug.Log("反应、行动间隔、同刻顺序与预览验证通过。");
    }

    static void Check(bool valid, string detail)
    {
        if (!valid) throw new InvalidOperationException(detail);
    }
}
