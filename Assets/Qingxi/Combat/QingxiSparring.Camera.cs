using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class QingxiSparring
{
    const float EntrySeconds = .22f;
    Bounds openingBattleBounds, battleMapBounds;
    Vector3 cameraFrom, battleCameraVelocity;
    Vector2 cameraFocus, dragPointer;
    Vector2Int? cameraCursor;
    float cameraFromSize;
    double openedAt;
    bool manualCamera, draggingCamera;

    void ResetBattleFocus()
    {
        cameraFrom = view.transform.position;
        cameraFromSize = view.orthographicSize;
        cameraFocus = (Vector2)openingBattleBounds.center + Vector2.up * .15f;
        battleCameraVelocity = Vector3.zero;
        draggingCamera = false;
        openedAt = Time.unscaledTimeAsDouble;
        FollowBattleActor();
    }

    void FollowBattleActor()
    {
        manualCamera = false;
        cameraCursor = null;
    }

    void FocusBattleCursor()
    {
        manualCamera = false;
        cameraCursor = cursor;
    }

    Vector2 BattleCameraActor()
    {
        var actor = PlaybackActor ?? Match.ActiveUnit;
        for (int i = 0; i < Match.Units.Count; i++)
            if (Match.Units[i] == actor) return board.FeetPosition(i);
        return openingBattleBounds.center;
    }

    bool ReadBattleCameraInput(Keyboard keys)
    {
        if (JournalOpen || PracticeMenuOpen || confirmingYield)
        {
            draggingCamera = false;
            return false;
        }
        if (keys != null && keys.homeKey.wasPressedThisFrame)
        {
            FollowBattleActor();
            cameraFocus = BattleCameraActor() + Vector2.up * .45f;
        }

        var mouse = Mouse.current;
        if (mouse == null || !mouse.middleButton.isPressed)
        {
            draggingCamera = false;
            return false;
        }

        Vector2 pointer = mouse.position.ReadValue();
        if (mouse.middleButton.wasPressedThisFrame)
        {
            draggingCamera = !hud.CoversBoard(pointer);
            dragPointer = pointer;
            if (draggingCamera) cameraFocus = view.transform.position;
        }
        if (!draggingCamera) return false;
        manualCamera = true;
        cameraCursor = null;
        float worldPerPixel = 2 * view.orthographicSize / Mathf.Max(1, view.pixelHeight);
        cameraFocus -= (pointer - dragPointer) * worldPerPixel;
        dragPointer = pointer;
        return true;
    }

    void FrameBoard()
    {
        if (JournalOpen || PracticeMenuOpen || confirmingYield) return;
        float aspect = Mathf.Max(.1f, view.aspect);
        float vertical = Mathf.Max(4.85f, (openingBattleBounds.extents.y + 1.28f) / .74f);
        float halfHeight = Mathf.Max(vertical, (openingBattleBounds.extents.x + 1.1f) / aspect);
        float halfWidth = halfHeight * aspect;
        float blend = Mathf.SmoothStep(0, 1, (float)(Time.unscaledTimeAsDouble - openedAt) / EntrySeconds);

        // Shift only when the selected point heads out of the central view.
        if (blend >= 1 && !manualCamera)
        {
            Vector2 target = cameraCursor.HasValue ? (Vector2)board.CellWorld(cameraCursor.Value) : BattleCameraActor();
            target.y += .45f;
            var inset = new Vector2(halfWidth * .56f, halfHeight * .43f);
            cameraFocus = Vector2.Max(target - inset, Vector2.Min(cameraFocus, target + inset));
        }

        cameraFocus = new Vector2(
            ClampCameraAxis(cameraFocus.x, battleMapBounds.min.x, battleMapBounds.max.x, halfWidth),
            ClampCameraAxis(cameraFocus.y, battleMapBounds.min.y, battleMapBounds.max.y, halfHeight));
        var destination = new Vector3(cameraFocus.x, cameraFocus.y, cameraFrom.z);
        view.orthographicSize = Mathf.Lerp(cameraFromSize, halfHeight, blend);
        if (draggingCamera) view.transform.position = destination;
        else if (blend < 1) view.transform.position = Vector3.Lerp(cameraFrom, destination, blend);
        else view.transform.position = Vector3.SmoothDamp(view.transform.position, destination, ref battleCameraVelocity,
            .14f, Mathf.Infinity, Mathf.Min(.1f, Time.unscaledDeltaTime));
    }

    static float ClampCameraAxis(float target, float min, float max, float halfSpan) =>
        max - min > halfSpan * 2f ? Mathf.Clamp(target, min + halfSpan, max - halfSpan) : (min + max) * .5f;
}
