using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed partial class QingxiSparring : MonoBehaviour
{
    [SerializeField] SparBoard board;
    [SerializeField, Min(0.5f)] float conversationRange = 2.7f;
    [SerializeField, Min(0.1f)] float opponentInterval = 0.24f;
    [SerializeField, Min(0.1f)] float initiativeBreathsPerSecond = 6f;

    QingxiWalker walker;
    Rigidbody2D regionalBody;
    SparHud hud;
    Canvas trailCanvas;
    Camera view;
    Vector3 previousCameraPosition;
    SpriteRenderer guide;
    Collider2D guideBody;
    SpriteRenderer walkerSprite;
    bool guideWasVisible;
    bool walkerWasVisible;
    float previousSize;
    bool previousCanvasEnabled;
    bool nearby;
    bool confirmingYield;
    double nextOpponentAction;
    Vector2 lastMousePosition;
    Vector2Int cursor;
    SparUnit selected;
    string feedback;

    public SparMatch Match { get; private set; }
    public SparBoard Board => board;
    public Camera ViewCamera => view;
    public bool InMatch => Match != null;
    public double DisplayCombatTime { get; private set; }
    public bool ChargingInitiative => InMatch && !Finished && DisplayCombatTime < Match.CombatTime;
    public bool JournalOpen => hud && hud.JournalOpen;
    public bool AwaitingPlayer => InMatch && !PracticeMenuOpen && !JournalOpen && !Finished && !DemoRun.Modal && !confirmingYield &&
        !ChargingInitiative && !PlayingAction && !Match.OpeningPending && Match.Status == SparStatus.PlayerTurn;
    public bool CanChallenge => !InMatch && !InEncounterOffer && !DemoRun.Modal && walker != null && walker.CanUseExplorationInput &&
        Vector2.Distance(walker.transform.position, transform.position) <= conversationRange;
    public bool ConfirmingYield => confirmingYield;
    public string BattlePlace { get; private set; }
    public SparUnit Selected => selected;
    public Vector2Int Cursor => cursor;
    public bool Finished => InMatch && (Match.Status == SparStatus.Won ||
        Match.Status == SparStatus.Lost || Match.Status == SparStatus.Yielded);

    void Start()
    {
        if (regionalBody) return;
        walker = FindAnyObjectByType<QingxiWalker>();
        hud = FindAnyObjectByType<SparHud>();
        var trailHud = FindAnyObjectByType<QingxiTrailHud>();
        if (trailHud != null) trailCanvas = trailHud.GetComponent<Canvas>();
        view = walker.FollowCamera;
        guide = GetComponent<SpriteRenderer>();
        guideBody = GetComponent<Collider2D>();
        walkerSprite = walker.GetComponentInChildren<SpriteRenderer>();
        hud.Bind(this);
        UpdateProximity();
    }

    public void BindRegional(Rigidbody2D actor, SpriteRenderer sprite, SparBoard fieldBoard, SparHud fieldHud)
    {
        regionalBody = actor;
        walkerSprite = sprite;
        view = Camera.main;
        board = fieldBoard;
        hud = fieldHud;
        hud.Bind(this);
    }

    void Update()
    {
        var keys = Keyboard.current;
        if (InMatch) board.PlaybackPaused = DemoRun.Modal || PracticeMenuOpen || confirmingYield || JournalOpen;
        if (DemoRun.Modal) return;
        if (InEncounterOffer)
        {
            if (brigandTerms != null) HandleBrigandInput(keys); else HandleOfferInput(keys);
            return;
        }
        if (regionalBody && !InMatch) return;
        if (!InMatch)
        {
            UpdateProximity();
            if (keys != null && keys.gKey.wasPressedThisFrame && CanOpenOffer) OpenFieldOffer();
            else if (keys != null && keys.eKey.wasPressedThisFrame && nearby) BeginSpar();
            else if (keys != null && keys.tKey.wasPressedThisFrame) BeginPractice();
            return;
        }

        bool panning = ReadBattleCameraInput(keys);
        FrameBoard();
        if (keys != null && keys.lKey.wasPressedThisFrame)
        {
            ToggleJournal();
            return;
        }
        if (JournalOpen)
        {
            if (keys != null && keys.escapeKey.wasPressedThisFrame) ToggleJournal();
            return;
        }
        if (HandlePracticeMenu(keys)) return;
        if (CanLeaveSpar)
        {
            if (keys != null && (keys.eKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame ||
                keys.enterKey.wasPressedThisFrame)) LeaveSpar();
            return;
        }
        if ((keys != null && keys.escapeKey.wasPressedThisFrame) ||
            (HasOrder && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame))
        {
            CancelOrYield();
            return;
        }
        if (confirmingYield)
        {
            if (keys != null && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame))
                ConfirmYield();
            return;
        }
        if (PlayingAction)
        {
            TickPlayback();
            return;
        }
        if (ChargingInitiative)
        {
            DisplayCombatTime = System.Math.Min(Match.CombatTime, DisplayCombatTime +
                Mathf.Min(0.2f, Time.unscaledDeltaTime) * initiativeBreathsPerSecond);
            if (!ChargingInitiative)
            {
                nextOpponentAction = Time.unscaledTimeAsDouble + opponentInterval;
                Refresh();
            }
            return;
        }
        if (Match.OpeningPending)
        {
            BeginTurnOpening();
            return;
        }
        if (Match.Status == SparStatus.OpponentTurn)
        {
            if (Time.unscaledTimeAsDouble >= nextOpponentAction) StartOpponentStep();
            return;
        }
        if (keys != null)
        {
            if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame) { ConfirmOrEndTurn(); return; }
            if (keys.tabKey.wasPressedThisFrame) { InspectNextTurn(); return; }
            if (keys.fKey.wasPressedThisFrame) { TogglePlayerSpell(); return; }
            if (keys.rKey.wasPressedThisFrame) { CycleSpellPayment(); return; }
            if (keys.zKey.wasPressedThisFrame) { SelectCarriedSlot(0); return; }
            if (keys.xKey.wasPressedThisFrame) { SelectCarriedSlot(1); return; }
            for (int i = 0; i <= Match.PlayerSpells.Count && i < 10; i++)
            {
                var key = i == 0 ? Key.Digit0 : (Key)((int)Key.Digit1 + i - 1);
                if (!keys[key].wasPressedThisFrame) continue;
                SelectSpellSlot(i);
                return;
            }
            if (keys.qKey.wasPressedThisFrame && Match.InField) { UseFieldHerb(); return; }
            var step = Vector2Int.zero;
            if (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame) step.x--;
            if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame) step.x++;
            if (keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame) step.y--;
            if (keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame) step.y++;
            if (step != Vector2Int.zero)
            {
                SetCursor(Vector2Int.Min(new Vector2Int(Match.Columns - 1, Match.Rows - 1),
                    Vector2Int.Max(Vector2Int.zero, cursor + step)));
                FocusBattleCursor();
            }
            if (keys.spaceKey.wasPressedThisFrame) { ActivateCell(cursor); return; }
        }
        var mouse = Mouse.current;
        if (mouse == null) return;
        var pixel = mouse.position.ReadValue();
        bool moved = (pixel - lastMousePosition).sqrMagnitude > 0.01f;
        bool clicked = mouse.leftButton.wasPressedThisFrame;
        lastMousePosition = pixel;
        if (panning || (!moved && !clicked) || hud.CoversBoard(pixel)) return;
        var cell = board.WorldToCell(view.ScreenToWorldPoint(pixel));
        if (InBounds(cell))
        {
            cameraCursor = null;
            SetCursor(cell);
            if (clicked) ActivateCell(cell);
        }
    }

    void UpdateProximity()
    {
        bool next = CanChallenge;
        hud.SetFieldNearby(next && DemoRun.Active && DemoRun.Sensed);
        if (nearby == next) return;
        nearby = next;
        hud.SetNearby(nearby);
    }

    public void BeginSpar()
    {
        if (!CanChallenge) return;
        StartHere(transform.position, true);
    }

    public void BeginPractice()
    {
        if (InMatch || InEncounterOffer || DemoRun.Modal ||
            (!regionalBody && (walker == null || !walker.CanUseExplorationInput))) return;
        float direction = walkerSprite != null && walkerSprite.flipX ? -1f : 1f;
        Vector2 opponent = (Vector2)walkerSprite.transform.position + new Vector2(direction * 3.6f, 0.55f);
        StartHere(opponent, false);
    }

    void StartHere(Vector2 opponent, bool withGuide, FieldParty party = FieldParty.Practice)
    {
        bool field = party != FieldParty.Practice;
        CultivatorAttributes? attributes = field && DemoRun.Active ? DemoRun.State.Attributes(DemoRun.Sensed) : (CultivatorAttributes?)null;
        var loadout = SparLoadout.Personal();
        var terrain = QingxiBattlefield.Sample(regionalBody ? regionalBody : walker.GetComponent<Rigidbody2D>(), opponent, withGuide ? guideBody : null,
            party, DemoRun.Active ? DemoRun.Name : "药铺学徒",
            preparedWard: party == FieldParty.Brigands && brigandTerms.WardPacked, studiedWard: party == FieldParty.Brigands && brigandTerms.studied,
            secondOpponent: party == FieldParty.Brigands && lookoutSprite ? (Vector2?)lookoutSprite.transform.position : null,
            heroAttributes: attributes, heroMana: field && DemoRun.Active ? (float?)DemoRun.Mana : null,
            practiceGearAttack: loadout.GearAttack, practiceGearHealth: loadout.GearHealth);
        if (terrain == null)
        {
            hud.Explain("这里施展不开，走到宽一些的地方再试。");
            return;
        }
        var place = regionalBody ? (DemoRegion.InSect ? "栖霞山门" : "松风隘") : walker.CurrentPlace;
        BattlePlace = place + (party == FieldParty.Brigands ? " · 生死交锋" : field ? " · 驱兽护药" : withGuide ? " · 切磋" : " · 试阵");
        previousCameraPosition = view.transform.position;
        previousSize = view.orthographicSize;
        if (walker) walker.SetExplorationPaused(true);
        walkerWasVisible = walkerSprite != null && walkerSprite.enabled;
        guideWasVisible = guide != null && guide.enabled;
        if (walkerSprite != null) walkerSprite.enabled = false;
        if (withGuide && guide != null) guide.enabled = false;
        if (trailCanvas != null)
        {
            previousCanvasEnabled = trailCanvas.enabled;
            trailCanvas.enabled = false;
        }
        Match = terrain.Match;
        Match.DeferTurnOpening = true;
        board.ClearActionCues();
        practiceTerrain = field ? null : terrain;
        practiceIndex = 0;
        activeLoadout = loadout;
        activeLoadout.ApplyTo(Match, field ? Random.Range(0, int.MaxValue) : SparLoadout.ComparisonSeed);
        fieldSettled = false;
        openingBattleBounds = terrain.ViewBounds;
        battleMapBounds = terrain.MapBounds;
        confirmingYield = false;
        feedback = "";
        SyncActor();
        board.transform.position = terrain.Origin;
        board.gameObject.SetActive(true);
        ResetBattleFocus();
        FrameBoard();
        if (Mouse.current != null) lastMousePosition = Mouse.current.position.ReadValue();
        nearby = false;
        hud.SetNearby(false);
        Refresh();
    }

    bool InBounds(Vector2Int p) => p.x >= 0 && p.x < Match.Columns && p.y >= 0 && p.y < Match.Rows;

    void SetCursor(Vector2Int cell)
    {
        if (cell == cursor) return;
        cursor = cell;
        feedback = "";
        Refresh();
    }

    void SyncActor()
    {
        CancelOrder();
        ResetPlayback();
        DisplayCombatTime = Finished ? Match.CombatTime : Match.PreviousCombatTime;
        nextOpponentAction = Time.unscaledTimeAsDouble + opponentInterval;
        selected = Match.ActiveUnit;
        cursor = selected.Cell;
        FollowBattleActor();
    }

    public void TogglePlayerSpell()
    {
        if (!AwaitingPlayer || !Match.TogglePlayerSpell(selected)) return;
        DescribeSpell();
    }

    public void SelectSpellSlot(int slot)
    {
        if (!AwaitingPlayer || selected != Match.Units[0] || slot < 0 || slot > Match.PlayerSpells.Count) return;
        var spell = slot == 0 ? null : Match.PlayerSpells[slot - 1];
        if (Match.SelectPlayerSpell(selected, spell)) { DescribeSpell(); return; }
        CancelOrder();
        string current = Match.SpellSelected ? Match.PlayerSpell.Name : "常击";
        feedback = spell ? $"{spell.Name}　·　{Match.SpellUnavailableReason(selected, spell)}。当前仍选{current}。" :
            $"常击需要 {SparMatch.StrikeCost} 点行动力。";
        Refresh();
    }

    public void CycleSpellPayment()
    {
        if (AwaitingPlayer && Match.CyclePayment(selected)) DescribeSpell();
    }

    void DescribeSpell()
    {
        CancelOrder();
        if (!Match.SpellSelected) feedback = "已选常击。";
        else
        {
            var spell = Match.PlayerSpell;
            string aim = spell.Target == FaShuTarget.Self ? "点自己的格子预览。" : $"选取射程 {spell.Range} 格内的敌人。";
            feedback = $"{spell.Name}　·　{SpellPaymentAndEffect()}　·　{aim}";
        }
        Refresh();
    }

    public string SpellPaymentAndEffect()
    {
        var spell = Match.PlayerSpell;
        string unavailable = Match.SpellUnavailableReason(selected, spell);
        if (unavailable.Length > 0) return unavailable;
        string mana = spell.ManaCost > 0
            ? $"法力 {TravelModel.DisplayMana(selected.Mana)} → {TravelModel.DisplayMana(selected.Mana - spell.ManaCost)} · "
            : "";
        string action = spell.ActionPointCost > 0 ? ActionPointPreview(spell.ActionPointCost) + " · " : "";
        string bind = spell.Effect == QingxiSpellEffect.Bind ? $" · 目标下次少走 {spell.EffectAmount} 格" : "";
        return mana + action + Match.CastCostText + bind;
    }

    void InspectNextTurn()
    {
        if (!AwaitingPlayer) return;
        CancelOrder();
        var next = Match.Upcoming[0];
        cursor = next.Actor.Cell;
        FocusBattleCursor();
        feedback = $"接下来是{next.Actor.Name}，距当前 {next.At - Match.CombatTime:0.#} 息。";
        Refresh();
    }

    void Select(SparUnit unit)
    {
        selected = unit;
        cursor = unit.Cell;
        feedback = "";
        Refresh();
    }

    public void ActivateCell(Vector2Int cell)
    {
        if (!AwaitingPlayer || !InBounds(cell) || orderInputFrame == Time.frameCount) return;
        cursor = cell;
        if (pending == OrderKind.Carried && cell == orderCell) { ConfirmOrder(); return; }
        var target = Match.At(cell);
        if (target != null && target.Friendly)
        {
            if (pending == OrderKind.Bandage && cell == orderCell) { ConfirmOrder(); return; }
            if (Match.SpellSelected && Match.CanAttack(selected, cell)) { PreviewCell(cell, target); return; }
            CancelOrder();
            if (Match.CanCommand(target)) Select(target);
            else
            {
                if (target.Alive)
                    feedback = target.Direct ? $"轮到{target.Name}时可指挥，反应 {target.Reaction}。" :
                        target.Name + "会自行选择站位与出手时机。";
                else feedback = Match.CanCarryNow ? "已在相邻格，选择撤离便可带人走。" : "先移到阿荇相邻的可通行格，再带人撤走。";
                Refresh();
            }
            return;
        }
        if (selected != null) PreviewCell(cell, target);
    }

    public void FinishTurn()
    {
        if (!AwaitingPlayer) return;
        if (board.IsMoving(selected))
        {
            SetPlayback(BattlePlaybackPhase.Closing, selected);
            feedback = selected.Name + "走完这段路后收势。";
            Refresh();
            return;
        }
        Match.EndTurn();
        SyncActor();
        feedback = "";
        Refresh();
    }

    public void ToggleYield()
    {
        if (!InMatch || Finished || JournalOpen) return;
        confirmingYield = !confirmingYield;
        board.PlaybackPaused = confirmingYield;
        if (!confirmingYield) nextOpponentAction = Time.unscaledTimeAsDouble + opponentInterval;
        if (!PlayingAction) feedback = "";
        Refresh();
    }

    public void ConfirmYield()
    {
        if (!InMatch || !confirmingYield || JournalOpen) return;
        Match.Yield();
        ResetPlayback();
        board.ClearActionCues();
        confirmingYield = false;
        feedback = "";
        Refresh();
    }

    public void LeaveSpar()
    {
        if (!CanLeaveSpar || JournalOpen) return;
        CancelOrder();
        SetPracticeMenu(false);
        practiceTerrain = null;
        Match = null;
        if (brigandTerms != null) SetBrigandParticipantsVisible(true);
        brigandTerms = null;
        selected = null;
        board.gameObject.SetActive(false);
        if (walkerSprite != null) walkerSprite.enabled = walkerWasVisible;
        if (guide != null) guide.enabled = guideWasVisible;
        view.orthographicSize = previousSize;
        view.transform.position = previousCameraPosition;
        if (trailCanvas != null) trailCanvas.enabled = previousCanvasEnabled;
        if (walker) walker.SetExplorationPaused(false);
        if (regionalBody) RegionalPractice.Finish();
        hud.HideBattle();
        UpdateProximity();
    }

    public void ToggleJournal()
    {
        if (!InMatch || DemoRun.Modal || PracticeMenuOpen || confirmingYield) return;
        hud.SetJournalOpen(!JournalOpen);
        board.PlaybackPaused = JournalOpen;
    }

    void Refresh()
    {
        // Record every confirmed expense before the encounter's own settlement.
        // Saving in an unfinished fight keeps the cost even if the player quits.
        if (Match.InField && DemoRun.ApplyFieldMana(Match.Units[0].Mana) && !Finished)
            DemoRun.Save();
        if (Match.Party == FieldParty.Brigands) RecordBrigandOutcome();
        else if (Match.InField) RecordFieldOutcome();
        var aim = PlaybackPhase == BattlePlaybackPhase.Windup && impactTarget != null ? impactTarget.Cell : OrderTarget;
        board.Render(Match, selected, cursor, OrderPath, aim, AwaitingPlayer);
        hud.Show(this, feedback);
    }
}
