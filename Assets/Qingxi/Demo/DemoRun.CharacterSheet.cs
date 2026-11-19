using UnityEngine;

public sealed partial class DemoRun
{
    static readonly Color SheetSurface = new Color32(31, 56, 47, 255);
    static readonly Color SheetInset = new Color32(23, 43, 37, 255);
    static readonly Color SheetEdge = new Color32(80, 106, 86, 255);
    static readonly Color SheetJade = new Color32(157, 206, 176, 255);
    static readonly Color[] RootColors =
    {
        new Color32(145, 192, 128, 255), new Color32(224, 145, 117, 255),
        new Color32(216, 170, 114, 255), new Color32(224, 214, 183, 255),
        new Color32(129, 181, 205, 255)
    };
    Sprite sheetPortrait;

    void DrawAttributes()
    {
        var traits = State.Attributes(Sensed);
        var bag = State.Bag;
        var weapon = bag.Equipped(CultivationGearSlot.Weapon);
        var clothing = bag.Equipped(CultivationGearSlot.Clothing);
        DrawSheetIdentity();
        DrawSheetGear(weapon, clothing, bag.Equipped(CultivationGearSlot.Accessory));
        DrawSheetCultivation(traits);
        DrawSheetCombat(traits);
        DrawSheetRoots(SpiritRoots.For(SingleRoot));
    }

    void DrawSheetIdentity()
    {
        SheetCard(new Rect(75, 211, 292, 229));
        Block(new Rect(90, 226, 110, 198), SheetInset);
        Block(new Rect(102, 237, 1, 176), SheetEdge);
        Block(new Rect(187, 237, 1, 176), SheetEdge);
        Block(new Rect(102, 237, 86, 1), SheetEdge);
        Block(new Rect(102, 413, 86, 1), SheetEdge);
        DrawSheetPortrait(new Rect(92, 252, 106, 154));

        string name = string.IsNullOrWhiteSpace(Name) ? "无名" : Name;
        Words(214, 225, 135, 27, Sandbox ? "沙盒" : "剧情", 15, SheetJade);
        Words(212, 249, 143, 58, name, name.Length > 6 ? 20 : 28, Paper, true);
        Words(214, 310, 138, 28, Wanderer ? "游方散修" : Sandbox ? "药铺寄学" : "药铺学徒", 18, Gold);
        Words(214, 342, 138, 25, IsDisciple ? "栖霞外门弟子" : "未入宗门", 16, Soft);
        Block(new Rect(214, 374, 134, 1), SheetEdge);
        Words(214, 377, 138, 29, $"年岁  {State.Age} 岁", 20, Paper, true);
        Words(214, 405, 138, 24, State.Foundation ? "已脱凡寿之限" : "本卷寿限 80 岁", 15, Soft);
    }

    void DrawSheetPortrait(Rect area)
    {
        if (!sheetPortrait)
        {
            var renderer = walker ? walker.GetComponentInChildren<SpriteRenderer>() : null;
            if (!renderer)
            {
                // The regional stage uses the same sprite. Read its reference from the prefab on a world-map load.
                var gateScene = Resources.Load<GameObject>("Regions/QixiaGate");
                if (gateScene) renderer = gateScene.transform.Find("行者")?.GetComponent<SpriteRenderer>();
            }
            if (renderer) sheetPortrait = renderer.sprite;
        }
        if (!sheetPortrait) return;

        var texture = sheetPortrait.texture;
        Rect sprite = sheetPortrait.rect;
        float scale = Mathf.Min(area.width / sprite.width, area.height / sprite.height);
        var size = sprite.size * scale;
        var draw = new Rect(area.x + (area.width - size.x) * .5f, area.yMax - size.y, size.x, size.y);
        GUI.DrawTextureWithTexCoords(draw, texture, new Rect(sprite.x / texture.width, sprite.y / texture.height,
            sprite.width / texture.width, sprite.height / texture.height));
    }

    void DrawSheetGear(CultivationItem weapon, CultivationItem clothing, CultivationItem accessory)
    {
        SheetCard(new Rect(75, 451, 292, 177));
        Words(91, 459, 258, 33, "随身装备", 20, Paper, true);
        DrawSheetGearSlot(new Rect(91, 494, 259, 35), CultivationGearSlot.Weapon, "武器", weapon);
        DrawSheetGearSlot(new Rect(91, 535, 259, 35), CultivationGearSlot.Clothing, "防具", clothing);
        DrawSheetGearSlot(new Rect(91, 576, 259, 35), CultivationGearSlot.Accessory, "法宝", accessory);
    }

    void DrawSheetGearSlot(Rect area, CultivationGearSlot slot, string title, CultivationItem item)
    {
        bool hover = area.Contains(Event.current.mousePosition);
        Block(area, hover ? Pine : SheetInset);
        Block(new Rect(area.x, area.yMax - 1, area.width, 1), SheetEdge);
        Words(area.x + 11, area.y, 54, area.height, title, 16, Gold);
        var spell = item?.GrantedSpell;
        string text = spell == null ? item?.Name ?? "空置" : $"{item.Name} · {spell.Name}";
        Words(area.x + 64, area.y, 166, area.height, text, spell == null ? 18 : 16, item != null ? Paper : Soft);
        Words(area.xMax - 28, area.y, 20, area.height, "›", 24, SheetJade);
        if (GUI.Button(area, GUIContent.none, GUIStyle.none)) OpenGearFromSheet(slot);
    }

    void DrawSheetCultivation(CultivatorAttributes traits)
    {
        SheetCard(new Rect(379, 211, 646, 146));
        Words(398, 220, 130, 25, "境界", 15, SheetJade, true);
        Words(397, 243, 230, 44, traits.Realm.Name, 32, Gold, true);
        Words(641, 253, 133, 26, traits.CanFly ? "可御剑" : "尚不能御剑", 16, traits.CanFly ? SheetJade : Soft);
        Words(818, 222, 181, 23, $"已历 {State.Days} 日", 16, Soft);
        Words(818, 253, 181, 26, $"筑基旧伤  {State.Injuries} 层", 16, State.Injuries > 0 ? RootColors[1] : Soft);

        if (State.Foundation) Words(398, 299, 329, 36, "根基　" + State.QualityName, 18, Paper);
        else
        {
            int limit = DemoCultivation.Bottleneck;
            Words(398, 298, 329, 32, $"炼气修为　{State.Qi} / {limit}", 18, Paper);
            SheetBar(new Rect(398, 334, 322, 6), (float)State.Qi / limit, Gold);
        }
        float mana = Mana;
        Words(754, 298, 247, 32, $"法力　{TravelModel.DisplayMana(mana)} / {traits.ManaCapacity}", 18, Paper);
        SheetBar(new Rect(754, 334, 252, 6), mana / traits.ManaCapacity, SheetJade);
    }

    void DrawSheetCombat(CultivatorAttributes traits)
    {
        SheetCard(new Rect(379, 367, 646, 151));
        Words(398, 378, 300, 29, "野外斗法", 21, Paper, true);
        DrawSheetStat(398, "气血上限", traits.MaxHealth, traits.EquipmentHealth > 0 ? $"装备 +{traits.EquipmentHealth}" : "");
        DrawSheetStat(553, "术威", traits.Attack, traits.EquipmentAttack > 0 ? $"装备 +{traits.EquipmentAttack}" : "");
        DrawSheetStat(708, "步法基础", traits.Movement, "");
        DrawSheetStat(863, "反应", traits.Reaction, $"每 {SparMatch.InitiativeThreshold / traits.Reaction:0.#} 息");
        Block(new Rect(398, 485, 608, 1), SheetEdge);
        Words(398, 487, 605, 25, $"体质  {traits.Fitness}　　 力量  {traits.Strength}　　 经络  {traits.Meridian}", 16, Soft);
    }

    void DrawSheetStat(float x, string labelText, int number, string detail)
    {
        Words(x, 410, 145, 27, labelText, 16, Soft);
        Words(x - 1, 436, 53, 40, number.ToString(), 29, Paper, true);
        if (detail.Length > 0) Words(x + 53, 442, 90, 26, detail, 14, SheetJade);
    }

    void DrawSheetRoots(SpiritRoots roots)
    {
        SheetCard(new Rect(379, 528, 646, 100));
        Words(398, 537, 278, 29, roots.Name, 20, Paper, true);
        Words(824, 541, 182, 23, "五行引气权重", 15, Soft);
        for (int i = 0; i < SpiritRoots.ElementNames.Length; i++)
        {
            float x = 398 + 123 * i;
            int weight = roots.Weight((SpiritElement)i);
            Words(x, 571, 30, 29, SpiritRoots.ElementNames[i], 17, RootColors[i], true);
            Words(x + 34, 571, 72, 29, weight.ToString(), 19, Paper);
            SheetBar(new Rect(x, 607, 115, 5), (float)weight / Mathf.Max(1, roots.TotalWeight), RootColors[i]);
        }
    }

    static void SheetCard(Rect area)
    {
        Block(area, SheetEdge);
        Block(new Rect(area.x + 1, area.y + 1, area.width - 2, area.height - 2), SheetSurface);
    }

    static void SheetBar(Rect area, float fraction, Color fill)
    {
        Block(area, SheetInset);
        area.width *= Mathf.Clamp01(fraction);
        if (area.width > 0) Block(area, fill);
    }
}
