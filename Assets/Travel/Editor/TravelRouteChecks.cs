using System;
using UnityEditor;
using UnityEngine;

public static class TravelRouteChecks
{
    [MenuItem("行旅/验证地图原型")]
    public static void Run()
    {
        double reference = 0;
        foreach (int fps in new[] { 30, 60, 120, 144 })
        {
            var journey = new TravelModel();
            journey.Interact();
            if (journey.Place != MapPlace.World) throw new InvalidOperationException("未能从镇口出发。");
            double walked = Follow(journey, TravelModel.Sect, fps);
            journey.Interact();
            if (journey.Place != MapPlace.Sect) throw new InvalidOperationException("未能在终点进入栖霞宗。");
            journey.Interact();
            journey.ToggleFlight();
            double flown = Follow(journey, TravelModel.Town, fps);
            if (flown >= walked || journey.Mana >= 100 || journey.CompletedTrips != 2)
                throw new InvalidOperationException("御剑返程的用时、法力或行程笔数异常。");
            if (reference > 0 && Math.Abs(walked - reference) > 1)
                throw new InvalidOperationException("同一条官道的游戏内用时随刷新率而变化。");
            reference = walked;
        }
        CheckCheckpoint();
        Debug.Log("行旅验证通过：官道往返、御剑耗时、途中续程与部分山路勘记均已检查。");
    }

    static void CheckCheckpoint()
    {
        var journey = new TravelModel();
        journey.Interact();
        journey.Move((TravelModel.Trail[1] - TravelModel.Town).normalized, .65f);
        var copy = new TravelModel();
        var saved = JsonUtility.FromJson<TravelCheckpoint>(JsonUtility.ToJson(journey.Capture()));
        if (!copy.Restore(saved) || copy.Position != journey.Position || copy.TrailFraction <= 0 ||
            copy.TrailFraction != journey.TrailFraction || copy.Minutes != journey.Minutes)
            throw new InvalidOperationException("山路脚印或途中时刻没有恢复。");

        var flight = new TravelModel();
        flight.Interact();
        flight.ToggleFlight();
        flight.StartRoute(TravelRoute.Road);
        flight.TickAuto(2f);
        if (!copy.Restore(flight.Capture()) || !copy.Flying || !copy.IsAuto || copy.Mana != flight.Mana)
            throw new InvalidOperationException("御剑状态或循路进度没有恢复。");
        while (copy.IsAuto) copy.TickAuto(.05f);
        while (flight.IsAuto) flight.TickAuto(.05f);
        if (copy.CompletedTrips != flight.CompletedTrips || copy.Position != flight.Position ||
            Math.Abs(copy.Mana - flight.Mana) > .01f || Math.Abs(copy.LastTripMinutes - flight.LastTripMinutes) > .01d)
            throw new InvalidOperationException("重读途中存档改变了抵达地点、时日或法力。");
    }

    static double Follow(TravelModel journey, Vector2 goal, int fps)
    {
        journey.StartRoute(TravelRoute.Road);
        double start = journey.Minutes;
        for (int frame = 0; journey.IsAuto && frame < fps * 60; frame++)
            journey.TickAuto(1f / fps);
        if (journey.IsAuto || Vector2.Distance(journey.Position, goal) > 1)
            throw new InvalidOperationException(fps + " 帧下循官道未抵达目的地。");
        return journey.Minutes - start;
    }
}
