using System;
using UnityEngine;

public sealed partial class TravelPrototype
{
    string campaignKey;
    DemoCultivation campaignState;
    double reportedMinutes;
    float nextSave;
    bool wasInMenu;
    RegionalLocalView localView;

    public MapPlace Place => journey.Place;
    public bool LogOpen => showLog;
    internal bool MatchesCampaign => campaignKey != null && DemoRun.Active &&
        ReferenceEquals(campaignState, DemoRun.State) && campaignKey == DemoRun.Key("Region");
    bool CanFly => campaignKey == null || (MatchesCampaign && DemoRun.State.CanFly);
    bool ActiveLocal => campaignKey != null && (Place == MapPlace.Sect || Place == MapPlace.Pass);

    void BeginCampaign()
    {
        campaignKey = DemoRun.Key("Region");
        campaignState = DemoRun.State;
        string snapshot = DemoRun.RegionData;
        if (!string.IsNullOrEmpty(snapshot))
        {
            try
            {
                var checkpoint = JsonUtility.FromJson<TravelCheckpoint>(snapshot);
                if (journey.Restore(checkpoint) && checkpoint.log != null)
                    for (int i = 0; i < Mathf.Min(MaxRecords, checkpoint.log.Length); i++) log.Add(checkpoint.log[i]);
            }
            catch (ArgumentException) { journey.Notify("舆图已从青溪镇口重新展开。"); }
        }
        if (Place == MapPlace.Town) journey.Interact();
        completed = journey.CompletedTrips;
        reportedMinutes = journey.Minutes;
        DemoRegion.Bind(this);
        localView = new RegionalLocalView(GetComponent<Camera>());
        journey.LocalObstruction = localView.Obstructed;
        localView.Show(journey);
    }

    internal void CaptureCampaign()
    {
        if (!MatchesCampaign) return;
        double delta = journey.Minutes - reportedMinutes;
        reportedMinutes = journey.Minutes;
        if (delta > 0) campaignState.AddTravelMinutes(delta);
        var checkpoint = journey.Capture();
        checkpoint.log = log.ToArray();
        DemoRun.RegionData = JsonUtility.ToJson(checkpoint);
    }

    void LateUpdate()
    {
        if (localView != null && !RegionalPractice.Busy) localView.Show(journey);
    }

    void BeginLocalPractice()
    {
        if (!ActiveLocal || DemoRun.Modal || RegionalPractice.Busy) return;
        DemoRegion.Flush();
        RegionalPractice.Begin(localView.Traveler);
        wasInMenu = true;
    }

    void MeetBrigand()
    {
        if (DemoRun.Modal || RegionalPractice.Busy || !localView.NearBrigand(journey)) return;
        DemoRegion.Flush();
        RegionalPractice.MeetBrigand(localView.Traveler);
        wasInMenu = true;
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && MatchesCampaign) DemoRegion.Flush();
    }

    void OnApplicationQuit()
    {
        if (MatchesCampaign) DemoRegion.Flush();
    }

    void ToggleFlight()
    {
        if (!CanFly) { journey.Notify("还未习得御剑，先步行探路。修行簿中可查看境界。"); return; }
        journey.ToggleFlight();
        if (MatchesCampaign) DemoRegion.Flush();
    }

    void Interact()
    {
        if (ActiveLocal && localView.NearContact(journey))
        {
            DemoRegion.Flush();
            DemoRun.OpenCultivation();
            return;
        }
        if (journey.InteractLabel.Length == 0) return;
        journey.Interact();
        if (!MatchesCampaign) return;
        SaveChanges();
        DemoRegion.Flush();
        if (Place == MapPlace.Town)
        {
            DemoRegion.Release(this);
            DemoRun.ReturnTown();
        }
        else localView.Show(journey);
    }

    void DrawLocalControls()
    {
        bool sect = Place == MapPlace.Sect;
        Fill(new Rect(22, 22, 216, 65), Panel);
        Fill(new Rect(22, 22, 4, 65), Gold);
        Word(new Rect(38, 28, 184, 29), PlaceName(Place), 23, Paper, TextAnchor.MiddleLeft, true);
        Word(new Rect(39, 57, 180, 23), sect ? "山门前庭" : "北隘石径", 14, Soft);
        if (Button(new Rect(994, 24, 181, 36), "行旅簿   Tab")) showLog = true;
        Fill(new Rect(22, 648, 1154, 75), Panel);
        string action = localView.NearContact(journey) ? sect ? "拜访山门执事" : "察看石隙药圃" : journey.InteractLabel;
        bool stranger = localView.NearBrigand(journey);
        string hint = stranger ? "灰衣客向你招手。他的同伴站在哪里？" :
            sect ? "沿石阶向北，可以拜访执事、借阅法诀。" :
            localView.HasBrigands ? "循水向北，石隙间生有灵草。东侧有人歇脚。" : "循水向北，石隙间生有灵草。东侧石阶已空。";
        Word(new Rect(40, 656, 630, 29), hint, 17, Paper);
        Word(new Rect(40, 686, 700, 26), "WASD / 方向键  行走        南侧山门可返回舆图", 14, Soft);
        if (Button(new Rect(605, 666, 140, 40), "原地试阵   T")) BeginLocalPractice();
        if (stranger)
        {
            if (Button(new Rect(756, 666, 205, 40), "与灰衣客搭话   G", true, true)) MeetBrigand();
        }
        else if (Button(new Rect(756, 666, 205, 40), string.IsNullOrEmpty(action) ? "走近地点可交互" : action + "   E", action.Length > 0, true)) Interact();
        if (Button(new Rect(974, 656, 183, 28), "修行簿   F1")) DemoRun.OpenCultivation();
        if (Button(new Rect(974, 687, 183, 28), "卷册   F2")) DemoRun.OpenMenu();
    }
}
