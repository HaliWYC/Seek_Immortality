using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed partial class TravelPrototype : MonoBehaviour
{
    const float Width = 1200f;
    const float Height = 746f;
    const string ChartKey = "Qinglan.Travel.Chart.v1";
    const string LogKey = "Qinglan.Travel.Log.v1";
    const int MaxRecords = 8;
    static readonly Color Ink = Hex("#202F2C");
    static readonly Color Panel = Hex("#2B3C37");
    static readonly Color Border = Hex("#45544A");
    static readonly Color Paper = Hex("#F1E7CD");
    static readonly Color Soft = Hex("#C5C9B8");
    static readonly Color Gold = Hex("#DABD80");
    static readonly Color Muted = Hex("#9DAA99");
    static readonly Color Red = Hex("#A64F3D");
    static readonly Color Green = Hex("#8DB19D");

    TravelModel journey;
    TravelMapView map;
    Font font;
    GUIStyle lettering;
    readonly List<string> log = new List<string>();
    bool showLog;
    int writtenMask;
    int completed;

    void Awake()
    {
        journey = new TravelModel();
        if (DemoRun.Active) BeginCampaign();
        else
        {
            writtenMask = PlayerPrefs.GetInt(ChartKey, 0);
            journey.RestoreSeen(writtenMask);
            string history = PlayerPrefs.GetString(LogKey, "");
            if (history.Length > 0) log.AddRange(history.Split('\n'));
        }
        map = new TravelMapView();
        font = Resources.Load<Font>("NotoSansSC-Regular");
    }

    void Update()
    {
        if (campaignKey != null)
        {
            if (!MatchesCampaign || DemoRun.Modal || RegionalPractice.Busy) { wasInMenu = true; return; }
            if (wasInMenu) { wasInMenu = false; return; }
        }
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            showLog = !showLog;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (showLog) showLog = false;
            else journey.CancelAuto();
        }
        if (showLog) return;
        Vector2 direction = Vector2.zero;
        if (keyboard != null)
        {
            if (ActiveLocal && keyboard.gKey.wasPressedThisFrame && localView.NearBrigand(journey)) { MeetBrigand(); return; }
            if (ActiveLocal && keyboard.tKey.wasPressedThisFrame) { BeginLocalPractice(); return; }
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) direction.x--;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) direction.x++;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) direction.y--;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) direction.y++;
            if (keyboard.eKey.wasPressedThisFrame) { Interact(); return; }
            if (keyboard.fKey.wasPressedThisFrame) ToggleFlight();
            if (keyboard.rKey.wasPressedThisFrame) journey.Rest();
            if (keyboard.digit1Key.wasPressedThisFrame && journey.CanStartRoute(TravelRoute.Road))
                journey.StartRoute(TravelRoute.Road);
            if (keyboard.digit2Key.wasPressedThisFrame && journey.CanStartRoute(TravelRoute.Trail))
                journey.StartRoute(TravelRoute.Trail);
        }
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (direction != Vector2.zero)
        {
            if (journey.IsAuto) journey.CancelAuto();
            journey.Move(direction.normalized, dt);
        }
        else if (journey.IsAuto) journey.TickAuto(dt);
        SaveChanges();
    }

    void SaveChanges()
    {
        if (campaignKey == null && writtenMask != journey.SeenMask)
        {
            writtenMask = journey.SeenMask;
            PlayerPrefs.SetInt(ChartKey, writtenMask);
            PlayerPrefs.Save();
        }
        bool arrived = completed != journey.CompletedTrips;
        if (arrived)
        {
            completed = journey.CompletedTrips;
            log.Insert(0, journey.LastTripLabel + "   ·   " + Duration(journey.LastTripMinutes));
            if (log.Count > MaxRecords) log.RemoveRange(MaxRecords, log.Count - MaxRecords);
            if (campaignKey == null)
            {
                PlayerPrefs.SetString(LogKey, string.Join("\n", log));
                PlayerPrefs.Save();
            }
        }
        if (MatchesCampaign && (arrived || (journey.Minutes != reportedMinutes && Time.unscaledTime >= nextSave)))
        {
            nextSave = Time.unscaledTime + .8f;
            DemoRegion.Flush();
        }
    }

    void OnDestroy()
    {
        if (journey != null)
        {
            SaveChanges();
            if (DemoRegion.Current == this) DemoRegion.Flush();
        }
        DemoRegion.Release(this);
        map?.Dispose();
    }

    public static string Duration(double minutes)
    {
        if (minutes > 0 && minutes < 1) return "不足 1 分";
        int n = (int)Math.Floor(minutes + 0.000001);
        int days = n / 1440, hours = n / 60 % 24;
        if (days > 0) return days + " 天 " + hours + " 时 " + n % 60 + " 分";
        if (hours > 0) return hours + " 时 " + n % 60 + " 分";
        return n + " 分";
    }

    static string PlaceName(MapPlace p)
    {
        switch (p)
        {
            case MapPlace.Town: return "青溪镇";
            case MapPlace.Pass: return "松风隘";
            case MapPlace.Sect: return "栖霞宗";
            default: return "青岚山境";
        }
    }

    static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString(value, out var c);
        return c;
    }

    void Fill(Rect r, Color c)
    {
        var previous = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    void Word(Rect r, string value, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool bold = false, bool wrap = false)
    {
        lettering.fontSize = size;
        lettering.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        lettering.alignment = align;
        lettering.normal.textColor = color;
        lettering.wordWrap = wrap;
        GUI.Label(r, value, lettering);
    }

    bool Button(Rect r, string value, bool enabled = true, bool primary = false)
    {
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        Fill(r, enabled ? (primary ? Gold : hover ? Green : Border) : Panel);
        if (!primary)
        {
            var inner = new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2);
            Fill(inner, enabled && hover ? Hex("#394F45") : Panel);
        }
        Word(r, value, 15, enabled ? (primary ? Ink : Paper) : Muted, TextAnchor.MiddleCenter);
        return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    void OnGUI()
    {
        if (journey == null) return;
        if (ActiveLocal && RegionalPractice.Busy) return;
        if (lettering == null) lettering = new GUIStyle { font = font, clipping = TextClipping.Clip };
        int priorDepth = GUI.depth;
        GUI.depth = 20;
        Matrix4x4 original = GUI.matrix;
        bool local = ActiveLocal;
        if (!local) Fill(new Rect(0, 0, Screen.width, Screen.height), Ink);
        float scale = Mathf.Min(Screen.width / Width, Screen.height / Height);
        var offset = new Vector3((Screen.width - Width * scale) / 2, (Screen.height - Height * scale) / 2, 0);
        GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1));
        bool enabledBefore = GUI.enabled;
        if (showLog || (campaignKey != null && DemoRun.Modal)) GUI.enabled = false;
        if (local) DrawLocalControls();
        else
        {
            Header();
            Fill(new Rect(22, 89, 843, 543), Gold);
            map.Draw(new Rect(24, 91, 839, 539), journey, font);
            Sidebar();
            Footer();
        }
        GUI.enabled = enabledBefore;
        if (showLog) Ledger();
        GUI.matrix = original;
        GUI.depth = priorDepth;
    }

    void Header()
    {
        Fill(new Rect(24, 23, 44, 44), Red);
        Word(new Rect(24, 21, 44, 46), "岚", 28, Paper, TextAnchor.MiddleCenter);
        Word(new Rect(82, 13, 360, 42), "青岚山境", 32, Paper, TextAnchor.MiddleLeft, true);
        Word(new Rect(84, 53, 410, 20), "行旅舆图   /   青溪镇至栖霞宗", 14, Soft);
        int total = (int)Math.Floor(journey.Minutes) + 360;
        string time = "第 " + (total / 1440 + 1) + " 天    " + (total / 60 % 24).ToString("00") + "：" + (total % 60).ToString("00");
        Word(new Rect(667, 23, 351, 44), MatchesCampaign ? "历练第 " + (DemoRun.State.Days + 1) + " 天" : time, 19, Gold, TextAnchor.MiddleRight);
        if (Button(new Rect(1038, 27, 138, 38), "行旅簿   Tab")) showLog = true;
    }

    void Sidebar()
    {
        const float x = 902, w = 248;
        Fill(new Rect(879, 89, 297, 543), Panel);
        Word(new Rect(x, 102, w, 26), journey.Place == MapPlace.World ? "区域舆图" : "局部舆图", 15, Gold);
        Word(new Rect(x, 130, w, 45), PlaceName(journey.Place), 31, Paper, TextAnchor.MiddleLeft, true);
        string terrain = journey.Place == MapPlace.World ? journey.TerrainLabel : "境内";
        Word(new Rect(x, 177, w, 28), terrain + "    /    " + (journey.Flying ? "御剑" : "步行"), 17, Soft);
        Fill(new Rect(x, 218, w, 1), Border);
        Word(new Rect(x, 229, 92, 26), "法力", 15, Soft);
        Word(new Rect(x + 111, 229, 137, 26), Mathf.FloorToInt(journey.Mana + 0.001f) + " / 100", 16, Paper, TextAnchor.MiddleRight);
        Fill(new Rect(x, 261, w, 5), Border);
        Fill(new Rect(x, 261, w * Mathf.Clamp01(journey.Mana / 100), 5), Green);
        string action = journey.InteractLabel;
        if (Button(new Rect(x, 289, w, 42), string.IsNullOrEmpty(action) ? "靠近出入口可交互" : action + "   E", !string.IsNullOrEmpty(action), true))
            Interact();
        if (Button(new Rect(x, 341, 121, 37), CanFly ? journey.Flying ? "收剑   F" : "御剑   F" : "御剑未习", journey.Place == MapPlace.World && CanFly))
            ToggleFlight();
        if (Button(new Rect(x + 127, 341, 121, 37), "调息   R", journey.CanRest))
            journey.Rest();
        Fill(new Rect(x, 398, w, 1), Border);
        Word(new Rect(x, 413, w, 27), "循路赶路", 19, Paper, TextAnchor.MiddleLeft, true);
        Word(new Rect(x, 444, w, 23), "山路勘记", 14, Soft);
        int count = TravelModel.Trail.Length - 1;
        int seen = 0;
        for (int i = 0; i < count; i++)
            if (journey.SegmentSeen(i)) seen++;
        Word(new Rect(x + 110, 444, 138, 23), seen + " / " + count + " 段", 14, Gold, TextAnchor.MiddleRight);
        for (int i = 0; i < count; i++)
        {
            float step = w / count;
            Fill(new Rect(x + i * step, 473, step - 3, 4), journey.SegmentSeen(i) ? Gold : Border);
        }
        if (journey.IsAuto)
        {
            if (Button(new Rect(x, 495, w, 36), "停步   Esc")) journey.CancelAuto();
        }
        else
        {
            if (Button(new Rect(x, 495, 121, 36), "官道   1", journey.CanStartRoute(TravelRoute.Road)))
                journey.StartRoute(TravelRoute.Road);
            if (Button(new Rect(x + 127, 495, 121, 36), "山路   2", journey.CanStartRoute(TravelRoute.Trail)))
                journey.StartRoute(TravelRoute.Trail);
        }
        string hint = journey.IsAuto ? "按方向键可随时离开路线。" :
            journey.Place != MapPlace.World ? "从出入口进入大地图，即可自行探路。" :
            !journey.TrailKnown ? "官道沿河南绕。青溪镇东北有小径通往松风隘，步行可勘路。" :
            "在大地图的镇口或山门，可选已知路线往返。";
        Word(new Rect(x, 542, w, 73), hint, 15, Soft, TextAnchor.UpperLeft, false, true);
    }

    void Footer()
    {
        Fill(new Rect(24, 651, 4, 42), Gold);
        string notice = journey.Notice;
        Word(new Rect(42, 643, 798, 54), string.IsNullOrEmpty(notice) ? "从青溪镇出发，往东北寻访栖霞宗。" : notice, 16, Paper, TextAnchor.MiddleLeft, false, true);
        bool arrived = journey.TripMinutes == 0 && journey.CompletedTrips > 0;
        string timing = (arrived ? "上程   " : "本程   ") + Duration(arrived ? journey.LastTripMinutes : journey.TripMinutes);
        Word(new Rect(820, 650, 355, 40), timing, 20, Gold, TextAnchor.MiddleRight);
        Fill(new Rect(24, 706, 1152, 1), Border);
        if (MatchesCampaign)
        {
            Word(new Rect(24, 712, 770, 28), "WASD / 方向键  移动      E  进出地点      F  御剑      R  调息      Tab  行旅簿", 14, Soft);
            if (Button(new Rect(883, 710, 135, 30), "修行簿   F1")) DemoRun.OpenCultivation();
            if (Button(new Rect(1031, 710, 145, 30), "卷册   F2")) DemoRun.OpenMenu();
        }
        else Word(new Rect(24, 713, 1152, 26), "WASD / 方向键  移动       E  进出地点       F  步行 / 御剑       R  调息       Tab  行旅簿", 14, Soft);
    }

    void Ledger()
    {
        Fill(new Rect(0, 0, Width, Height), new Color(0.035f, 0.07f, 0.055f, 0.82f));
        Fill(new Rect(254, 101, 692, 538), Gold);
        Fill(new Rect(256, 103, 688, 534), Panel);
        Word(new Rect(289, 127, 550, 44), "行旅簿", 31, Paper, TextAnchor.MiddleLeft, true);
        if (Button(new Rect(822, 133, 92, 34), "合上   Tab")) showLog = false;
        Word(new Rect(290, 181, 622, 31), "镇口与山门之间，每走完一程便记下一笔。", 16, Soft);
        Fill(new Rect(290, 226, 622, 1), Border);
        if (log.Count == 0)
            Word(new Rect(290, 248, 622, 60), "尚无完整行程。可先沿官道上山，再原路御剑返回，比较两次耗时。", 17, Gold, TextAnchor.UpperLeft, false, true);
        for (int i = 0; i < log.Count; i++)
        {
            float y = 242 + i * 45;
            Word(new Rect(290, y, 622, 37), log[i], 17, i == 0 ? Gold : Paper);
            Fill(new Rect(290, y + 40, 622, 1), Border);
        }
    }
}
