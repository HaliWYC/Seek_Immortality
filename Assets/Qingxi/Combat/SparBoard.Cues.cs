using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class SparBoard
{
    const float HealthPopupSeconds = .92f;
    static readonly Color HealthLoss = new Color32(255, 162, 136, 255);
    static readonly Color HealthGain = new Color32(166, 244, 163, 255);
    static readonly Color StrikeGold = new Color32(253, 223, 140, 255);
    static readonly Color WoodLight = new Color32(151, 224, 125, 255);
    static readonly Color EarthLight = new Color32(234, 192, 129, 255);
    static readonly Color FireLight = new Color32(255, 129, 92, 255);
    static readonly Color RelicLight = new Color32(116, 230, 202, 255);

    sealed class HealthPopup
    {
        public Text Label;
        public SpriteRenderer Flash;
        public Vector3 Origin;
        public Vector3 Impact;
        public Vector2 ScreenPoint;
        public Color Color;
        public float Age, HalfWidth;
    }

    struct AttackCue
    {
        public int Source, Target;
        public Color Color;
        public float Age, Duration;
        public bool Restorative;
    }

    readonly List<HealthPopup> healthPopups = new List<HealthPopup>(6);
    RectTransform feedbackLayer, feedbackTop, feedbackBottom;
    Canvas feedbackCanvas;
    Font feedbackFont;
    Camera feedbackCamera;
    SpriteRenderer castAura, castBolt, castTrail;
    Texture2D sparkTexture;
    Sprite sparkSprite;
    AttackCue attackCue;

    public bool PlaybackPaused { get; set; }
    public bool HasAttackCue => attackCue.Duration > 0;

    public void BindCombatEffects(Canvas canvas, Font font, RectTransform top, RectTransform bottom)
    {
        if (!canvas || !font || feedbackLayer) return;
        feedbackCanvas = canvas;
        feedbackFont = font;
        feedbackTop = top;
        feedbackBottom = bottom;
        feedbackLayer = new GameObject("战斗反馈", typeof(RectTransform)).GetComponent<RectTransform>();
        feedbackLayer.SetParent(canvas.transform, false);
        feedbackLayer.SetSiblingIndex(1);
        feedbackLayer.anchorMin = Vector2.zero;
        feedbackLayer.anchorMax = Vector2.one;
        feedbackLayer.offsetMin = feedbackLayer.offsetMax = Vector2.zero;
    }

    int PieceIndex(SparUnit actor)
    {
        if (actor == null || displayedMatch == null) return -1;
        for (int index = 0; index < displayedMatch.Units.Count; index++)
            if (displayedMatch.Units[index] == actor) return index;
        return -1;
    }

    public bool IsMoving(SparUnit actor)
    {
        int index = PieceIndex(actor);
        return index >= 0 && pieces[index].route != null;
    }

    public void BeginAttackCue(SparUnit actor, SparUnit target, QingxiSpellDefinition spell, float seconds, bool restorative = false)
    {
        int sourceIndex = PieceIndex(actor);
        int targetIndex = PieceIndex(target);
        if (sourceIndex < 0 || targetIndex < 0) return;

        if (!castAura)
        {
            castAura = CreateSpark("施法起势", pieces[sourceIndex].stance.sprite, 1540);
            castTrail = CreateSpark("法术残光", SparkSprite(), 1599);
            castBolt = CreateSpark("法术飞光", SparkSprite(), 1600);
        }
        attackCue = new AttackCue
        {
            Source = sourceIndex, Target = targetIndex, Duration = Mathf.Max(.01f, seconds),
            Color = restorative ? HealthGain : SpellLight(spell), Restorative = restorative
        };
        if (sourceIndex != targetIndex)
            pieces[sourceIndex].body.flipX = BodyPoint(targetIndex).x < BodyPoint(sourceIndex).x;
        UpdateAttackCue(0f);
    }

    public void ShowHealthChange(SparUnit recipient, int amount, QingxiSpellDefinition spell = null)
    {
        if (amount == 0) return;
        bool healing = amount > 0;
        string number = healing ? $"+{amount}" : $"−{Math.Abs(amount)}";
        ShowImpactNumber(recipient, "气血飘字", number, healing ? HealthGain : HealthLoss,
            healing ? HealthGain : SpellLight(spell));
    }

    public void ShowBlockedDamage(SparUnit recipient, int amount)
    {
        if (amount > 0) ShowImpactNumber(recipient, "护体飘字", $"挡 {amount}", EarthLight, EarthLight);
    }

    void ShowImpactNumber(SparUnit recipient, string labelType, string number, Color color, Color flashColor)
    {
        int index = PieceIndex(recipient);
        if (index < 0 || !feedbackLayer) return;
        EndAttackCue();

        var label = new GameObject(labelType + " · " + recipient.Name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text))
            .GetComponent<Text>();
        label.rectTransform.SetParent(feedbackLayer, false);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
        label.rectTransform.pivot = new Vector2(.5f, .5f);
        label.rectTransform.sizeDelta = new Vector2(78f, 46f);
        label.font = feedbackFont;
        label.fontSize = 34;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.supportRichText = false;
        label.text = number;
        var outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(29, 28, 24, 242);
        outline.effectDistance = new Vector2(2f, -2f);

        var flash = CreateSpark("命中灵光", SparkSprite(), 1610);
        var popup = new HealthPopup
        {
            Label = label, Flash = flash, Origin = HeadPosition(index),
            Impact = BodyPoint(index), Color = color,
            HalfWidth = Mathf.Max(42f, label.preferredWidth * .59f + 4f)
        };
        flash.color = flashColor;
        flash.transform.position = popup.Impact;
        healthPopups.Add(popup);
        UpdateHealthPopups(0f);
    }

    public void ClearActionCues()
    {
        EndAttackCue();
        foreach (var popup in healthPopups) RemovePopup(popup);
        healthPopups.Clear();
    }

    void UpdateActionCues(float delta)
    {
        if (HasAttackCue) UpdateAttackCue(delta);
        if (healthPopups.Count != 0) UpdateHealthPopups(delta);
    }

    void UpdateAttackCue(float delta)
    {
        attackCue.Age += delta;
        float progress = Mathf.Clamp01(attackCue.Age / attackCue.Duration);
        if (progress >= 1f) { EndAttackCue(); return; }

        Vector3 source = BodyPoint(attackCue.Source);
        Vector3 destination = BodyPoint(attackCue.Target);
        castAura.transform.position = pieces[attackCue.Source].group.transform.position + Vector3.up * .02f;
        castAura.transform.localScale = Vector3.one * Mathf.Lerp(.72f, 1.24f, progress);
        castAura.color = Tinted(attackCue.Color, progress < .47f ? Mathf.Lerp(.28f, .74f, progress / .47f) : (1f - progress) * 1.2f);
        castAura.enabled = true;

        const float launchAt = .42f;
        float flight = Mathf.InverseLerp(launchAt, 1f, progress);
        castBolt.enabled = castTrail.enabled = progress >= launchAt && (attackCue.Source != attackCue.Target || attackCue.Restorative);
        if (!castBolt.enabled) return;

        float arc = (attackCue.Restorative ? .38f : .16f) * Mathf.Sin(Mathf.PI * flight);
        Vector3 point = Vector3.Lerp(source, destination, flight) + Vector3.up * arc;
        Vector3 direction = destination - source;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        var spin = Quaternion.Euler(0f, 0f, angle);
        castBolt.transform.SetPositionAndRotation(point, spin);
        castBolt.transform.localScale = new Vector3(.54f, .32f, 1f);
        castBolt.color = Tinted(Color.Lerp(attackCue.Color, Color.white, .34f), .96f);
        castTrail.transform.SetPositionAndRotation(point - direction.normalized * .24f, spin);
        castTrail.transform.localScale = new Vector3(.69f, .16f, 1f);
        castTrail.color = Tinted(attackCue.Color, .47f);
    }

    void EndAttackCue()
    {
        attackCue = default;
        if (!castAura) return;
        castAura.enabled = castBolt.enabled = castTrail.enabled = false;
    }

    void UpdateHealthPopups(float delta)
    {
        if (!feedbackLayer || !feedbackCanvas) return;
        if (!feedbackCamera)
            feedbackCamera = feedbackCanvas.worldCamera ? feedbackCanvas.worldCamera : Camera.main;
        if (!feedbackCamera) return;

        const float halfLabel = 28f;
        var canvasArea = feedbackLayer.rect;
        Rect top = SparHud.BoundsOnLayer(feedbackTop, feedbackLayer);
        Rect dock = SparHud.BoundsOnLayer(feedbackBottom, feedbackLayer);
        float ceiling = (top.height > 0 ? top.yMin : canvasArea.yMax) - halfLabel;

        for (int i = 0; i < healthPopups.Count; i++)
        {
            var popup = healthPopups[i];
            popup.Age += delta;
            if (popup.Age >= HealthPopupSeconds)
            {
                RemovePopup(popup);
                healthPopups.RemoveAt(i--);
                continue;
            }

            var screen = feedbackCamera.WorldToScreenPoint(popup.Origin);
            Camera canvasCamera = feedbackCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : feedbackCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(feedbackLayer, screen, canvasCamera, out var point);
            float life = popup.Age / HealthPopupSeconds;
            point.y += 49f + 27f * Mathf.SmoothStep(0f, 1f, life);
            float halfWidth = Mathf.Min(popup.HalfWidth, canvasArea.width * .5f);
            point.x = Mathf.Clamp(point.x, canvasArea.xMin + halfWidth, canvasArea.xMax - halfWidth);
            float floor = SparHud.OverlapsHorizontally(dock, point.x - halfWidth, point.x + halfWidth) ? dock.yMax : canvasArea.yMin;
            floor = Mathf.Min(floor + halfLabel, ceiling);
            point.y = Mathf.Clamp(point.y, floor, ceiling);
            for (int prior = 0; prior < i; prior++)
            {
                Vector2 earlier = healthPopups[prior].ScreenPoint;
                if (Mathf.Abs(point.x - earlier.x) < 62f && Mathf.Abs(point.y - earlier.y) < 32f)
                    point.y = earlier.y + 33f <= ceiling ? earlier.y + 33f : Mathf.Max(floor, earlier.y - 33f);
            }
            popup.ScreenPoint = point;
            popup.Label.rectTransform.anchoredPosition = point;
            float scale = Mathf.Lerp(1.18f, 1f, Mathf.InverseLerp(0f, .17f, life));
            popup.Label.rectTransform.localScale = new Vector3(scale, scale, 1f);
            popup.Label.color = Tinted(popup.Color, 1f - Mathf.InverseLerp(.56f, 1f, life));

            float impact = Mathf.InverseLerp(0f, .21f, popup.Age);
            popup.Flash.enabled = impact < 1f;
            popup.Flash.transform.localScale = Vector3.one * Mathf.Lerp(.45f, .95f, impact);
            popup.Flash.color = Tinted(popup.Flash.color, (1f - impact) * .78f);
        }
    }

    void RemovePopup(HealthPopup popup)
    {
        if (popup.Label)
        {
            popup.Label.gameObject.SetActive(false);
            Destroy(popup.Label.gameObject);
        }
        if (popup.Flash)
        {
            popup.Flash.enabled = false;
            Destroy(popup.Flash.gameObject);
        }
    }

    Vector3 BodyPoint(int index) =>
        Vector3.Lerp(pieces[index].group.transform.position, HeadPosition(index), .53f);

    SpriteRenderer CreateSpark(string name, Sprite sprite, int layer)
    {
        var renderer = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        renderer.transform.SetParent(transform, false);
        renderer.sprite = sprite;
        renderer.sortingOrder = layer;
        return renderer;
    }

    Sprite SparkSprite()
    {
        if (sparkSprite) return sparkSprite;
        const int size = 32;
        sparkTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        sparkTexture.name = "斗法灵光";
        sparkTexture.wrapMode = TextureWrapMode.Clamp;
        sparkTexture.filterMode = FilterMode.Bilinear;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Abs(x - (size - 1) * .5f) / (size * .5f);
            float dy = Mathf.Abs(y - (size - 1) * .5f) / (size * .5f);
            float core = Mathf.Clamp01(1f - (dx + dy) * 1.12f);
            float flare = Mathf.Clamp01(1f - Mathf.Min(dx, dy) * 8f - Mathf.Max(dx, dy));
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Max(core * core, flare * .75f) * 255f));
        }
        sparkTexture.SetPixels32(pixels);
        sparkTexture.Apply(false, true);
        sparkSprite = Sprite.Create(sparkTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        return sparkSprite;
    }

    static Color SpellLight(QingxiSpellDefinition spell)
    {
        if (!spell) return StrikeGold;
        if (spell.FaShuType == FaShuType.法宝) return RelicLight;
        switch (spell.Effect)
        {
            case QingxiSpellEffect.Bind: return WoodLight;
            case QingxiSpellEffect.Ward:
            case QingxiSpellEffect.Harmonize: return EarthLight;
            default: return FireLight;
        }
    }

    static Color Tinted(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

    void OnDestroy()
    {
        ReleaseMarkerTiles();
        if (feedbackLayer) Destroy(feedbackLayer.gameObject);
        if (sparkSprite) Destroy(sparkSprite);
        if (sparkTexture) Destroy(sparkTexture);
    }
}
