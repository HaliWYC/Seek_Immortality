using System;
using UnityEditor;
using UnityEngine;

public static class SparMatchChecks
{
    [MenuItem("行旅/验证切磋规则")]
    public static void Run()
    {
        Verify();
        Debug.Log("切磋规则验证通过。");
    }

    public static void Verify()
    {
        VerifySampledTerrain();
        var game = new SparMatch();
        var pupil = game.Units[0];
        var puppet = game.Units[1];
        var disciple = game.Units[2];
        var guard = game.Units[3];
        var initialCell = pupil.Cell;

        Check(!game.Move(pupil, new Vector2Int(3, 1)), "Stone blocks movement.");
        Check(!game.Move(pupil, new Vector2Int(4, 1)), "Walking around the stone exceeds three steps.");
        Check(!game.Move(pupil, puppet.Cell), "An occupied square is impassable.");
        Check(!game.Move(pupil, new Vector2Int(-1, 1)), "Board boundaries are enforced.");
        Check(!game.CanMove(new SparMatch().Units[0], new Vector2Int(1, 2)), "Unit belongs to another match.");
        Check(!game.Attack(pupil, disciple.Cell) && !game.Attack(pupil, puppet.Cell), "Range and factions apply.");
        Check(game.Move(pupil, new Vector2Int(2, 1)), "Pupil can move.");
        Check(!game.Move(pupil, initialCell), "Movement is spent.");
        Check(game.Move(puppet, new Vector2Int(2, 4)), "Puppet has its own action budget.");
        game.EndTurn();
        Check(!game.Attack(pupil, disciple.Cell) && !game.Move(disciple, new Vector2Int(5, 1)), "Player input is locked while opponents act.");
        game.StepOpponent();
        Check(game.Status == SparStatus.OpponentTurn && game.Round == 1, "Only one opponent steps at a time.");
        game.StepOpponent();
        Check(game.Round == 2 && !pupil.Moved && !puppet.Moved, "New round restores movement.");
        Check(game.Attack(pupil, disciple.Cell), "A spell reaches over low stone.");
        Check(!game.Attack(pupil, disciple.Cell), "Only one spell per unit per round.");
        Check(game.Move(puppet, new Vector2Int(3, 5)) && game.Attack(puppet, guard.Cell), "Puppet can approach and cast.");
        FinishRound(game);
        for (int round = 3; round <= 4; round++)
        {
            Check(game.Attack(pupil, disciple.Cell), "Pupil can focus fire across rounds.");
            Check(game.Attack(puppet, guard.Cell), "Puppet can commit to holding the other opponent.");
            FinishRound(game);
        }
        Check(!disciple.Alive && game.At(disciple.Cell) == null, "Withdrawn units free their square.");
        Check(game.Move(pupil, new Vector2Int(2, 4)) && game.Attack(pupil, guard.Cell), "A final supporting spell lands.");
        Check(game.Status == SparStatus.Won, "A coordinated pair can win the match.");
        game.Yield();
        Check(game.Status == SparStatus.Won, "Terminal state cannot be overridden.");

        var passive = new SparMatch();
        for (int i = 0; passive.Status == SparStatus.PlayerTurn && i < 20; i++) FinishRound(passive);
        Check(passive.Status == SparStatus.Lost, "Passing every round can end in defeat.");

        var replay = new SparMatch();
        Check(replay.Round == 1 && replay.Units[0].Hp == replay.Units[0].MaxHp, "Replay starts with fresh participants.");
        replay.Yield();
        replay.StepOpponent();
        Check(replay.Status == SparStatus.Yielded && !replay.CanMove(replay.Units[0], new Vector2Int(1, 2)), "Concession ends the match.");
    }

    static void VerifySampledTerrain()
    {
        var terrain = new bool[11, 5];
        for (int y = 0; y < 5; y++) terrain[5, y] = true;
        var deployment = new[]
        {
            new Vector2Int(4, 2), new Vector2Int(2, 4), new Vector2Int(10, 2), new Vector2Int(10, 4)
        };
        var match = new SparMatch(terrain, deployment);
        Check(match.Columns == 11 && match.Rows == 5, "Exploration samples determine grid dimensions.");
        Check(!match.IsBlocked(new Vector2Int(3, 1)), "Example stones do not leak into the local map.");
        Check(match.IsBlocked(new Vector2Int(5, 2)) && match.IsBlocked(new Vector2Int(11, 2)), "Walls and true map edges block walking.");
        Check(match.Units[2].Cell == deployment[2], "The map places participants, including beyond the example width.");
        Check(!match.CanMove(match.Units[0], new Vector2Int(6, 2)), "A continuous sampled wall cannot be crossed.");
        terrain[5, 2] = false;
        Check(match.IsBlocked(new Vector2Int(5, 2)), "The match owns its snapshot.");
        FinishRound(match);
        Check(match.Round == 2 && match.Units[2].Cell.x > 5 && match.Units[3].Cell.x > 5, "Opponents finish their turns on their own side of a continuous wall.");

        // A second match takes a fresh snapshot after an obstacle is removed.
        var open = new SparMatch(terrain, deployment);
        Check(open.Move(open.Units[0], new Vector2Int(6, 2)), "A newly opened gap is passable on the next visit.");

        VerifyFencedEdge();
        VerifyBlockedShot();

        deployment[1] = deployment[0];
        try
        {
            new SparMatch(terrain, deployment);
        }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Overlapping starting cells were accepted.");
    }

    static void VerifyFencedEdge()
    {
        var terrain = new bool[6, 4];
        var deployment = new[]
        {
            new Vector2Int(1, 1), new Vector2Int(0, 3), new Vector2Int(5, 1), new Vector2Int(5, 3)
        };
        var left = deployment[0];
        var right = left + Vector2Int.right;
        Func<Vector2Int, Vector2Int, bool> canTraverse = (from, to) =>
            !((from == left && to == right) || (from == right && to == left));

        // The two cells next to the fence are individually clear. A detour around one
        // end costs three steps. One further cell exceeds this unit's allowance.
        var fenced = new SparMatch(terrain, deployment, canTraverse);
        Check(fenced.CanMove(fenced.Units[0], right), "An open route around a fenced edge is usable.");
        Check(!fenced.CanMove(fenced.Units[0], right + Vector2Int.right), "An intervening fence charges the full detour.");
        Check(fenced.Move(fenced.Units[0], right), "Move across the gap by walking around the fence.");
        var route = fenced.Units[0].WalkPath;
        Check(route.Count == 4 && route[0] == left && route[3] == right, "Recorded route includes both ends of the three-step detour.");
        for (int i = 1; i < route.Count; i++)
            Check(canTraverse(route[i - 1], route[i]) && (route[i] - route[i - 1]).sqrMagnitude == 1,
                "Only legal neighbouring cells are sent to the movement animation.");
        var clear = new SparMatch(terrain, deployment);
        Check(clear.CanMove(clear.Units[0], right + Vector2Int.right), "Without the fence the shorter route costs two steps.");
    }

    static void VerifyBlockedShot()
    {
        var terrain = new bool[4, 3];
        var cells = new[]
        {
            new Vector2Int(0, 0), new Vector2Int(0, 2), new Vector2Int(2, 0), new Vector2Int(2, 2)
        };
        var open = new SparMatch(terrain, cells);
        Check(open.CanAttack(open.Units[0], cells[2]), "An opponent two squares away is normally in range.");
        var occluded = new SparMatch(terrain, cells, clearShot: (a, b) => false);
        Check(!occluded.Attack(occluded.Units[0], cells[2]) && !occluded.Units[0].Acted,
            "A blocked spell cannot hit or consume the caster's action.");
        FinishRound(occluded);
        Check(occluded.Units[0].Hp == occluded.Units[0].MaxHp && occluded.Units[1].Hp == occluded.Units[1].MaxHp,
            "Opponents respect the same blocked lines of sight.");
    }

    static void FinishRound(SparMatch match)
    {
        match.EndTurn();
        for (int step = 0; step < match.Units.Count && match.Status == SparStatus.OpponentTurn; step++)
            match.StepOpponent();
        Check(match.Status != SparStatus.OpponentTurn, "Opponent phase finishes.");
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
