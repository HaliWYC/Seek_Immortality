using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class SparBoard : MonoBehaviour
{
    static readonly Color Plain = new Color32(64, 78, 61, 170);
    static readonly Color Reachable = new Color32(84, 176, 147, 235);
    static readonly Color Selected = new Color32(223, 186, 106, 240);
    static readonly Color Target = new Color32(194, 98, 73, 235);
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
        public Transform healthFill;

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

    readonly List<SpriteRenderer> cellMarks = new List<SpriteRenderer>();
    SparMatch displayedMatch;

    public Vector3 CellWorld(Vector2Int cell) => transform.position + (Vector3)QingxiGridMetric.Offset(cell);
    public Vector2Int WorldToCell(Vector3 world) => QingxiGridMetric.Index(world - transform.position);

    public void Render(SparMatch match, SparUnit selected, Vector2Int cursor)
    {
        bool newMatch = displayedMatch != match;
        if (newMatch)
        {
            LayoutMarks(match);
            displayedMatch = match;
        }

        bool playerTurn = match.Status == SparStatus.PlayerTurn;
        for (int i = 0; i < match.Columns * match.Rows; i++)
        {
            var cell = new Vector2Int(i % match.Columns, i / match.Columns);
            SpriteRenderer mark = cellMarks[i];
            if (!mark.enabled) continue;
            Color tint = Plain;
            if (playerTurn && selected != null && selected.Alive)
            {
                if (selected.Cell == cell) tint = Selected;
                else if (match.CanMove(selected, cell)) tint = Reachable;
                else if (match.CanAttack(selected, cell)) tint = Target;
            }
            mark.sprite = tint == Plain ? gridStamp.sprite : rangeStamp;
            mark.color = tint;
        }

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
                else if (unit.Moved && unit.Acted) piece.stance.color = Spent;
            }
            piece.targetFrame.enabled = playerTurn && selected != null && match.CanAttack(selected, unit.Cell);
            piece.healthFill.localScale = new Vector3((float)unit.Hp / unit.MaxHp, 1f, 1f);
        }

        bool onField = cursor.x >= 0 && cursor.x < match.Columns && cursor.y >= 0 && cursor.y < match.Rows;
        cursorMark.gameObject.SetActive(playerTurn && onField);
        if (onField) cursorMark.position = CellWorld(cursor);
    }

    void PositionPiece(PieceView piece, SparUnit unit, bool newMatch)
    {
        Vector2Int previous = piece.destination;
        piece.destination = unit.Cell;
        piece.travel = 0f;
        var path = unit.WalkPath;
        if (!newMatch && path.Count > 1 && path[0] == previous && path[path.Count - 1] == unit.Cell)
        {
            piece.route = path;
            return;
        }

        piece.route = null;
        piece.body.flipX = !unit.Friendly;
        SetFeet(piece, CellWorld(unit.Cell));
    }

    void LateUpdate()
    {
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
    }

    static void SetFeet(PieceView piece, Vector3 feet)
    {
        piece.group.transform.position = feet;
        piece.group.sortingOrder = 1000 - Mathf.RoundToInt(feet.y * 10);
    }

    void OnDisable()
    {
        displayedMatch = null;
        foreach (PieceView piece in pieces) piece.route = null;
    }

    void LayoutMarks(SparMatch match)
    {
        int count = match.Columns * match.Rows;
        gridStamp.enabled = false;
        for (int i = cellMarks.Count; i < count; i++)
        {
            var copy = Instantiate(gridStamp, gridStamp.transform.parent);
            cellMarks.Add(copy);
        }
        for (int i = 0; i < cellMarks.Count; i++)
        {
            var cell = new Vector2Int(i % match.Columns, i / match.Columns);
            SpriteRenderer mark = cellMarks[i];
            mark.enabled = i < count && !match.IsBlocked(cell);
            if (!mark.enabled) continue;
            mark.transform.position = CellWorld(cell);
            mark.name = $"阵位 {cell.x + 1} · {cell.y + 1}";
        }
    }
}
