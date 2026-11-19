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
        VerifySplitMovement();
        VerifyCommonAttackBudget();
        VerifyAutonomousActions();
        VerifySynchronousStatusOpening();
        VerifyBattleKit();
        var game = new SparMatch();
        var pupil = game.Units[0];
        var puppet = game.Units[1];
        var disciple = game.Units[2];
        var guard = game.Units[3];
        var initialCell = pupil.Cell;

        Check(!game.Move(pupil, new Vector2Int(3, 1)), "Stone blocks movement.");
        Check(game.PreviewPath(pupil, new Vector2Int(4, 1)).Count == 6, "The detour around the stone costs five actual steps.");
        Check(!game.Move(pupil, puppet.Cell), "An occupied square is impassable.");
        Check(!game.Move(pupil, new Vector2Int(-1, 1)), "Board boundaries are enforced.");
        Check(!game.CanMove(new SparMatch().Units[0], new Vector2Int(1, 2)), "Unit belongs to another match.");
        Check(!game.Attack(pupil, disciple.Cell) && !game.Attack(pupil, puppet.Cell), "Range and factions apply.");
        Check(game.Move(pupil, new Vector2Int(2, 1)), "Pupil can move.");
        Check(pupil.ActionPoints == 5 && game.CanMove(pupil, initialCell), "One step leaves five points for the turn.");
        Check(!game.Move(puppet, new Vector2Int(2, 4)), "An allied puppet must wait for its own action.");
        game.EndTurn();
        Check(game.ActiveUnit == disciple && !game.Attack(pupil, disciple.Cell), "Enemy initiative locks player input.");
        game.StepOpponent();
        Check(game.ActiveUnit == guard && guard.TurnsStarted == 1, "Exactly one AI actor resolves per step.");
        game.StepOpponent();
        Check(game.ActiveUnit == puppet && game.Move(puppet, new Vector2Int(2, 4)), "Puppet has its own movement budget.");
        game.EndTurn();
        Check(game.ActiveUnit == pupil && pupil.TurnsStarted == 2 && !pupil.Moved && !pupil.Acted,
            "Returning to pupil refreshes that unit's actions.");

        VerifyVictory();
        var passive = new SparMatch();
        for (int i = 0; i < 120 && IsPlaying(passive); i++) PassActive(passive);
        Check(passive.Status == SparStatus.Lost, "Passing every action can end in defeat.");

        var replay = new SparMatch();
        Check(replay.TurnNumber == 1 && replay.Units[0].Hp == replay.Units[0].MaxHp, "Replay starts with fresh participants.");
        Check(replay.Journal.Count == 1 && replay.Journal[0].Actions.Count == 0, "Replay starts with an empty combat journal.");
        replay.Yield();
        replay.StepOpponent();
        Check(replay.Status == SparStatus.Yielded && !replay.CanMove(replay.Units[0], new Vector2Int(1, 2)), "Concession ends the match.");
    }

    static void VerifyBattleKit()
    {
        var cells = new[] { new Vector2Int(1, 1), Vector2Int.zero, new Vector2Int(3, 1), new Vector2Int(5, 4) };
        var pill = FirstVolumeItems.Find(FirstVolumeItems.SpringPill);
        var seal = FirstVolumeItems.Find(FirstVolumeItems.RoadSeal);
        var relic = FirstVolumeItems.Find(FirstVolumeItems.SpiritPendant).GrantedSpell;
        var carried = new[] { pill, seal };
        var practice = new SparMatch(new bool[6, 5], cells, canTraverse: (_, _) => false);
        var hero = practice.Units[0];
        int payments = 0;
        practice.ConfigurePlayerLoadout(SpiritRoots.FireEarth, QingxiArts.EmberManual,
            new[] { QingxiArts.Firebolt, QingxiArts.Pulse }, 0, relic);
        practice.ConfigureCarried(carried, (_, _) => { payments++; return false; });
        Check(practice.PlayerSpells.Count == 3 && practice.IsRelicSpell(relic) &&
            practice.SelectPlayerSpell(hero, relic) && !practice.CanUseCarried(hero, 0) && !practice.CanUseCarried(hero, 1),
            "A relic adds its art after both learned slots. An unwounded fighter or a sparring-only target cannot use these carried items.");
        Check(practice.PreviewAttackDamage(hero, cells[2]) == hero.Power + relic.ExtraPower &&
            hero.Mana == hero.MaxMana && practice.Attack(hero, cells[2]) &&
            hero.ActionPoints == hero.MaxActionPoints && hero.Mana == hero.MaxMana - relic.ManaCost &&
            practice.Journal[0].Actions[0].Name.Contains("法宝"),
            "A relic preview spends nothing. Committing the cast spends only its mana and spirit, with a source-labelled result.");
        Check(!practice.SelectPlayerSpell(hero, relic), "A relic spell enters its personal cooldown.");

        practice.EndTurn();
        Check(practice.TryPlanOpponentAction(out var strike) && practice.ExecuteOpponentAction(strike),
            "A natural strike gives the player a wound.");
        practice.FinishOpponentTurn();
        for (int i = 0; i < 12 && !practice.CanCommand(hero); i++) PassActive(practice);
        int hp = hero.Hp;
        int ap = hero.ActionPoints;
        Check(hp < hero.MaxHp && hero.MaxHp - hp < pill.CarryHeal && !practice.SelectPlayerSpell(hero, relic),
            "The test leaves a small wound when the fighter's next personal action begins. The relic is still cooling.");
        Check(practice.UseCarried(hero, 0) && !practice.UseCarried(hero, 0) &&
            practice.Carried(0) == null && practice.Carried(1) == seal && payments == 0 &&
            hero.Hp == hero.MaxHp && hero.ActionPoints == ap - pill.CarryActionCost &&
            practice.LastHealthChanges[0].Amount == hero.MaxHp - hp,
            "A practice dose removes one local item and records the exact capped healing without paying from the real bag.");

        var field = new SparMatch(new bool[6, 5], cells, canTraverse: (_, _) => false, party: FieldParty.Solo);
        hero = field.Units[0];
        bool canPay = false;
        field.ConfigureCarried(new[] { pill, pill }, (slot, id) =>
        {
            if (!canPay || slot != 1 || id != pill.Id) return false;
            payments++;
            return true;
        });
        field.EndTurn();
        for (int i = 0; i < 12 && !field.CanCommand(hero); i++) PassActive(field);
        hp = hero.Hp;
        ap = hero.ActionPoints;
        Check(hp < hero.MaxHp && field.CanUseCarried(hero, 1) &&
            !field.UseCarried(hero, 1) && field.Carried(1) == pill && hero.Hp == hp && hero.ActionPoints == ap,
            "A rejected backing-bag withdrawal does not heal, spend an action or empty the slot.");
        canPay = true;
        Check(field.UseCarried(hero, 1) && !field.UseCarried(hero, 1) && payments == 1 &&
            field.Carried(0) == pill && field.Carried(1) == null && hero.ActionPoints == ap - pill.CarryActionCost,
            "A field dose consumes exactly the confirmed slot, even when both slots contain the same item.");

        var ambush = new SparMatch(new bool[6, 5], cells, clearShot: (_, _) => false, party: FieldParty.Brigands,
            preparedWard: true, studiedWard: true);
        hero = ambush.Units[0];
        ambush.ConfigureCarried(carried, (slot, id) => slot == 1 && id == seal.Id);
        for (int i = 0; i < 12 && !ambush.CanCommand(hero); i++) PassActive(ambush);
        Check(ambush.FindCarriedSlot(seal.Id) == 1 && !ambush.UseFieldWard(hero) &&
            ambush.UseCarried(hero, 1) && ambush.FormationBroken && ambush.WardReleased && ambush.Carried(1) == null &&
            ambush.LastHealthChanges[0].Amount == -13,
            "The encounter ward can only enter the supported field path through its prepared slot, with its existing effect.");
    }

    static void VerifySynchronousStatusOpening()
    {
        var cells = new[] { Vector2Int.zero, new Vector2Int(0, 4), new Vector2Int(0, 3), new Vector2Int(4, 0) };
        var fight = new SparMatch(new bool[5, 5], cells, canTraverse: (from, to) => false,
            clearShot: (from, to) => from == cells[3] && to == cells[0]);
        var hero = fight.Units[0];
        var puppet = fight.Units[1];
        var fire = QingxiArts.Firebolt;
        var ward = QingxiArts.Ward;
        fight.ConfigurePlayerLoadout(SpiritRoots.FireEarth, QingxiArts.EmberManual, new[] { ward }, 0);
        fight.ConfigureUnitLoadout(fight.Units[3], SpiritRoots.FireEarth, null, new[] { fire }, 0);
        Check(fight.SelectPlayerSpell(hero, ward) && fight.Attack(hero, hero.Cell), "The hero can ward their own tile without a clear shot.");
        fight.EndTurn();
        fight.StepOpponent();
        fight.StepOpponent();

        int damageAfterWard = hero.MaxHp - hero.Hp;
        Check(fight.ActiveUnit == puppet && fight.WardStatusFor(hero).Remaining == ward.EffectTurns - 1 &&
            fight.BurnStatusFor(hero).Amount == fire.EffectAmount && fight.BurnStatusFor(hero).Remaining == fire.EffectTurns,
            "The enemy's direct impact consumes one ward charge and leaves its unmodified burn pending on the same hero.");
        fight.EndTurn();
        var tick = fight.Journal[fight.TurnNumber - 1].Actions[0];
        Check(fight.CanCommand(hero) && !fight.OpeningPending && !fight.ResolveTurnOpening() &&
            hero.Hp == hero.MaxHp - damageAfterWard - fire.EffectAmount &&
            fight.WardStatusFor(hero).Remaining == ward.EffectTurns - 1 && fight.BurnStatusFor(hero).Remaining == fire.EffectTurns - 1 &&
            tick.StatusEvents[0].Kind == SparStatusEventKind.BurnTick &&
            tick.HealthChanges[0].Amount == -fire.EffectAmount,
            "Synchronous turns resolve a burn before command, exactly once and without spending the remaining ward charge.");

        fight.EndTurn();
        fight.StepOpponent();
        fight.StepOpponent();
        fight.EndTurn();
        var finalTick = fight.Journal[fight.TurnNumber - 1].Actions[0];
        Check(fight.CanCommand(hero) && hero.Hp == hero.MaxHp - damageAfterWard - 2 * fire.EffectAmount &&
            fight.BurnStatusFor(hero).Remaining == 0 && fight.WardStatusFor(hero).Remaining == ward.EffectTurns - 1 &&
            finalTick.StatusEvents[0].Amount == fire.EffectAmount && finalTick.StatusEvents[0].Remaining == 0,
            "The second personal opening exhausts burn without spending a ward charge.");

        var lethal = new SparMatch(new bool[5, 5], cells, canTraverse: (from, to) => false);
        hero = lethal.Units[0];
        var rival = lethal.Units[2];
        lethal.ConfigurePlayerLoadout(SpiritRoots.FireEarth, QingxiArts.EmberManual, new[] { fire }, 0);
        Check(lethal.SelectPlayerSpell(hero, fire) && lethal.Attack(hero, rival.Cell) && rival.Alive, "The direct spell leaves the target for its next opening.");
        lethal.EndTurn();
        Check(!rival.Alive && lethal.ActiveUnit == lethal.Units[3] && lethal.Journal[1].Actor == rival &&
            lethal.Journal[1].Actions[0].HealthChanges[0].Amount == -(rival.MaxHp - hero.Power - fire.ExtraPower),
            "A lethal headless status retains its own journal turn and advances directly past its defeated actor.");
    }

    static void VerifySplitMovement()
    {
        var cells = new[] { new Vector2Int(1, 1), Vector2Int.zero, new Vector2Int(3, 1), new Vector2Int(5, 3) };
        var match = new SparMatch(new bool[6, 4], cells);
        var hero = match.ActiveUnit;
        var opening = match.Journal[0];
        var ahead = cells[0] + Vector2Int.right;
        Check(hero.MaxActionPoints == 6 && match.Move(hero, ahead) && hero.ActionPoints == 5, "First segment costs one.");
        Check(match.PreviewPath(hero, cells[0]).Count == 2 && hero.ActionPoints == 5, "Preview has no cost.");
        Check(match.Attack(hero, cells[2]) && hero.MovementLeft == 3, "A cast deducts two from the same pool as movement.");
        Check(!match.Move(hero, new Vector2Int(5, 0)) && hero.ActionPoints == 3, "Rejected movement has no cost.");
        Check(match.Move(hero, cells[0]) && match.Move(hero, cells[0] + Vector2Int.up) && hero.ActionPoints == 1,
            "Movement can be confirmed in segments after striking.");
        Check(!match.Attack(hero, cells[2]) && match.Move(hero, cells[0] + 2 * Vector2Int.up) && hero.ActionPoints == 0,
            "One point still permits a step but cannot pay for a strike.");
        Check(!match.Move(hero, cells[0]), "Movement stops when the shared pool is empty.");
        Check(match.Journal.Count == 1 && opening.Actor == hero && opening.Steps == 4 && opening.Actions.Count == 5 &&
            opening.Actions[0].Steps == 1 && opening.Actions[1].Name == "常击" && opening.Actions[2].Steps == 1,
            "The journal preserves the order of actual walk segments and strikes and omits previews and rejected actions.");
        ReturnToPupil(match);
        Check(hero.MovementLeft == hero.MaxActionPoints && !hero.Acted, "The next personal turn restores the shared budget.");
    }

    static void VerifyAutonomousActions()
    {
        var cells = new[] { new Vector2Int(1, 1), new Vector2Int(1, 4), new Vector2Int(3, 1), new Vector2Int(6, 4) };
        var fight = new SparMatch(new bool[7, 5], cells);
        var hero = fight.Units[0];
        var rival = fight.Units[2];
        fight.EndTurn();
        int turn = fight.TurnNumber;
        fight.StepOpponent();
        Check(fight.TurnNumber == turn + 1 && rival.ActionPoints == 1 && hero.Hp == hero.MaxHp - 2 * rival.Power,
            "An opponent can strike twice during a single personal turn and stop with one unusable point.");

        var farCells = new[] { Vector2Int.zero, Vector2Int.up, new Vector2Int(11, 0), new Vector2Int(12, 2) };
        var advance = new SparMatch(new bool[13, 3], farCells);
        rival = advance.Units[2];
        advance.EndTurn();
        advance.StepOpponent();
        Check(rival.Cell == new Vector2Int(6, 0) && rival.ActionPoints == 0 &&
            advance.Units[0].Hp == advance.Units[0].MaxHp,
            "An opponent can spend its whole movement allowance on a route with no attack this turn.");

        var occluded = new SparMatch(new bool[7, 5], cells, clearShot: (from, to) => false);
        rival = occluded.Units[2];
        occluded.EndTurn();
        occluded.StepOpponent();
        Check(rival.Cell == cells[2] && rival.ActionPoints == rival.MaxActionPoints,
            "An opponent can finish its turn with unused points if no movement would create a firing position.");

        var spellCells = new[] { Vector2Int.zero, new Vector2Int(0, 7), new Vector2Int(12, 7), new Vector2Int(9, 0) };
        var casterFight = new SparMatch(new bool[13, 8], spellCells, clearShot: (from, to) => to == spellCells[0]);
        var guard = casterFight.Units[3];
        var vine = QingxiArts.Vine;
        casterFight.ConfigureUnitLoadout(guard, SpiritRoots.For(true), QingxiArts.SpringManual, new[] { vine }, 0);
        casterFight.EndTurn();
        casterFight.StepOpponent();
        var target = casterFight.Units[0];
        var initialChargeTime = casterFight.CombatTime;
        int initialTurn = casterFight.TurnNumber;
        int wood = casterFight.DaoCang(guard, WuXing.灵木);
        var guardTurn = casterFight.Journal[initialTurn - 1];
        Check(casterFight.TryPlanOpponentAction(out var move) && move.Kind == SparActionKind.Move &&
            move.Destination == new Vector2Int(3, 0) && guard.Cell == spellCells[3] && guard.ActionPoints == guard.MaxActionPoints &&
            guardTurn.Actions.Count == 0,
            "Planning the walk never spends action points or moves the actor.");
        Check(casterFight.ExecuteOpponentAction(move) && guard.ActionPoints == 0 && guard.Cell == move.Destination &&
            guard.WalkPath.Count == 7 && target.Hp == target.MaxHp && guard.Mana == guard.MaxMana &&
            casterFight.LastHealthChanges.Count == 0 && !casterFight.ExecuteOpponentAction(move),
            "A discrete walk only spends movement, uses its own path, and cannot replay the same plan.");
        Check(casterFight.TryPlanOpponentAction(out var spell) && spell.Kind == SparActionKind.Spell &&
            spell.Target == target && spell.Destination == target.Cell && spell.Spell == vine &&
            casterFight.DaoCang(guard, WuXing.灵木) == wood && guard.Mana == guard.MaxMana && target.Hp == target.MaxHp &&
            guardTurn.Actions.Count == 1,
            "Telegraphing a zero-point spell after movement does not commit damage or costs.");
        Check(casterFight.ExecuteOpponentAction(spell) && guard.Mana == guard.MaxMana - vine.ManaCost &&
            casterFight.DaoCang(guard, WuXing.灵木) == wood - vine.FixedCost(WuXing.灵木) &&
            target.Hp == target.MaxHp - guard.Power && casterFight.LastHealthChanges.Count == 1 &&
            casterFight.LastHealthChanges[0].Target == target && casterFight.LastHealthChanges[0].Amount == -guard.Power &&
            casterFight.LastHealthChanges[0].Spell == vine && !casterFight.ExecuteOpponentAction(spell),
            "Impact spends the spell once and reports actual damage and the affected unit.");
        Check(casterFight.TryPlanOpponentAction(out var hold) && hold.Kind == SparActionKind.Hold &&
            casterFight.TurnNumber == initialTurn && casterFight.CombatTime == initialChargeTime &&
            casterFight.ActiveUnit == guard && casterFight.ChargeAt(guard, initialChargeTime) == 1f,
            "The opponent may hold remaining mana and the initiative stays frozen through impact.");
        casterFight.FinishOpponentTurn();
        Check(casterFight.TurnNumber == initialTurn + 1 && casterFight.ActiveUnit != guard &&
            !casterFight.ExecuteOpponentAction(spell), "Only finishing advances initiative; old casts cannot carry over.");
        Check(casterFight.Journal.Count == initialTurn + 1 && guardTurn.Actor == guard && guardTurn.Steps == 6 &&
            guardTurn.Actions.Count == 2 && guardTurn.Actions[0].Steps == 6 && guardTurn.Actions[1].Name == vine.Name &&
            guardTurn.Actions[1].HealthChanges[0].Amount == -guard.Power,
            "The enemy walk and named spell impact are saved in the same personal turn.");
    }

    static void VerifyCommonAttackBudget()
    {
        var cells = new[] { new Vector2Int(1, 1), Vector2Int.zero, new Vector2Int(1, 0), new Vector2Int(3, 1) };
        var match = new SparMatch(new bool[4, 2], cells);
        var hero = match.ActiveUnit;
        int turn = match.TurnNumber;
        Check(match.Attack(hero, cells[2]) && match.Attack(hero, cells[2]) && match.Units[2].Hp == 1,
            "Two ordinary strikes are legal in one turn when the player can afford them.");
        Check(match.Attack(hero, cells[3]) && hero.ActionPoints == 0 && match.TurnNumber == turn,
            "Three strikes consume the budget without advancing initiative.");
        Check(!match.Attack(hero, cells[3]) && match.Units[3].Hp == 7, "An attempted fourth strike cannot damage an alive target.");
    }

    static void VerifyVictory()
    {
        // Fix all feet in place to verify alternating direct/AI casts independently of pathfinding.
        var cells = new[] { new Vector2Int(1, 1), Vector2Int.zero, new Vector2Int(1, 0), new Vector2Int(3, 1) };
        var match = new SparMatch(new bool[4, 2], cells, canTraverse: (a, b) => false);
        var disciple = match.Units[2];
        var guard = match.Units[3];
        int casts = 0;
        for (int i = 0; i < 60 && IsPlaying(match); i++)
        {
            var actor = match.ActiveUnit;
            if (match.Status == SparStatus.PlayerTurn)
            {
                var target = disciple.Alive ? disciple : guard;
                if (match.CanAttack(actor, target.Cell))
                {
                    Check(match.Attack(actor, target.Cell), "A legal strike resolves in the current personal turn.");
                    casts++;
                }
            }
            if (IsPlaying(match)) PassActive(match);
        }
        Check(casts >= 4 && !disciple.Alive && match.At(disciple.Cell) == null, "Withdrawn units free their square.");
        Check(match.Status == SparStatus.Won, "A coordinated pair can win the match.");
        match.Yield();
        Check(match.Status == SparStatus.Won, "Terminal state cannot be overridden.");
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
        ReturnToPupil(match);
        Check(match.Units[0].TurnsStarted == 2 && match.Units[2].Cell.x > 5 && match.Units[3].Cell.x > 5, "Opponents finish their turns on their own side of a continuous wall.");

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

        // The two cells next to the fence are individually clear, but a detour costs three steps.
        var fenced = new SparMatch(terrain, deployment, canTraverse);
        Check(fenced.CanMove(fenced.Units[0], right), "An open route around a fenced edge is usable.");
        Check(fenced.Move(fenced.Units[0], right), "Move across the gap by walking around the fence.");
        Check(fenced.Units[0].ActionPoints == 3, "The entire three-step detour is paid even though the destination is adjacent.");
        var route = fenced.Units[0].WalkPath;
        Check(route.Count == 4 && route[0] == left && route[3] == right, "Recorded route includes both ends of the three-step detour.");
        for (int i = 1; i < route.Count; i++)
            Check(canTraverse(route[i - 1], route[i]) && (route[i] - route[i - 1]).sqrMagnitude == 1,
                "Only legal neighbouring cells are sent to the movement animation.");
        Check(!fenced.CanMove(fenced.Units[0], Vector2Int.left + left),
            "A four-step detour is unavailable with three points left, even when the destination looks close.");
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
        ReturnToPupil(occluded);
        Check(occluded.Units[0].Hp == occluded.Units[0].MaxHp && occluded.Units[1].Hp == occluded.Units[1].MaxHp,
            "Opponents respect the same blocked lines of sight.");
    }

    static bool IsPlaying(SparMatch match) => match.Status == SparStatus.PlayerTurn || match.Status == SparStatus.OpponentTurn;

    static void PassActive(SparMatch match)
    {
        if (match.Status == SparStatus.OpponentTurn) match.StepOpponent();
        else match.EndTurn();
    }

    static void ReturnToPupil(SparMatch match)
    {
        match.EndTurn();
        for (int i = 0; i < 12 && IsPlaying(match) && match.ActiveUnit != match.Units[0]; i++) PassActive(match);
        Check(match.CanCommand(match.Units[0]), "The terrain test returns to a new action for the pupil.");
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
