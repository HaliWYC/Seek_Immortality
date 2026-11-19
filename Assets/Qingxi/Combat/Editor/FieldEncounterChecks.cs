using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

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
        VerifyWholeMap();
        var solo = NewFight(FieldParty.Solo);
        Check(solo.Units.Count == 3 && solo.Companion == null, "独行仅有本人及木傀。");
        solo.Yield();
        Check(solo.Status == SparStatus.Yielded, "可无伤撤走。");

        var ally = NewFight(FieldParty.Ally);
        var escort = ally.Companion;
        WaitUntil(ally, () => ally.ActiveUnit == escort, "友军应有独立行动时机。");
        Check(ally.Status == SparStatus.OpponentTurn && !ally.CanCommand(escort) &&
            !ally.Attack(escort, ally.Units[2].Cell), "已到友军的行动时机，玩家仍不能代为出招。");
        int beastHealth = ally.Units[2].Hp;
        ally.StepOpponent();
        Check(ally.Units[2].Hp < beastHealth, "友军自行施术。");

        var contract = NewFight(FieldParty.Contract);
        var partner = contract.Companion;
        WaitUntil(contract, () => contract.ActiveUnit == partner, "契友轮到自己时可指挥。");
        Check(contract.CanCommand(partner) && !contract.CanCommand(contract.Units[0]), "指令权仅属于此刻的契友。");
        WaitUntil(contract, () => contract.ActiveUnit == partner && partner.Hp < partner.MaxHp, "遇袭后契友仍有机会用药。");
        Check(contract.UseAgreedHerb(partner) && !contract.UseAgreedHerb(partner) &&
            !contract.TalismanUsed, "依约只能用一包止血散，护符由本人保留。");
        Check(contract.LastHealthChanges.Count == 1 && contract.LastHealthChanges[0].Target == partner &&
            contract.LastHealthChanges[0].Amount > 0 && contract.LastHealthChanges[0].Spell == null,
            "显示止血散带来的实际回复，重复用药失败后不增发回复。");

        var rescue = NewFight(FieldParty.Contract);
        WaitForWound(rescue);
        Check(rescue.TalismanUsed && rescue.RescueRounds == 2 && rescue.At(rescue.Companion.Cell) == rescue.Companion,
            "护符自行发动，倒地后保留两个主角行动机会。");
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
        var hero = expiry.Units[0];
        WaitUntil(expiry, () => expiry.ActiveUnit == hero, "伤者倒地后等到本人第一次行动。");
        Check(expiry.RescueRounds == 2, "木傀、友军和妖兽的行动不扣救援时限。");
        expiry.EndTurn();
        Check(expiry.RescueRounds == 1 && !expiry.CompanionDied, "主角第一次收势后仍能救援。");
        WaitUntil(expiry, () => expiry.ActiveUnit == hero, "伤者应撑到第二次本人行动。");
        Check(expiry.RescueRounds == 1, "主角第二次行动前仍剩最后一息。");
        expiry.EndTurn();
        Check(expiry.CompanionDied && expiry.RescueRounds == 0, "再次放弃救援则同伴身亡。");

        var victory = NewFight(FieldParty.Contract);
        victory.PreparePlayerSpell(QingxiArts.Firebolt);
        FightToEnd(victory);
        Check(victory.Status == SparStatus.Won && !victory.TalismanUsed && !victory.CompanionDied,
            "轮流施术能在同伴耗去护符前得胜。");
        VerifyBrigands();
    }

    static void VerifyWholeMap()
    {
        // Distant fixture avoids the colliders in whichever scene is already open.
        var origin = new Vector2Int(250, -50);
        var world = new GameObject("全图战棋验证", typeof(Grid));
        var tile = ScriptableObject.CreateInstance<Tile>();
        try
        {
            var grid = world.GetComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = QingxiGridMetric.TilemapCellSize;
            var surfaceObject = new GameObject("测试地面", typeof(Tilemap), typeof(QingxiTacticalGround));
            surfaceObject.transform.SetParent(world.transform, false);
            var tiles = surfaceObject.GetComponent<Tilemap>();
            tiles.tileAnchor = Vector3.zero;
            var lake = origin + new Vector2Int(14, 3);
            Vector2 low = QingxiGridMetric.Offset(origin);
            Vector2 high = low;
            for (int x = 0; x < 34; x++)
            for (int y = 0; y < 6; y++)
            {
                var absolute = origin + new Vector2Int(x, y);
                if (absolute != lake) tiles.SetTile((Vector3Int)absolute, tile);
                Vector2 point = QingxiGridMetric.Offset(absolute);
                low = Vector2.Min(low, point);
                high = Vector2.Max(high, point);
            }
            var authoring = new SerializedObject(surfaceObject.GetComponent<QingxiTacticalGround>());
            authoring.FindProperty("tiles").objectReferenceValue = tiles;
            authoring.FindProperty("worldBounds").rectValue =
                Rect.MinMaxRect(low.x - 0.1f, low.y - 0.1f, high.x + 0.1f, high.y + 0.1f);
            authoring.ApplyModifiedPropertiesWithoutUndo();

            var rockCell = origin + new Vector2Int(11, 3);
            var rock = new GameObject("测试山岩", typeof(BoxCollider2D));
            rock.transform.SetParent(world.transform, false);
            rock.transform.position = QingxiGridMetric.Offset(rockCell) + Vector2.up * 0.2f;
            rock.GetComponent<BoxCollider2D>().size = new Vector2(0.42f, 0.42f);
            var heroCell = origin + new Vector2Int(4, 1);
            var actor = new GameObject("测试棋手", typeof(Rigidbody2D));
            actor.transform.SetParent(world.transform, false);
            actor.transform.position = QingxiGridMetric.Offset(heroCell);
            var board = QingxiBattlefield.Sample(actor.GetComponent<Rigidbody2D>(),
                QingxiGridMetric.Offset(origin + new Vector2Int(6, 4)));
            Check(board != null && board.Match.Columns > 13 &&
                board.MapBounds.size.x > board.ViewBounds.size.x * 3, "战场使用整张地面地图，起手画幅仍围绕参战者。");

            Vector2Int Relative(Vector2Int cell) => QingxiGridMetric.Index(QingxiGridMetric.Offset(cell) - board.Origin);
            var match = board.Match;
            var hero = match.Units[0];
            var distant = Relative(origin + new Vector2Int(22, 1));
            Check(hero.Cell == Relative(heroCell) && !match.IsBlocked(distant) && !match.CanMove(hero, distant),
                "旧十三格之外仍是合法地形，不能在一次本人行动里超支跨越。");
            Check(match.IsBlocked(Relative(lake)) && match.IsBlocked(Relative(rockCell)) &&
                match.IsBlocked(Relative(origin + new Vector2Int(34, 1))) &&
                !match.HasLineOfSight(Relative(rockCell + Vector2Int.left), Relative(rockCell + Vector2Int.right)),
                "缺地、山岩和边缘都不可落脚，实体山岩仍阻挡术法。");
            for (int x = 10; x <= 22; x += 6)
            {
                var next = Relative(origin + new Vector2Int(x, 1));
                Check(match.Move(hero, next) && hero.ActionPoints == 0 &&
                    !match.CanMove(hero, next + Vector2Int.right), "每次本人行动至多走六格。");
                match.EndTurn();
                for (int turn = 0; turn < 12 && !match.CanCommand(hero); turn++)
                {
                    if (match.Status == SparStatus.OpponentTurn) match.FinishOpponentTurn();
                    else match.EndTurn();
                }
                Check(match.CanCommand(hero), "离开起手阵位后仍可轮转行动。");
            }
            Check(hero.Cell == distant && board.ReplayPractice().Units[0].Cell == Relative(heroCell),
                "可以分回合走进远处地面，重试仍还原最初阵位。");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(world);
            UnityEngine.Object.DestroyImmediate(tile);
        }
    }

    static void VerifyBrigands()
    {
        var cells = new[] { new Vector2Int(1, 1), new Vector2Int(3, 1), new Vector2Int(3, 2), new Vector2Int(4, 2) };
        var unready = Duel(cells, false, false);
        Check(unready.ActiveUnit == unready.Units[2], "这次对阵由反应更快的筑基劫修先手。");
        WaitForHero(unready);
        var hero = unready.Units[0];
        var boss = unready.Units[2];
        Check(!unready.UseFieldWard(hero), "没有备符不能凭空破阵。");
        int hits = boss.Hp;
        Check(unready.Attack(hero, boss.Cell) && hits - boss.Hp == 3, "炼气后期的寻常术法被护阵削弱。");
        unready.Yield();
        Check(unready.Status == SparStatus.Yielded, "轮到本人仍可选择逃离。");

        var blind = Duel(cells, true, false);
        WaitForHero(blind);
        hits = blind.Units[2].Hp;
        Check(blind.UseFieldWard(blind.Units[0]) && hits - blind.Units[2].Hp == 6 && !blind.FormationBroken,
            "未验阵时灵符只能损六。");

        var plan = Duel(cells, true, true);
        plan.PreparePlayerSpell(QingxiArts.Firebolt);
        WaitForHero(plan);
        hits = plan.Units[2].Hp;
        Check(!plan.UseFieldWard(plan.Units[1]) && plan.UseFieldWard(plan.Units[0]) && !plan.UseFieldWard(plan.Units[0]) &&
            hits - plan.Units[2].Hp == 13 && plan.FormationBroken, "亲自看破阵眼，备符并在自己的行动中祭出。");
        FightToEnd(plan);
        Check(plan.Status == SparStatus.Won, "炼气后期看破阵眼，配合火弹术和木傀确可越阶击退筑基劫修。");

        var doomed = Duel(cells, false, false);
        WaitUntil(doomed, () => doomed.Status == SparStatus.Lost, "主动应战又执意留在阵中可致身亡。");
    }

    static SparMatch Duel(Vector2Int[] cells, bool ready, bool studied) =>
        new SparMatch(new bool[6, 5], cells, party: FieldParty.Brigands,
            preparedWard: ready, studiedWard: studied, heroAttributes: LateQi());

    static CultivatorAttributes LateQi() =>
        JsonUtility.FromJson<DemoCultivation>("{\"qi\":9}").Attributes(true);

    static SparMatch NewFight(FieldParty party, bool close = true)
    {
        var deployment = new[]
        {
            close ? new Vector2Int(2, 0) : new Vector2Int(0, 0),
            close ? new Vector2Int(3, 1) : new Vector2Int(0, 1),
            new Vector2Int(3, 2), new Vector2Int(2, 1)
        };
        return new SparMatch(new bool[6, 5], deployment, party: party, heroAttributes: LateQi());
    }

    static bool InProgress(SparMatch match) =>
        match.Status == SparStatus.PlayerTurn || match.Status == SparStatus.OpponentTurn;

    static void WaitForWound(SparMatch match) =>
        WaitUntil(match, () => match.RescueRounds > 0, "伤友须能在真实攻击流程中倒地。");

    static void WaitForHero(SparMatch match) =>
        WaitUntil(match, () => match.CanCommand(match.Units[0]), "主角应获得一次完整的行动机会。");

    static void WaitUntil(SparMatch match, Func<bool> reached, string label)
    {
        for (int i = 0; i < 100 && InProgress(match) && !reached(); i++) AdvanceActor(match);
        Check(reached(), label);
    }

    static void AdvanceActor(SparMatch match)
    {
        if (match.Status == SparStatus.PlayerTurn) match.EndTurn();
        else if (match.Status == SparStatus.OpponentTurn) match.StepOpponent();
    }

    static void FightToEnd(SparMatch match)
    {
        for (int i = 0; i < 120 && InProgress(match); i++)
        {
            if (match.Status == SparStatus.PlayerTurn)
            {
                var actor = match.ActiveUnit;
                if (match.CanSelectSpell(actor)) match.TogglePlayerSpell(actor);
                var enemy = match.Units[2].Alive ? match.Units[2] : match.Units[3];
                // Remove the lookout when one shot can buy the party an entire enemy action.
                if (match.Party == FieldParty.Brigands && match.SpellSelected)
                {
                    var lookout = match.Units[3];
                    if (lookout.Alive && lookout.Hp <= actor.Power + match.PlayerSpell.ExtraPower &&
                        match.CanAttack(actor, lookout.Cell)) enemy = lookout;
                }
                if (!actor.Acted && !match.CanAttack(actor, enemy.Cell))
                {
                    var best = actor.Cell;
                    int gap = Distance(best, enemy.Cell);
                    for (int x = 0; x < match.Columns; x++)
                    for (int y = 0; y < match.Rows; y++)
                    {
                        var candidate = new Vector2Int(x, y);
                        int next = Distance(candidate, enemy.Cell);
                        if (next >= gap || !match.CanMove(actor, candidate)) continue;
                        best = candidate;
                        gap = next;
                    }
                    match.Move(actor, best);
                }
                match.Attack(actor, enemy.Cell);
                while (match.Status == SparStatus.PlayerTurn && enemy.Alive && match.CanAttack(actor, enemy.Cell))
                    match.Attack(actor, enemy.Cell);
                if (match.Party == FieldParty.Brigands && actor == match.Units[1] &&
                    actor.Acted && !actor.Moved && enemy.Alive)
                    PullBackPuppet(match, actor, enemy);
            }
            AdvanceActor(match);
        }
    }

    static void PullBackPuppet(SparMatch match, SparUnit puppet, SparUnit enemy)
    {
        // Draw the wounded puppet out of casting range, close enough to strike again.
        for (int x = 0; x < match.Columns; x++)
        for (int y = 0; y < match.Rows; y++)
        {
            var cell = new Vector2Int(x, y);
            int gap = Distance(cell, enemy.Cell);
            if (gap > enemy.Range && gap <= puppet.Steps + puppet.Range && match.Move(puppet, cell)) return;
        }
    }

    static int Distance(Vector2Int from, Vector2Int to) => Math.Abs(from.x - to.x) + Math.Abs(from.y - to.y);

    static void Check(bool valid, string label)
    {
        if (!valid) throw new InvalidOperationException(label);
    }
}
