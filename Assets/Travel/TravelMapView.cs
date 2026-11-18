using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Illustrated placeholder maps, painted in the same coordinates as travel and collision.</summary>
public sealed class TravelMapView : IDisposable
{
    private const int Width = 1000;
    private const int Height = 640;
    private static readonly Color32 Paper = C(241, 232, 201);
    private static readonly Color32 Ink = C(47, 70, 65);
    private static readonly Color32 SoftInk = C(92, 113, 95);
    private static readonly Color32 Moss = C(151, 165, 132);
    private static readonly Color32 Sienna = C(137, 92, 49);
    private static readonly Color32 Track = C(39, 120, 105);
    private readonly Dictionary<MapPlace, Texture2D> maps = new Dictionary<MapPlace, Texture2D>();
    private Texture2D walker;
    private Texture2D flyer;
    private GUIStyle label;

    public void Draw(Rect rect, TravelModel model, Font chineseFont)
    {
        if (Event.current.type != EventType.Repaint || rect.width < 1 || rect.height < 1)
            return;

        if (!maps.TryGetValue(model.Place, out var texture))
        {
            texture = PaintMap(model.Place);
            maps.Add(model.Place, texture);
        }

        var parentMatrix = GUI.matrix;
        GUI.matrix = parentMatrix * Matrix4x4.TRS(new Vector3(rect.x, rect.y, 0), Quaternion.identity,
                                                  new Vector3(rect.width / Width, rect.height / Height, 1));
        var prevColor = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(0, 0, Width, Height), texture);

        if (label == null)
            label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter,
                                                   clipping = TextClipping.Overflow, padding = new RectOffset() };
        label.font = chineseFont;
        Caption(936, 16, "北", 13, SoftInk);
        if (model.Place == MapPlace.World)
        {
            DrawDiscoveredTrail(model);
            PlaceLabel(TravelModel.Town, "青溪镇", C(218, 193, 145));
            PlaceLabel(TravelModel.Pass, "松风隘", C(183, 199, 165));
            PlaceLabel(TravelModel.Sect, "栖霞宗", C(218, 193, 145));
            Caption(459, 541, "沿河官道", 14, Sienna);
            Caption(559, 323, "青 岚 山 脊", 17, Ink);
        }
        else
        {
            var buildings = TravelModel.Buildings(model.Place);
            if (model.Place == MapPlace.Town)
            {
                Caption(buildings[1].center.x, buildings[1].yMax + 17, "青溪药铺", 15, Ink);
                Caption(buildings[3].center.x, buildings[3].yMax + 17, "客栈", 15, Ink);
            }
            else if (model.Place == MapPlace.Sect)
                Caption(buildings[0].center.x, buildings[0].yMax + 18, "山门大殿", 16, Ink);
            else
                Caption(505, 192, "古道石阶", 16, Ink);

            PlaceLabel(TravelModel.LocalGate, "出境", C(218, 193, 145));
        }

        if (walker == null)
            walker = PaintPlayer(false);
        if (flyer == null)
            flyer = PaintPlayer(true);
        var position = model.Position;
        var player = new Rect(position.x - 23, position.y - 33, 46, 46);
        if (player.y < 0)
        {
            float visible = player.yMax / player.height;
            GUI.DrawTextureWithTexCoords(new Rect(player.x, 0, player.width, player.yMax),
                                         model.Flying ? flyer : walker, new Rect(0, 0, 1, visible));
        }
        else
            GUI.DrawTexture(player, model.Flying ? flyer : walker);
        GUI.color = prevColor;
        GUI.matrix = parentMatrix;
    }

    private void Caption(float x, float y, string value, int size, Color ink)
    {
        label.fontSize = size;
        label.fontStyle = FontStyle.Normal;
        label.normal.textColor = new Color(1f, .97f, .85f, .86f);
        var area = new Rect(x - 84, y - 13, 168, 26);
        GUI.Label(new Rect(area.x + 1, area.y + 1, area.width, area.height), value, label);
        label.normal.textColor = ink;
        GUI.Label(area, value, label);
    }

    private void PlaceLabel(Vector2 at, string value, Color32 keyline)
    {
        float w = value.Length * 18 + 19;
        var bounds = new Rect(at.x - w * .5f, at.y + 16, w, 23);
        Fill(bounds, keyline);
        Fill(new Rect(bounds.x + 1, bounds.y + 1, bounds.width - 2, bounds.height - 2), C(249, 240, 212));
        Caption(at.x, bounds.center.y, value, 16, Ink);
    }

    private static void DrawDiscoveredTrail(TravelModel model)
    {
        var path = TravelModel.Trail;
        for (int i = 0; i < path.Length - 1; i++)
        {
            bool recorded = model.SegmentSeen(i);
            Vector2 a = path[i], b = path[i + 1], dir = (b - a).normalized;
            float distance = Vector2.Distance(a, b);
            for (float d = 0; d < distance; d += 13)
            {
                var from = a + dir * d;
                if (recorded)
                    GuiLine(from, a + dir * Mathf.Min(distance, d + 9), 3.5f, Track);
                else if (!model.Flying && (from - model.Position).sqrMagnitude < 75 * 75)
                    GuiLine(from, a + dir * Mathf.Min(distance, d + 5), 2.5f, C(161, 149, 110));
            }
        }
    }

    private static void GuiLine(Vector2 from, Vector2 to, float width, Color color)
    {
        var matrix = GUI.matrix;
        var rotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
        GUI.matrix = matrix * Matrix4x4.TRS(from, rotation, Vector3.one);
        Fill(new Rect(0, -width * .5f, Vector2.Distance(from, to), width), color);
        GUI.matrix = matrix;
    }

    private static void Fill(Rect rect, Color32 color)
    {
        var old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }

    private static Texture2D PaintMap(MapPlace place)
    {
        var canvas = new Sheet(Width, Height, 1841 + (int)place * 13);
        canvas.Parchment();
        if (place == MapPlace.World)
            PaintRegion(canvas);
        else
            PaintLocal(canvas, place);
        // A printed graticule indexes the edges without obscuring exploration.
        for (int x = 0; x < Width; x += 25)
        {
            int length = x % 100 == 0 ? 8 : 3;
            canvas.Line(x, 0, x, length, 1, Moss);
            canvas.Line(x, Height - length, x, Height - 1, 1, Moss);
        }
        for (int y = 0; y < Height; y += 25)
        {
            int length = y % 100 == 0 ? 8 : 3;
            canvas.Line(0, y, length, y, 1, Moss);
            canvas.Line(Width - length, y, Width - 1, y, 1, Moss);
        }
        // Compass rose.
        canvas.Polygon(C(247, 239, 207), 936, 30, 945, 59, 936, 75, 927, 59);
        canvas.Polygon(Ink, 936, 29, 944, 59, 936, 53);
        canvas.Polygon(SoftInk, 936, 53, 928, 59, 936, 75);
        canvas.Line(917, 58, 954, 58, 1, SoftInk);
        return canvas.Finish(place.ToString());
    }

    private static void PaintRegion(Sheet c)
    {
        // Shallow water follows the southern foothills. The two tracks meet at the town and sect.
        var river =
            Points(-25, 164, 32, 233, 35, 286, 83, 352, 135, 414, 128, 450, 186, 487, 217, 535, 318, 542, 391, 565,
                   469, 557, 554, 572, 646, 552, 703, 508, 755, 497, 806, 460, 867, 443, 927, 452, 1005, 420);
        river = RoundCorners(river);
        c.Path(river, 12, C(150, 180, 161));
        c.Path(river, 7, C(182, 202, 178));
        c.Path(river, 1, C(217, 222, 192));
        var arm = Points(235, 650, 268, 618, 269, 583, 299, 563, 318, 542);
        c.Path(arm, 4, C(161, 186, 163));
        Fields(c, 88, 418, 50, 15);
        Fields(c, 184, 417, 57, 17);
        Fields(c, 780, 343, 45, 12);

        // Contours are concentric around the actual ridge; the mountain trail stays passable.
        var ridge = TravelModel.Ridge;
        var centre = Vector2.zero;
        foreach (var p in ridge)
            centre += p;
        centre /= ridge.Length;
        var contour = new Vector2[ridge.Length + 1];
        Array.Copy(ridge, contour, ridge.Length);
        contour[ridge.Length] = ridge[0];
        c.Polygon(RoundCorners(contour), C(209, 211, 178));
        for (int i = 0; i < 6; i++)
        {
            var ring = new Vector2[ridge.Length + 1];
            for (int j = 0; j < ridge.Length; j++)
                ring[j] = Vector2.Lerp(ridge[j], centre, i * .065f);
            ring[ridge.Length] = ring[0];
            c.Path(RoundCorners(ring), 1, C(177 + i * 3, 187 + i * 2, 155 + i * 2));
        }

        // The peak symbols keep to blocked ridge land, leaving negative space at the pass.
        for (int y = 106; y < 416; y += 35)
            for (int x = 270; x < 749; x += 47)
            {
                var p = new Vector2(x + c.Rng.Next(-10, 11), y + c.Rng.Next(-5, 6));
                if (!TravelModel.Walkable(MapPlace.World, p) && !NearPath(p, TravelModel.Trail, 36))
                    Peak(c, p.x, p.y, c.Rng.Next(46, 85), c.Rng.Next(25, 61));
            }

        // Woodland glyphs cluster in the valley and around the margins.
        Vector2[] groves = Points(91, 125, 202, 196, 796, 86, 883, 297, 802, 548, 633, 598, 469, 585, 114, 524, 208,
                                  354, 746, 348, 237, 74, 343, 469, 62, 305, 907, 563);
        foreach (var g in groves)
        {
            for (int i = 0; i < 16; i++)
            {
                var p = g + new Vector2(c.Rng.Next(-39, 40), c.Rng.Next(-20, 21));
                if (NearPath(p, TravelModel.Road, 18) || NearPath(p, TravelModel.Trail, 18))
                    continue;
                Pine(c, p.x, p.y, c.Rng.Next(8, 20), false);
            }
        }
        var road = RoundCorners(TravelModel.Road);
        c.Path(road, 14, C(205, 182, 132));
        c.Path(road, 10, C(237, 213, 160));
        c.Dashed(road, 2.5f, C(154, 112, 62));

        var town = TravelModel.Town;
        House(c, new Rect(town.x - 27, town.y - 16, 19, 17), false);
        House(c, new Rect(town.x - 10, town.y - 28, 26, 24), false);
        House(c, new Rect(town.x + 5, town.y - 12, 23, 18), false);
        var sect = TravelModel.Sect;
        c.Oval(sect.x, sect.y + 3, 34, 6, C(164, 172, 142));
        Gate(c, sect.x, sect.y + 2, 45);
        var pass = TravelModel.Pass;
        Stele(c, pass.x - 6, pass.y + 3, 22);
        Pine(c, pass.x + 12, pass.y + 4, 27, true);
    }

    private static void PaintLocal(Sheet c, MapPlace place)
    {
        var buildings = TravelModel.Buildings(place);
        var gate = TravelModel.LocalGate;
        // Untilled grass, worn footpaths and buildings read as separate surfaces at local scale.
        for (int i = 0; i < 780; i++)
        {
            int x = c.Rng.Next(10, Width - 10), y = c.Rng.Next(15, Height - 5);
            if (x > 455 && x < 545)
                continue;
            var ink = C(182, 184, 150);
            c.Line(x, y, x - 2, y - 3, 1, ink);
            c.Line(x, y, x + 2, y - 2, 1, ink);
        }
        var path = Points(500, 650, 500, 525, 492, 450, 506, 343, 493, 268, 469, 245, 442, 225, 442, 173);
        if (place == MapPlace.Sect)
            path = Points(500, 650, 500, 525, 500, 418, 500, 338, 500, 182);
        if (place == MapPlace.Pass)
            path = Points(500, 650, 500, 525, 453, 442, 425, 365, 465, 288, 553, 216, 530, 151, 565, 53);
        c.Path(path, 52, C(208, 194, 156));
        c.Path(path, 48, C(226, 211, 170));
        if (place == MapPlace.Pass)
        {
            for (int i = 0; i < 10; i++)
            {
                var p = Vector2.Lerp(new Vector2(480, 274), new Vector2(551, 211), i / 10f);
                c.Line(p.x - 12, p.y - 10, p.x + 12, p.y + 10, 2, C(161, 157, 129));
                c.Line(p.x - 11, p.y - 9, p.x + 11, p.y + 11, 1, C(245, 232, 191));
            }
            foreach (var r in buildings)
                RockyOutcrop(c, r);
            Stele(c, gate.x - 47, gate.y, 48);
        }
        else
        {
            var side = place == MapPlace.Town
                ? Points(246, 231, 253, 254, 291, 265, 373, 271, 500, 283, 645, 270, 689, 257, 689, 236)
                : Points(245, 326, 256, 342, 325, 350, 500, 350, 674, 350, 746, 342, 746, 326);
            side = RoundCorners(side);
            c.Path(side, 33, C(211, 197, 159));
            c.Path(side, 30, C(226, 211, 171));
            if (place == MapPlace.Town)
            {
                var lane = RoundCorners(Points(278, 379, 278, 404, 308, 414, 384, 420, 494, 435,
                    578, 422, 703, 429, 732, 415, 732, 393));
                c.Path(lane, 19, C(221, 206, 167));
            }
            // The paving and small courtyards indicate the local walking scale.
            for (int i = 0; i < 160; i++)
            {
                int x = c.Rng.Next(475, 524), y = c.Rng.Next(285, 632);
                if (!NearPath(new Vector2(x, y), path, 20)) continue;
                c.Line(x, y, x + c.Rng.Next(2, 8), y, 1, C(201, 185, 145));
            }
            for (int i = 0; i < buildings.Length; i++)
            {
                var r = buildings[i];
                bool temple = place == MapPlace.Sect;
                House(c, r, temple);
                if (place == MapPlace.Town && (i == 1 || i == 3))
                {
                    // Upright shop streamers use separate colours for the apothecary and the inn.
                    float x = r.xMax - 1, y = r.yMax;
                    c.Line(x, y, x, y - 49, 2, Sienna);
                    c.Line(x - 11, y - 47, x + 8, y - 47, 2, Ink);
                    c.Polygon(i == 1 ? C(168, 83, 58) : C(89, 122, 110),
                        x - 10, y - 46, x - 1, y - 46, x - 1, y - 24,
                        x - 4, y - 27, x - 7, y - 23, x - 10, y - 26);
                    c.Line(x - 6, y - 42, x - 6, y - 34, 1, Paper);
                }
                if (temple && i == 0)
                {
                    c.Polygon(C(206, 194, 157), r.x + 16, r.yMax + 1, r.xMax - 16, r.yMax + 1, r.xMax - 4,
                              r.yMax + 11, r.x + 4, r.yMax + 11);
                    c.Line(r.x + 6, r.yMax + 8, r.xMax - 6, r.yMax + 8, 1, SoftInk);
                }
            }
            if (place == MapPlace.Town)
            {
                Fields(c, 110, 375, 82, 27);
                Fields(c, 162, 426, 85, 27);
                Fields(c, 814, 431, 76, 20);
            }
            else
            {
                Stele(c, 409, 363, 44);
                Stele(c, 594, 363, 44);
                c.Oval(500, 337, 38, 13, C(181, 184, 150));
                c.Oval(500, 337, 32, 10, C(219, 209, 171));
            }
        }
        Vector2[] woods =
            Points(75, 109, 76, 438, 895, 124, 907, 344, 303, 531, 729, 529, 159, 553, 780, 70, 320, 72, 919, 564);
        foreach (var cluster in woods)
        {
            for (int i = 0; i < 10; i++)
            {
                float x = cluster.x + c.Rng.Next(-31, 32), y = cluster.y + c.Rng.Next(-16, 17);
                bool blocked = false;
                foreach (var r in buildings)
                    if (r.Contains(new Vector2(x, y)))
                    {
                        blocked = true;
                        break;
                    }
                if (!blocked)
                    Pine(c, x, y, c.Rng.Next(14, 32), true);
            }
        }
        Gate(c, gate.x, gate.y - 2, 63);
    }

    // Trim only the ends of straight stretches so illustrated roads stay inside their gameplay corridors.
    private static Vector2[] RoundCorners(Vector2[] points)
    {
        var rounded = new List<Vector2> { points[0] };
        for (int i = 1; i < points.Length - 1; i++)
        {
            Vector2 previous = points[i - 1], corner = points[i], next = points[i + 1];
            float radius = Mathf.Min(25, Vector2.Distance(previous, corner) * .24f,
                Vector2.Distance(next, corner) * .24f);
            Vector2 start = corner + (previous - corner).normalized * radius;
            Vector2 end = corner + (next - corner).normalized * radius;
            for (int step = 0; step <= 6; step++)
            {
                float t = step / 6f;
                rounded.Add(Vector2.Lerp(Vector2.Lerp(start, corner, t), Vector2.Lerp(corner, end, t), t));
            }
        }
        rounded.Add(points[points.Length - 1]);
        return rounded.ToArray();
    }

    private static void Peak(Sheet c, float x, float y, float w, float h)
    {
        float tip = x - w * .12f;
        c.Polygon(C(161, 178, 149), x - w * .56f, y, x - w * .32f, y - h * .42f, tip, y - h, x + w * .14f,
                  y - h * .56f, x + w * .27f, y - h * .44f, x + w * .54f, y);
        c.Polygon(C(111, 143, 124), tip, y - h, x + w * .14f, y - h * .56f, x + w * .27f, y - h * .44f,
                  x + w * .54f, y, x + w * .13f, y - 2, x + w * .03f, y - h * .34f);
        c.Polygon(C(202, 208, 176), x - w * .32f, y - h * .42f, tip, y - h, x - w * .14f, y - h * .53f,
                  x - w * .32f, y - h * .20f, x - w * .22f, y - h * .49f);
        c.Line(tip, y - h, x + w * .14f, y - h * .56f, 1.2f, SoftInk);
        c.Line(x + w * .08f, y - h * .49f, x + w * .19f, y - h * .23f, 1, SoftInk);
        c.Line(x - w * .29f, y - h * .32f, x - w * .44f, y - h * .12f, 1, Moss);
        // Side hatching suggests folded rock and breaks up the large wash shapes.
        for (int i = 0; i < 6; i++)
        {
            float a = .15f + i * .036f, b = .32f - i * .024f;
            c.Line(x + w * a, y - h * b, x + w * (a + .04f), y - h * (b - .12f), 1, C(139, 160, 135));
        }
    }

    private static void Pine(Sheet c, float x, float y, float h, bool deep)
    {
        var ink = deep ? C(77, 104, 85) : C(102, 129, 107);
        c.Line(x, y - 1, x + .4f, y - h * .8f, 1.3f, ink);
        for (int i = 0; i < 3; i++)
        {
            float tip = y - h + h * i * .22f, r = h * (.14f + i * .043f);
            c.Polygon(ink, x, tip, x - r, tip + h * .37f, x - r * .46f, tip + h * .33f, x, tip + h * .40f,
                      x + r * .8f, tip + h * .35f, x + r * .45f, tip + h * .25f);
        }
    }

    private static void Fields(Sheet c, float x, float y, float w, float h)
    {
        c.Polygon(C(198, 194, 147), x, y, x + w, y - 5, x + w + 12, y + h - 3, x + 12, y + h + 2);
        for (int i = 1; i < 7; i++)
            c.Line(x + w * i / 7f, y - i * .6f, x + w * i / 7f + 11, y + h - i * .6f, 1.4f, C(156, 161, 122));
    }

    private static void House(Sheet c, Rect r, bool temple)
    {
        float x = r.x, y = r.y, w = r.width, h = r.height;
        Color32 roof = temple ? C(73, 97, 94) : C(92, 112, 103);
        c.Polygon(C(163, 158, 123), x + 5, y + h * .78f, x + w - 2, y + h * .74f, x + w + 4, y + h, x + 5,
                  y + h + 3);
        c.Polygon(C(225, 205, 162), x + w * .13f, y + h * .47f, x + w * .88f, y + h * .47f, x + w * .88f,
                  y + h * .91f, x + w * .13f, y + h * .91f);
        c.Line(x + w * .14f, y + h * .86f, x + w * .86f, y + h * .86f, Mathf.Max(1, h * .035f), Sienna);
        for (int i = 0; i < 4; i++)
            c.Line(x + w * (.18f + i * .21f), y + h * .55f, x + w * (.18f + i * .21f), y + h * .87f,
                   Mathf.Max(1, w * .022f), C(133, 86, 58));
        c.Polygon(roof, x, y + h * .48f, x + w * .13f, y + h * .45f, x + w * .31f, y + h * .12f, x + w * .69f,
                  y + h * .12f, x + w * .86f, y + h * .45f, x + w, y + h * .48f, x + w * .9f, y + h * .59f,
                  x + w * .1f, y + h * .59f);
        c.Polygon(C(130, 148, 130), x + w * .14f, y + h * .45f, x + w * .31f, y + h * .12f, x + w * .69f,
                  y + h * .12f, x + w * .66f, y + h * .22f);
        for (int i = 0; i < 8; i++)
            c.Line(x + w * (.31f + .38f * i / 7), y + h * .17f, x + w * (.10f + .8f * i / 7), y + h * .51f, 1,
                   C(115, 136, 122));
        c.Line(x + w * .03f, y + h * .51f, x + w * .97f, y + h * .51f, Mathf.Max(1, w * .014f), Ink);
        c.Line(x + w * .29f, y + h * .105f, x + w * .71f, y + h * .105f, Mathf.Max(1, w * .018f), Ink);
        c.Polygon(C(124, 83, 62), x + w * .432f, y + h * .59f, x + w * .57f, y + h * .59f, x + w * .57f,
                  y + h * .9f, x + w * .432f, y + h * .9f);
        if (w < 60) return;
        c.Line(x + w * .50f, y + h * .61f, x + w * .50f, y + h * .89f, 1, C(81, 66, 52));
        for (int i = 0; i < 2; i++)
        {
            float left = x + w * (.245f + i * .40f), right = left + w * .11f;
            float top = y + h * .64f, bottom = y + h * .78f;
            c.Polygon(C(98, 105, 87), left, top, right, top, right, bottom, left, bottom);
            for (int j = 1; j < 4; j++)
                c.Line(left + (right - left) * j / 4, top, left + (right - left) * j / 4, bottom, 1, C(191, 167, 125));
            c.Line(left, (top + bottom) / 2, right, (top + bottom) / 2, 1, C(191, 167, 125));
            float hook = x + w * (.18f + i * .64f);
            c.Line(hook, y + h * .54f, hook, y + h * .66f, 1, Ink);
            c.Oval(hook, y + h * .69f, h * .044f, h * .060f, C(166, 76, 53));
            c.Line(hook, y + h * .74f, hook, y + h * .78f, 1, Sienna);
        }
        if (temple)
        {
            // A raised roof over the central hall distinguishes the monastery from a village house.
            c.Polygon(C(87, 105, 97), x + w * .22f, y + h * .23f, x + w * .35f, y + h * .08f,
                x + w * .65f, y + h * .08f, x + w * .78f, y + h * .23f,
                x + w * .69f, y + h * .28f, x + w * .31f, y + h * .28f);
            c.Line(x + w * .32f, y + h * .28f, x + w * .68f, y + h * .28f, 2, Ink);
        }
    }

    private static void RockyOutcrop(Sheet c, Rect r)
    {
        c.Polygon(C(165, 172, 142), r.x, r.yMax - 8, r.x + 7, r.y + 12, r.x + r.width * .37f, r.y, r.xMax - 12,
                  r.y + 7, r.xMax, r.yMax - 5, r.x + r.width * .61f, r.yMax);
        Peak(c, r.center.x - r.width * .14f, r.yMax - 7, r.width * .75f, r.height * .85f);
        Peak(c, r.center.x + r.width * .20f, r.yMax - 2, r.width * .62f, r.height * .68f);
    }

    private static void Gate(Sheet c, float x, float y, float w)
    {
        c.Oval(x, y + 1, w * .56f, w * .11f, C(170, 166, 134));
        float h = w * .51f;
        c.Line(x - w * .29f, y, x - w * .29f, y - h, w * .075f, Sienna);
        c.Line(x + w * .29f, y, x + w * .29f, y - h, w * .075f, Sienna);
        c.Line(x - w * .38f, y - h * .74f, x + w * .38f, y - h * .74f, w * .055f, Sienna);
        c.Polygon(Ink, x - w * .55f, y - h * 1.11f, x - w * .34f, y - h * 1.07f, x - w * .18f, y - h * 1.37f,
                  x + w * .18f, y - h * 1.37f, x + w * .34f, y - h * 1.07f, x + w * .55f, y - h * 1.11f,
                  x + w * .44f, y - h * .9f, x - w * .44f, y - h * .9f);
        c.Line(x - w * .16f, y - h * 1.34f, x + w * .16f, y - h * 1.34f, 2, Moss);
    }

    private static void Stele(Sheet c, float x, float y, float h)
    {
        float w = h * .36f;
        c.Polygon(C(132, 153, 135), x - w, y, x - w * .72f, y - h * .09f, x - w * .62f, y - h * .85f, x - w * .32f,
                  y - h, x + w * .47f, y - h * .94f, x + w * .7f, y - h * .10f, x + w, y);
        c.Line(x - w * .34f, y - h * .78f, x - w * .25f, y - h * .33f, 1.5f, Ink);
    }

    private static bool NearPath(Vector2 point, Vector2[] path, float distance)
    {
        for (int i = 1; i < path.Length; i++)
        {
            var segment = path[i] - path[i - 1];
            float t = Mathf.Clamp01(Vector2.Dot(point - path[i - 1], segment) / segment.sqrMagnitude);
            if ((point - (path[i - 1] + segment * t)).sqrMagnitude < distance * distance)
                return true;
        }
        return false;
    }

    private static Texture2D PaintPlayer(bool flying)
    {
        var c = new Sheet(46, 46, 8);
        if (flying)
        {
            c.Polygon(C(46, 106, 96), 2, 37, 22, 32, 42, 22, 30, 35, 13, 39);
            c.Line(7, 37, 39, 25, 1.6f, C(249, 235, 189));
        }
        else
        {
            c.Oval(23, 34, 12, 4, C(84, 93, 76));
            c.Oval(23, 34, 9, 2, C(188, 182, 143));
        }
        c.Oval(23, 19, 10, 11, C(251, 240, 211));
        c.Polygon(C(249, 240, 211), 12, 32, 17, 21, 27, 19, 35, 32, 25, 36);
        c.Polygon(C(143, 63, 45), 15, 30, 19, 21, 25, 21, 31, 30, 25, 33);
        c.Polygon(C(202, 102, 69), 18, 28, 21, 19, 24, 20, 24, 30);
        c.Line(29, 24, 34, 34, 2, Ink);
        c.Oval(22, 17, 4, 5, C(81, 72, 61));
        c.Polygon(Ink, 11, 16, 22, 7, 32, 16, 25, 18, 18, 18);
        c.Line(12, 16, 22, 8, 1, C(222, 194, 146));
        return c.Finish(flying ? "御剑" : "行人");
    }

    private static Color32 C(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);
    private static Vector2[] Points(params float[] xy)
    {
        var result = new Vector2[xy.Length / 2];
        for (int i = 0; i < result.Length; i++)
            result[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
        return result;
    }

    public void Dispose()
    {
        foreach (var texture in maps.Values)
            UnityEngine.Object.Destroy(texture);
        maps.Clear();
        if (walker != null)
            UnityEngine.Object.Destroy(walker);
        if (flyer != null)
            UnityEngine.Object.Destroy(flyer);
    }

    // Origin at the upper left, like the travel model and GUI. The texture is inverted on upload.
    private sealed class Sheet
    {
        private readonly int width;
        private readonly int height;
        private readonly Color32[] pixels;
        internal readonly System.Random Rng;
        internal Sheet(int width, int height, int seed)
        {
            this.width = width;
            this.height = height;
            pixels = new Color32[width * height];
            Rng = new System.Random(seed);
        }

        internal void Parchment()
        {
            // The wash changes slowly across the page; sample it every eight pixels, then add fine paper grain.
            const int spacing = 8;
            int cols = width / spacing + 2, rows = height / spacing + 2;
            var wash = new float[rows * cols];
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    int x = i * spacing, y = j * spacing;
                    wash[j * cols + i] = Mathf.PerlinNoise(x * .007f, y * .007f) * 9 +
                                         Mathf.PerlinNoise(x * .023f + 13, y * .023f + 6) * 4;
                }
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = (y / spacing) * cols + x / spacing;
                    float u = (x % spacing) / (float)spacing, v = (y % spacing) / (float)spacing;
                    float upper = wash[i] + (wash[i + 1] - wash[i]) * u;
                    float lower = wash[i + cols] + (wash[i + cols + 1] - wash[i + cols]) * u;
                    float mist = upper + (lower - upper) * v;
                    int grain = Rng.Next(-3, 4) + (int)mist - 8;
                    Set(x, y, C(Paper.r + grain, Paper.g + grain, Paper.b + grain));
                }
        }

        private void Set(int x, int y, Color32 color)
        {
            if ((uint)x < width && (uint)y < height)
                pixels[(height - 1 - y) * width + x] = color;
        }

        private void Span(int y, int x0, int x1, Color32 color)
        {
            if ((uint)y >= height)
                return;
            int start = Mathf.Max(0, x0), end = Mathf.Min(width - 1, x1);
            int offset = (height - 1 - y) * width;
            for (int x = start; x <= end; x++)
                pixels[offset + x] = color;
        }

        internal void Oval(float x, float y, float rx, float ry, Color32 color)
        {
            int from = Mathf.CeilToInt(y - ry), to = Mathf.FloorToInt(y + ry);
            for (int row = from; row <= to; row++)
            {
                float dy = (row - y) / ry;
                float dx = rx * Mathf.Sqrt(Mathf.Max(0, 1 - dy * dy));
                Span(row, Mathf.RoundToInt(x - dx), Mathf.RoundToInt(x + dx), color);
            }
        }

        internal void Line(float ax, float ay, float bx, float by, float weight, Color32 color)
        {
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(bx - ax), Mathf.Abs(by - ay)));
            if (steps == 0)
            {
                Set(Mathf.RoundToInt(ax), Mathf.RoundToInt(ay), color);
                return;
            }
            float radius = weight * .5f;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float x = Mathf.LerpUnclamped(ax, bx, t), y = Mathf.LerpUnclamped(ay, by, t);
                if (weight <= 1.4f)
                    Set(Mathf.RoundToInt(x), Mathf.RoundToInt(y), color);
                else
                    Oval(x, y, radius, radius, color);
            }
        }

        internal void Path(Vector2[] path, float weight, Color32 color)
        {
            for (int i = 1; i < path.Length; i++)
                Line(path[i - 1].x, path[i - 1].y, path[i].x, path[i].y, weight, color);
        }

        internal void Dashed(Vector2[] path, float weight, Color32 color)
        {
            float run = 0;
            for (int i = 1; i < path.Length; i++)
            {
                var a = path[i - 1];
                var b = path[i];
                var dir = (b - a).normalized;
                float length = Vector2.Distance(a, b);
                for (float d = 0; d < length; d += 1)
                {
                    if ((run + d) % 13 >= 8)
                        continue;
                    var p = a + dir * d;
                    Oval(p.x, p.y, weight * .5f, weight * .5f, color);
                }
                run += length;
            }
        }

        internal void Polygon(Color32 color, params float[] xy) => Polygon(Points(xy), color);
        internal void Polygon(Vector2[] vertices, Color32 color)
        {
            float ymin = vertices[0].y, ymax = ymin;
            for (int i = 1; i < vertices.Length; i++)
            {
                ymin = Mathf.Min(ymin, vertices[i].y);
                ymax = Mathf.Max(ymax, vertices[i].y);
            }
            var hits = new float[vertices.Length];
            for (int y = Mathf.Max(0, Mathf.CeilToInt(ymin)); y <= Mathf.Min(height - 1, (int)ymax); y++)
            {
                float scan = y + .5f;
                int n = 0;
                for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
                {
                    var p = vertices[j];
                    var q = vertices[i];
                    if ((p.y <= scan && q.y > scan) || (q.y <= scan && p.y > scan))
                        hits[n++] = p.x + (scan - p.y) * (q.x - p.x) / (q.y - p.y);
                }
                Array.Sort(hits, 0, n);
                for (int i = 0; i + 1 < n; i += 2)
                    Span(y, Mathf.CeilToInt(hits[i]), Mathf.FloorToInt(hits[i + 1]), color);
            }
        }

        internal Texture2D Finish(string mapName)
        {
            var texture =
                new Texture2D(width, height, TextureFormat.RGBA32,
                              false) { name = "行旅图 · " + mapName, filterMode = FilterMode.Bilinear,
                                       wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
