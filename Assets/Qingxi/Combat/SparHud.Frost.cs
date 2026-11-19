using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed partial class SparHud
{
    const int FrostDownsample = 3;
    const float FrostFeather = 12f;
    static readonly int FrostViewport = Shader.PropertyToID("_FrostViewport");
    static readonly int FrostShape = Shader.PropertyToID("_FrostShape");

    RectTransform frostCanvas;
    Image originalDock;
    RenderTexture frostScene;
    Camera frostCamera;
    RawImage frostInitiative, frostLocation, frostControls, frostDock;

    void CreateBattleFrost()
    {
        var shader = Resources.Load<Shader>("QingxiBattleFrost");
        if (!shader || !shader.isSupported) return;
        frostCanvas = (RectTransform)topBar.parent;
        originalDock = bottomBar.GetComponent<Image>();
        frostInitiative = CreateFrostSurface(shader, "反应轴场景虚化");
        frostLocation = CreateFrostSurface(shader, "战斗地名场景虚化");
        frostControls = CreateFrostSurface(shader, "回合操作场景虚化");
        frostDock = CreateFrostSurface(shader, "法术栏场景虚化");
    }

    RawImage CreateFrostSurface(Shader shader, string label)
    {
        var graphic = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage))
            .GetComponent<RawImage>();
        var rect = graphic.rectTransform;
        rect.SetParent(frostCanvas, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        graphic.material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        graphic.maskable = false;
        graphic.raycastTarget = false;
        graphic.gameObject.SetActive(false);
        return graphic;
    }

    void UpdateBattleFrost()
    {
        if (!frostCanvas) return;
        var camera = controller.ViewCamera;
        if (!camera || !camera.isActiveAndEnabled || !CameraCaptureBridge.enabled)
        {
            HideBattleFrost();
            return;
        }
        if (frostCamera != camera)
        {
            if (frostCamera) CameraCaptureBridge.RemoveCaptureAction(frostCamera, CaptureFrostScene);
            CameraCaptureBridge.AddCaptureAction(camera, CaptureFrostScene);
            frostCamera = camera;
        }
        EnsureFrostTexture(camera);
        if (!frostScene.IsCreated())
        {
            HideBattleFrost();
            return;
        }

        if (initiativeQueue.gameObject.activeInHierarchy)
        {
            var initiative = BoundsInFrostCanvas(initiativeQueue);
            if (timeline.gameObject.activeInHierarchy) initiative = Enclose(initiative, FrostTextBounds(timeline));
            ShowFrostSurface(frostInitiative, initiative, camera, 9f);
        }
        else frostInitiative.gameObject.SetActive(false);

        ShowFrostSurface(frostLocation, FrostTextBounds(place), camera, 9f);
        var controls = FrostTextBounds(phase);
        if (journalButton.gameObject.activeInHierarchy)
            controls = Enclose(controls, BoundsInFrostCanvas((RectTransform)journalButton.transform));
        if (practice.gameObject.activeInHierarchy)
            controls = Enclose(controls, BoundsInFrostCanvas((RectTransform)practice.transform));
        ShowFrostSurface(frostControls, controls, camera, 8f);

        ShowFrostSurface(frostDock, BoundsInFrostCanvas(bottomBar), camera, 9f);
        if (originalDock) originalDock.enabled = !frostDock.gameObject.activeSelf;
    }

    void EnsureFrostTexture(Camera camera)
    {
        int width = Mathf.Max(1, camera.pixelWidth / FrostDownsample);
        int height = Mathf.Max(1, camera.pixelHeight / FrostDownsample);
        if (frostScene && frostScene.width == width && frostScene.height == height) return;
        ReleaseFrostTexture();
        frostScene = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = "青岚战斗界面背景",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        frostScene.Create();
    }

    void CaptureFrostScene(RenderTargetIdentifier source, CommandBuffer commands)
    {
        // URP camera capture runs after the world has rendered and before screen-space overlay UI.
        if (frostScene && frostScene.IsCreated()) commands.Blit(source, frostScene);
    }

    Rect BoundsInFrostCanvas(RectTransform rect)
    {
        Vector2 min = frostCanvas.InverseTransformPoint(rect.TransformPoint(rect.rect.min));
        Vector2 max = frostCanvas.InverseTransformPoint(rect.TransformPoint(rect.rect.max));
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    Rect FrostTextBounds(Text label)
    {
        var rect = BoundsInFrostCanvas(label.rectTransform);
        float width = Mathf.Clamp(label.preferredWidth, 1f, rect.width);
        float height = Mathf.Clamp(label.preferredHeight, 1f, rect.height);
        int align = (int)label.alignment;
        float left = rect.xMin + (rect.width - width) * ((align % 3) * .5f);
        float bottom = rect.yMin + (rect.height - height) * (1f - align / 3 * .5f);
        return new Rect(left, bottom, width, height);
    }

    static Rect Enclose(Rect first, Rect second) => Rect.MinMaxRect(
        Mathf.Min(first.xMin, second.xMin), Mathf.Min(first.yMin, second.yMin),
        Mathf.Max(first.xMax, second.xMax), Mathf.Max(first.yMax, second.yMax));

    void ShowFrostSurface(RawImage graphic, Rect content, Camera camera, float padding)
    {
        var bounds = frostCanvas.rect;
        var rect = Rect.MinMaxRect(
            Mathf.Max(bounds.xMin, content.xMin - padding), Mathf.Max(bounds.yMin, content.yMin - padding),
            Mathf.Min(bounds.xMax, content.xMax + padding), Mathf.Min(bounds.yMax, content.yMax + padding));
        if (rect.width <= 0 || rect.height <= 0)
        {
            graphic.gameObject.SetActive(false);
            return;
        }

        graphic.rectTransform.sizeDelta = rect.size;
        graphic.rectTransform.anchoredPosition = rect.center - bounds.center;
        graphic.texture = frostScene;

        var viewport = camera.pixelRect;
        Vector2 min = RectTransformUtility.WorldToScreenPoint(null, frostCanvas.TransformPoint(rect.min));
        Vector2 max = RectTransformUtility.WorldToScreenPoint(null, frostCanvas.TransformPoint(rect.max));
        graphic.material.SetVector(FrostViewport, new Vector4(
            (min.x - viewport.xMin) / viewport.width, (min.y - viewport.yMin) / viewport.height,
            (max.x - min.x) / viewport.width, (max.y - min.y) / viewport.height));
        graphic.material.SetVector(FrostShape, new Vector4(rect.width, rect.height, FrostFeather, 0f));
        graphic.gameObject.SetActive(true);
    }

    void HideBattleFrost()
    {
        if (frostCamera) CameraCaptureBridge.RemoveCaptureAction(frostCamera, CaptureFrostScene);
        frostCamera = null;
        if (frostInitiative) frostInitiative.gameObject.SetActive(false);
        if (frostLocation) frostLocation.gameObject.SetActive(false);
        if (frostControls) frostControls.gameObject.SetActive(false);
        if (frostDock) frostDock.gameObject.SetActive(false);
        if (originalDock) originalDock.enabled = true;
        ReleaseFrostTexture();
    }

    void ReleaseFrostTexture()
    {
        if (!frostScene) return;
        if (frostInitiative) frostInitiative.texture = null;
        if (frostLocation) frostLocation.texture = null;
        if (frostControls) frostControls.texture = null;
        if (frostDock) frostDock.texture = null;
        frostScene.Release();
        Destroy(frostScene);
        frostScene = null;
    }

    void OnDisable() => HideBattleFrost();

    void OnDestroy()
    {
        if (frostInitiative) Destroy(frostInitiative.material);
        if (frostLocation) Destroy(frostLocation.material);
        if (frostControls) Destroy(frostControls.material);
        if (frostDock) Destroy(frostDock.material);
    }
}
