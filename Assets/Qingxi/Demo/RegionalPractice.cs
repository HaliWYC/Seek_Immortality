using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public sealed class RegionalPractice : ScriptableObject
{
    [SerializeField] SparBoard board;
    [SerializeField] SparHud hud;
    static QingxiSparring field;
    static int finishedAt = -1;
    public static bool Busy => field && (field.InMatch || field.InEncounterOffer || finishedAt == Time.frameCount);

    public static void Begin(SpriteRenderer traveler)
    {
        if (!traveler || DemoRun.Modal || Busy) return;
        Prepare(traveler);
        field.BeginPractice();
    }

    public static void MeetBrigand(SpriteRenderer traveler)
    {
        if (!traveler || !DemoRun.Sensed || DemoRun.Modal || Busy) return;
        Prepare(traveler);
        field.OpenBrigandOffer();
    }

    static void Prepare(SpriteRenderer traveler)
    {
        if (!field)
        {
            var assets = Resources.Load<RegionalPractice>("RegionalPractice");
            var rig = new GameObject("原地试阵");
            rig.transform.SetParent(traveler.transform.parent, false);
            var battleBoard = Instantiate(assets.board, rig.transform);
            battleBoard.gameObject.SetActive(false);
            var battleHud = Instantiate(assets.hud, rig.transform);
            if (!EventSystem.current)
            {
                var events = new GameObject("战棋输入", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(rig.transform, false);
            }
            if (!traveler.TryGetComponent<Rigidbody2D>(out var body))
            {
                body = traveler.gameObject.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
            }
            field = rig.AddComponent<QingxiSparring>();
            field.BindRegional(body, traveler, battleBoard, battleHud);
        }
    }

    public static void Finish() => finishedAt = Time.frameCount;
}
