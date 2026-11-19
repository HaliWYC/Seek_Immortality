using System;
using System.Collections.Generic;

public sealed class SparJournalEntry
{
    public string Name { get; }
    public int Steps { get; }
    public SparUnit Target { get; }
    public IReadOnlyList<SparHealthChange> HealthChanges { get; }
    public IReadOnlyList<SparStatusEvent> StatusEvents { get; }

    internal SparJournalEntry(int steps)
    {
        Name = "移步";
        Steps = steps;
        HealthChanges = Array.Empty<SparHealthChange>();
        StatusEvents = Array.Empty<SparStatusEvent>();
    }

    internal SparJournalEntry(string name, SparUnit target, List<SparHealthChange> changes,
        List<SparStatusEvent> statusEvents)
    {
        Name = name;
        Target = target;
        HealthChanges = Array.AsReadOnly(changes.ToArray());
        StatusEvents = statusEvents.Count == 0 ? Array.Empty<SparStatusEvent>() : Array.AsReadOnly(statusEvents.ToArray());
    }
}

public sealed class SparJournalTurn
{
    readonly List<SparJournalEntry> actions = new List<SparJournalEntry>();

    public int Number { get; }
    public SparUnit Actor { get; }
    public int Steps { get; private set; }
    public IReadOnlyList<SparJournalEntry> Actions { get; }

    internal SparJournalTurn(int number, SparUnit actor)
    {
        Number = number;
        Actor = actor;
        Actions = actions.AsReadOnly();
    }

    internal void Add(SparJournalEntry action)
    {
        actions.Add(action);
        Steps += action.Steps;
    }
}

public sealed partial class SparMatch
{
    readonly List<SparJournalTurn> journal = new List<SparJournalTurn>();
    SparJournalTurn journalTurn;

    public IReadOnlyList<SparJournalTurn> Journal { get; }

    void StartJournalTurn()
    {
        journalTurn = new SparJournalTurn(TurnNumber, ActiveUnit);
        journal.Add(journalTurn);
    }

    void RecordJournalMove(int steps) => journalTurn.Add(new SparJournalEntry(steps));

    void RecordJournalAction(string name, SparUnit target) =>
        journalTurn.Add(new SparJournalEntry(name, target, healthChanges, statusEvents));
}
