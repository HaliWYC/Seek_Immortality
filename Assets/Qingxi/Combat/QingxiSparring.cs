using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed partial class QingxiSparring : MonoBehaviour
{
    [SerializeField] SparBoard board;
    [SerializeField, Min(0.5f)] float conversationRange = 2.7f;
    [SerializeField, Min(0.1f)] float opponentInterval = 0.78f;

    QingxiWalker walker;
    Rigidbody2D regionalBody;
    SparHud hud;
    Canvas trailCanvas;
    Camera view;
    Vector3 previousCameraPosition;
    SpriteRenderer guide;
    Collider2D guideBody;
    double openedAt;
    SpriteRenderer walkerSprite;
    bool guideWasVisible;
    bool walkerWasVisible;
    Bounds fieldBounds;
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
    public bool InMatch => Match != null;
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

        FrameBoard();
        if (Finished)
        {
            if (keys != null && (keys.eKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame ||
                keys.enterKey.wasPressedThisFrame)) LeaveSpar();
            return;
        }
        if (keys != null && keys.escapeKey.wasPressedThisFrame)
        {
            ToggleYield();
            return;
        }
        if (confirmingYield)
        {
            if (keys != null && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame))
                ConfirmYield();
            return;
        }
        if (Match.Status == SparStatus.OpponentTurn)
        {
            if (Time.unscaledTimeAsDouble >= nextOpponentAction)
            {
                Match.StepOpponent();
                nextOpponentAction = Time.unscaledTimeAsDouble + opponentInterval;
                if (Match.Status == SparStatus.PlayerTurn && (selected == null || !selected.Alive))
                    SelectFirstLiving();
                feedback = "";
                Refresh();
            }
            return;
        }
        if (keys != null)
        {
            if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame) { FinishTurn(); return; }
            if (keys.tabKey.wasPressedThisFrame) { CycleUnit(); return; }
            if (keys.qKey.wasPressedThisFrame && Match.InField) { UseFieldHerb(); return; }
            var step = Vector2Int.zero;
            if (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame) step.x--;
            if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame) step.x++;
            if (keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame) step.y--;
            if (keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame) step.y++;
            if (step != Vector2Int.zero) SetCursor(Vector2Int.Min(new Vector2Int(Match.Columns - 1, Match.Rows - 1),
                Vector2Int.Max(Vector2Int.zero, cursor + step)));
            if (keys.spaceKey.wasPressedThisFrame) { ActivateCell(cursor); return; }
        }
        var mouse = Mouse.current;
        if (mouse == null) return;
        var pixel = mouse.position.ReadValue();
        bool moved = (pixel - lastMousePosition).sqrMagnitude > 0.01f;
        bool clicked = mouse.leftButton.wasPressedThisFrame;
        lastMousePosition = pixel;
        if ((!moved && !clicked) || hud.CoversBoard(pixel)) return;
        var cell = board.WorldToCell(view.ScreenToWorldPoint(pixel));
        if (InBounds(cell))
        {
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
        int bonus = field ? (DemoRun.State.Foundation ? 3 : DemoRun.State.CanFly ? 2 : DemoRun.State.Qi > 0 ? 1 : 0) : 0;
        if (party == FieldParty.Brigands) bonus = DemoRun.State.Foundation ? 4 : Mathf.Min(3, DemoRun.State.Qi / 3);
        var terrain = QingxiBattlefield.Sample(regionalBody ? regionalBody : walker.GetComponent<Rigidbody2D>(), opponent, withGuide ? guideBody : null,
            party, DemoRun.Active ? DemoRun.Name : "药铺学徒", bonus,
            party == FieldParty.Brigands && brigandTerms.wardReady, party == FieldParty.Brigands && brigandTerms.studied,
            party == FieldParty.Brigands && lookoutSprite ? (Vector2?)lookoutSprite.transform.position : null);
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
        openedAt = Time.unscaledTimeAsDouble;
        Match = terrain.Match;
        fieldSettled = false;
        fieldBounds = terrain.ViewBounds;
        confirmingYield = false;
        feedback = "";
        SelectFirstLiving();
        board.transform.position = terrain.Origin;
        board.gameObject.SetActive(true);
        FrameBoard();
        if (Mouse.current != null) lastMousePosition = Mouse.current.position.ReadValue();
        nearby = false;
        hud.SetNearby(false);
        Refresh();
    }

    void FrameBoard()
    {
        // Keep standing figures, the ground cells, and the two HUD bands in view.
        float halfHeight = Mathf.Max(4.85f, (fieldBounds.extents.y + 1.28f) / 0.74f);
        float targetSize = Mathf.Max(halfHeight, (fieldBounds.extents.x + 1.1f) / Mathf.Max(0.1f, view.aspect));
        float blend = Mathf.SmoothStep(0, 1, (float)(Time.unscaledTimeAsDouble - openedAt) / 0.22f);
        var target = new Vector3(fieldBounds.center.x, fieldBounds.center.y + 0.09f, previousCameraPosition.z);
        view.orthographicSize = Mathf.Lerp(previousSize, targetSize, blend);
        view.transform.position = Vector3.Lerp(previousCameraPosition, target, blend);
    }

    bool InBounds(Vector2Int p) => p.x >= 0 && p.x < Match.Columns && p.y >= 0 && p.y < Match.Rows;

    void SetCursor(Vector2Int cell)
    {
        if (cell == cursor) return;
        cursor = cell;
        feedback = "";
        Refresh();
    }

    void SelectFirstLiving()
    {
        foreach (var unit in Match.Units)
        {
            if (!unit.Alive || !unit.Direct) continue;
            selected = unit;
            cursor = unit.Cell;
            return;
        }
    }

    public void CycleUnit()
    {
        if (!InMatch || Match.Status != SparStatus.PlayerTurn || confirmingYield) return;
        int start = 0;
        for (int i = 0; i < Match.Units.Count; i++) if (Match.Units[i] == selected) start = i;
        for (int n = 1; n <= Match.Units.Count; n++)
        {
            var unit = Match.Units[(start + n) % Match.Units.Count];
            if (!unit.Direct || !unit.Alive) continue;
            Select(unit);
            return;
        }
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
        if (!InMatch || Match.Status != SparStatus.PlayerTurn || confirmingYield || !InBounds(cell)) return;
        cursor = cell;
        var target = Match.At(cell);
        if (target != null && target.Friendly)
        {
            if (Match.CanCommand(target)) Select(target);
            else
            {
                feedback = target.Alive ? target.Name + "会自行选择站位与出手时机。" :
                    Match.CanCarryNow ? "已在相邻格，选择撤离便可带人走。" : "先移到阿荇相邻的可通行格，再带人撤走。";
                Refresh();
            }
            return;
        }
        if (target != null)
        {
            if (Match.Attack(selected, cell)) feedback = "";
            else if (selected.Acted) feedback = "本回合已经施术。";
            else if (!Match.HasLineOfSight(selected.Cell, cell)) feedback = "草木山石遮住了视线，换个落脚处再施术。";
            else feedback = "对手在射程之外，可先移动。";
        }
        else if (!Match.Move(selected, cell))
            feedback = Match.IsBlocked(cell) ? "此处地形无法落脚。" :
                selected.Moved ? "本回合已经移动。可选对手施术，或换一名队员。" : "此格走不到，选择青色范围内的空格。";
        else feedback = "";
        Refresh();
    }

    public void FinishTurn()
    {
        if (!InMatch || Match.Status != SparStatus.PlayerTurn || confirmingYield) return;
        Match.EndTurn();
        feedback = "";
        nextOpponentAction = Time.unscaledTimeAsDouble + opponentInterval;
        Refresh();
    }

    public void ToggleYield()
    {
        if (!InMatch || Finished) return;
        confirmingYield = !confirmingYield;
        if (!confirmingYield) nextOpponentAction = Time.unscaledTimeAsDouble + opponentInterval;
        feedback = "";
        Refresh();
    }

    public void ConfirmYield()
    {
        if (!InMatch || !confirmingYield) return;
        Match.Yield();
        confirmingYield = false;
        feedback = "";
        Refresh();
    }

    public void LeaveSpar()
    {
        if (!Finished) return;
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

    void Refresh()
    {
        if (Match.Party == FieldParty.Brigands) RecordBrigandOutcome();
        else if (Match.InField) RecordFieldOutcome();
        board.Render(Match, selected, cursor);
        hud.Show(this, feedback);
    }
}
