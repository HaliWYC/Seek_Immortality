using System;
using UnityEngine;

[Serializable]
public sealed class TravelCheckpoint
{
    public int version = 1;
    public MapPlace place, anchor;
    public Vector2 position, entrance, launch, landing;
    public double minutes, departure, lastDuration;
    public bool flying, tracking;
    public float mana, road, trail, wild, walk, flown, ridge;
    public int guided, index, direction, completed;
    public byte[] footfalls;
    public string lastTrip;
    public string[] log;
}

public sealed partial class TravelModel
{
    public TravelCheckpoint Capture() => new TravelCheckpoint
    {
        place = Place, anchor = worldAnchor, position = Position, entrance = returnToWorld,
        launch = ridgeLaunch, landing = ridgeLanding, minutes = Minutes, departure = departed,
        lastDuration = LastTripMinutes, flying = Flying, tracking = trackingTrip, mana = Mana,
        road = roadDistance, trail = trailDistance, wild = otherDistance, walk = walkedDistance,
        flown = flownDistance, ridge = ridgeDistance, completed = CompletedTrips, lastTrip = LastTripLabel,
        guided = guidedPath == Road ? 1 : guidedPath == Trail ? 2 : 0, index = guidedIndex,
        direction = guidedDirection, footfalls = (byte[])footprints.Clone()
    };

    public bool Restore(TravelCheckpoint saved)
    {
        if (saved == null || saved.version != 1 || !Enum.IsDefined(typeof(MapPlace), saved.place)
            || !InBounds(saved.position) || !InBounds(saved.entrance)
            || !(saved.minutes >= 0) || double.IsInfinity(saved.minutes)
            || !(saved.mana >= 0 && saved.mana <= 100)) return false;
        bool flying = saved.flying && saved.place == MapPlace.World && saved.mana > 0;
        if (!Walkable(saved.place, saved.position) &&
            !(flying && Walkable(MapPlace.World, saved.launch) && Walkable(MapPlace.World, saved.landing))) return false;
        Place = saved.place;
        Position = saved.position;
        worldAnchor = saved.anchor == MapPlace.Sect ? MapPlace.Sect : MapPlace.Town;
        returnToWorld = saved.entrance;
        ridgeLaunch = saved.launch;
        ridgeLanding = saved.landing;
        Minutes = saved.minutes;
        Mana = saved.mana;
        Flying = flying;
        overRidge = flying && !Walkable(Place, Position);
        departed = Math.Min(Minutes, Math.Max(0d, saved.departure));
        trackingTrip = saved.tracking;
        roadDistance = saved.road;
        trailDistance = saved.trail;
        otherDistance = saved.wild;
        walkedDistance = saved.walk;
        flownDistance = saved.flown;
        ridgeDistance = saved.ridge;
        CompletedTrips = Math.Max(0, saved.completed);
        LastTripLabel = saved.lastTrip ?? "";
        LastTripMinutes = Math.Max(0d, saved.lastDuration);
        RestoreSeen(0);
        if (saved.footfalls != null && saved.footfalls.Length == footprints.Length)
        {
            Array.Copy(saved.footfalls, footprints, footprints.Length);
            for (int i = 0; i < footprints.Length; i++)
                if (footprints[i] == byte.MaxValue) SeenMask |= 1 << i;
        }
        guidedPath = Place == MapPlace.World && saved.guided == 1 ? Road :
            Place == MapPlace.World && saved.guided == 2 && TrailKnown ? Trail : null;
        if (guidedPath != null && (saved.index < 0 || saved.index >= guidedPath.Length || Math.Abs((long)saved.direction) != 1)) guidedPath = null;
        guidedIndex = saved.index;
        guidedDirection = saved.direction;
        Notice = IsAuto ? "沿上次选定的路线继续赶路。" : "行囊已整，从此处继续。";
        return true;
    }
}
