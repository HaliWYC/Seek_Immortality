using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed partial class SparBoard : MonoBehaviour
{
    static readonly Color Plain = new Color32(64, 78, 61, 170);
    static readonly Color Reachable = new Color32(84, 176, 147, 235);
    static readonly Color Selected = new Color32(223, 186, 106, 240);
    static readonly Color Target = new Color32(194, 98, 73, 235);
    static readonly Color Route = new Color32(225, 171, 79, 255);
    static readonly Color Destination = new Color32(255, 224, 142, 255);
    static readonly Color Friendly = new Color32(71, 146, 124, 255);
    static readonly Color Opposing = new Color32(188, 102, 82, 255);
    static readonly Color Spent = new Color32(143, 143, 121, 255);

    [Serializable]
    sealed class PieceView
    {
        public SortingGroup group;
        public SpriteRenderer stance;
        public SpriteRenderer body;
        public SpriteRenderer targetFrame;
        public Transform headAnchor;

        [NonSerialized] public Vector2Int destination;
        [NonSerialized] public IReadOnlyList<Vector2Int> route;
        [NonSerialized] public float travel;
        [NonSerialized] public Sprite originalSprite;
    }

    [SerializeField] SpriteRenderer gridStamp;
    [SerializeField] Sprite rangeStamp;
    [SerializeField] Sprite fieldBeast;
    [SerializeField] PieceView[] pieces;
    [SerializeField] Transform cursorMark;
    [SerializeField, Range(0.10f, 0.13f)] float secondsPerStep = 0.11f;

    SparMatch displayedMatch;

    public Vector3 CellWorld(Vector2Int cell) => transform.position + (Vector3)QingxiGridMetric.Offset(cell);
    public Vector2Int WorldToCell(Vector3 world) => QingxiGridMetric.Index(world - transform.position);
    public Vector3 FeetPosition(int index) => pieces[index].group.transform.position;
    public Vector3 HeadPosition(int index) => pieces[index].headAnchor.position;
    public Sprite PieceSprite(int index) => pieces[index].body.sprite;

    public void Render(SparMatch match, SparUnit selected, Vector2Int cursor, IReadOnlyList<Vector2Int> previewPath = null, Vector2Int? pendingTarget = null, bool awaitingInput = true)
    {
        bool newMatch = displayedMatch != match;
        if (newMatch)
        {
            ClearActionCues();
            LayoutMarks(match);
            displayedMatch = match;
            FindScenery(match);
        }

        bool playerTurn = awaitingInput && match.Status == SparStatus.PlayerTurn;
        sceneryPath = playerTurn ? previewPath : null;
        UpdateMarks(match, playerTurn ? selected : null, previewPath, pendingTarget);

        for (int i = 0; i < pieces.Length; i++)
        {
            PieceView piece = pieces[i];
            SparUnit unit = i < match.Units.Count ? match.Units[i] : null;
            bool wounded = unit != null && unit == match.Companion && match.RescueRounds > 0;
            bool visible = unit != null && (unit.Alive || wounded);
            piece.group.gameObject.SetActive(visible);
            if (!visible)
            {
                piece.route = null;
                continue;
            }
            if (piece.originalSprite == null) piece.originalSprite = piece.body.sprite;
            if (newMatch)
                piece.body.sprite = match.InField && match.Party != FieldParty.Brigands && i == 2 ? fieldBeast : piece.originalSprite;
            piece.body.transform.localRotation = wounded ? Quaternion.Euler(0, 0, 64) : Quaternion.identity;
            piece.body.color = wounded ? new Color(0.8f, 0.6f, 0.5f) : Color.white;
            if (newMatch || piece.destination != unit.Cell)
                PositionPiece(piece, unit, newMatch);
            piece.stance.color = wounded ? Target : unit.Friendly ? Friendly : Opposing;
            if (playerTurn && unit.Friendly)
            {
                if (unit == selected) piece.stance.color = Selected;
                else if (unit.ActionPoints == 0) piece.stance.color = Spent;
            }
            bool aiming = pendingTarget.HasValue && pendingTarget.Value == unit.Cell;
            piece.targetFrame.enabled = aiming || (playerTurn && selected != null && match.CanAttack(selected, unit.Cell));
            piece.targetFrame.color = aiming ? Destination : Target;
        }

        bool onField = !match.IsBlocked(cursor);
        cursorMark.gameObject.SetActive(playerTurn && onField);
        if (onField) cursorMark.position = CellWorld(cursor);
    }

    void PositionPiece(PieceView piece, SparUnit unit, bool newMatch)
    {
        Vector2Int previous = piece.destination;
        piece.destination = unit.Cell;
        var path = unit.WalkPath;
        if (!newMatch && path.Count > 1 && path[0] == previous && path[path.Count - 1] == unit.Cell)
        {
            if (piece.route != null)
            {
                var continued = new List<Vector2Int>(piece.route);
                for (int i = 1; i < path.Count; i++) continued.Add(path[i]);
                piece.route = continued;
                return;
            }
            piece.travel = 0f;
            piece.route = path;
            return;
        }

        piece.route = null;
        piece.body.flipX = !unit.Friendly;
        SetFeet(piece, CellWorld(unit.Cell));
    }

    void LateUpdate()
    {
        if (PlaybackPaused) return;
        foreach (PieceView piece in pieces)
        {
            var path = piece.route;
            if (path == null) continue;
            piece.travel += Time.unscaledDeltaTime / secondsPerStep;
            int last = path.Count - 1;
            int from = Mathf.Min(Mathf.FloorToInt(piece.travel), last - 1);
            Vector3 a = CellWorld(path[from]);
            Vector3 b = CellWorld(path[from + 1]);
            piece.body.flipX = b.x < a.x;
            SetFeet(piece, Vector3.Lerp(a, b, piece.travel - from));
            if (piece.travel >= last) piece.route = null;
        }
        UpdateSceneryVisibility(Time.unscaledDeltaTime);
        UpdateActionCues(Time.unscaledDeltaTime);
    }

    static void SetFeet(PieceView piece, Vector3 feet)
    {
        piece.group.transform.position = feet;
        piece.group.sortingOrder = PieceSortingOrder(feet.y);
    }

    static int PieceSortingOrder(float feetY) => 1000 - Mathf.RoundToInt(feetY * 10);

    void OnDisable()
    {
        displayedMatch = null;
        RestoreScenery();
        foreach (PieceView piece in pieces) piece.route = null;
        PlaybackPaused = false;
        ClearActionCues();
    }

}
