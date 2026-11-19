using System;
using System.Collections.Generic;

public readonly struct SparTurn
{
    public SparUnit Actor { get; }
    public double At { get; }
    internal SparTurn(SparUnit actor, int turn)
    {
        Actor = actor;
        At = turn * SparMatch.InitiativeThreshold / actor.Reaction;
    }
}

public sealed partial class SparMatch
{
    public const double InitiativeThreshold = 100d;
    public SparUnit ActiveUnit { get; private set; }
    public int TurnNumber { get; private set; }
    public double CombatTime { get; private set; }
    public double PreviousCombatTime { get; private set; }
    bool forecastsDirty = true;
    IReadOnlyList<SparTurn> upcoming;

    // The model has reserved the incoming actor's turn. Its bar stays full
    // while it acts, and resets as soon as the next actor starts charging.
    public float ChargeAt(SparUnit unit, double shownTime)
    {
        if (unit == null || !unit.Alive) return 0f;
        double time = Math.Max(PreviousCombatTime, Math.Min(CombatTime, shownTime));
        int completed = unit.TurnsStarted - (unit == ActiveUnit ? 1 : 0);
        return (float)Math.Max(0d, Math.Min(1d, time * unit.Reaction / InitiativeThreshold - completed));
    }

    // Compare (personal actions + 1) / reaction exactly. A tie keeps deployment
    // order. The preview and the actual turn use this same comparison.
    int Earliest(int[] projected)
    {
        int best = -1;
        for (int i = 0; i < units.Length; i++)
        {
            if (!units[i].Alive) continue;
            long next = 1L + (projected == null ? units[i].TurnsStarted : projected[i]);
            if (best < 0) { best = i; continue; }
            long rival = 1L + (projected == null ? units[best].TurnsStarted : projected[best]);
            if (next * units[best].Reaction < rival * units[i].Reaction) best = i;
        }
        return best;
    }

    void AdvanceInitiative()
    {
        do
        {
            int index = Earliest(null);
            if (index < 0) throw new InvalidOperationException("斗法尚未结算但场上已无人存活。");
            ActiveUnit = units[index];
            ActiveUnit.TurnsStarted++;
            PreviousCombatTime = CombatTime;
            CombatTime = ActiveUnit.TurnsStarted * InitiativeThreshold / ActiveUnit.Reaction;
            TurnNumber++;
            StartJournalTurn();
            ActiveUnit.ActionPoints = ActiveUnit.MaxActionPoints;
            ActiveUnit.StepsTaken = 0;
            ActiveUnit.Acted = false;
            OnArtsTurnStarted(ActiveUnit);
            Status = ActiveUnit.Direct ? SparStatus.PlayerTurn : SparStatus.OpponentTurn;
            ResetOpponentActions();
            forecastsDirty = true;
            OpeningPending = burns.ContainsKey(ActiveUnit);
            if (DeferTurnOpening) return;
            ResolveTurnOpening();
        } while (BattleRunning && !ActiveUnit.Alive);
    }

    public IReadOnlyList<SparTurn> Upcoming
    {
        get
        {
            if (Status != SparStatus.PlayerTurn && Status != SparStatus.OpponentTurn) return Array.Empty<SparTurn>();
            if (!forecastsDirty) return upcoming;
            var turns = new int[units.Length];
            for (int i = 0; i < units.Length; i++) turns[i] = units[i].TurnsStarted;
            var preview = new SparTurn[4];
            for (int i = 0; i < preview.Length; i++)
            {
                int index = Earliest(turns);
                preview[i] = new SparTurn(units[index], ++turns[index]);
            }
            upcoming = Array.AsReadOnly(preview);
            forecastsDirty = false;
            return upcoming;
        }
    }
}
