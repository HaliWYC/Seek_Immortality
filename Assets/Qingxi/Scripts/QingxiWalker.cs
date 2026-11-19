using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D))]
public sealed class QingxiWalker : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float walkSpeed = 4.4f;
    [SerializeField, Min(0f)] float cameraFollowTime = 0.16f;
    [SerializeField] Camera followCamera;

    Rigidbody2D body;
    SpriteRenderer character;
    Vector2 input;
    Vector2 spawn;
    Vector3 cameraVelocity;
    float cameraDepth;
    bool originalFlip;
    bool explorationPaused;
    int inputBlockedOnFrame = -1;

    public Camera FollowCamera => followCamera;
    public bool CanUseExplorationInput => !DemoRun.Modal && !explorationPaused && Time.frameCount != inputBlockedOnFrame;
    public int CurrentAreaKind { get; private set; }
    public string CurrentPlace { get; private set; } = "青溪镇";
    public bool TrailDiscovered { get; private set; }
    public event Action StateChanged;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        character = GetComponentInChildren<SpriteRenderer>();
        if (character != null) originalFlip = character.flipX;
        if (followCamera == null) followCamera = Camera.main;
        spawn = body.position;
        if (followCamera != null)
        {
            cameraDepth = followCamera.transform.position.z;
            followCamera.transform.position = CameraTarget();
        }
        UpdateSorting();
    }

    void Update()
    {
        if (!CanUseExplorationInput) { input = Vector2.zero; return; }
        var keys = Keyboard.current;
        if (keys == null) { input = Vector2.zero; return; }
        if (keys.rKey.wasPressedThisFrame)
        {
            input = Vector2.zero;
            if (!DemoRun.Active) RestartWalk();
            else
            {
                string notice = DemoRun.RestInQingxi();
                if (notice.Length > 0) FindAnyObjectByType<SparHud>()?.Explain(notice);
            }
            return;
        }

        float x = (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0)
                - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0);
        float y = (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0)
                - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0);
        input = Vector2.ClampMagnitude(new Vector2(x, y), 1);
        if (character != null && x != 0) character.flipX = originalFlip ^ (x < 0);
    }

    void FixedUpdate()
    {
        if (CanUseExplorationInput && input != Vector2.zero)
            body.MovePosition(body.position + input * (walkSpeed * Time.fixedDeltaTime));
    }

    void LateUpdate()
    {
        if (explorationPaused) return;
        UpdateSorting();
        if (followCamera == null) return;
        var target = CameraTarget();
        followCamera.transform.position = Vector3.SmoothDamp(followCamera.transform.position, target,
            ref cameraVelocity, cameraFollowTime);
    }

    Vector3 CameraTarget()
    {
        var feet = transform.position;
        return new Vector3(feet.x, feet.y + 0.65f, cameraDepth);
    }

    void UpdateSorting()
    {
        if (character != null) character.sortingOrder = 1000 - Mathf.RoundToInt(transform.position.y * 10);
    }

    public void SetExplorationPaused(bool paused)
    {
        explorationPaused = paused;
        inputBlockedOnFrame = Time.frameCount;
        input = Vector2.zero;
        body.linearVelocity = Vector2.zero;
        cameraVelocity = Vector3.zero;
    }

    public void Reach(QingxiLandmark landmark)
    {
        if (explorationPaused) return;
        bool discovered = TrailDiscovered || landmark.kind == QingxiArea.Trail;
        if (CurrentAreaKind == (int)landmark.kind && CurrentPlace == landmark.placeName &&
            TrailDiscovered == discovered) return;
        CurrentAreaKind = (int)landmark.kind;
        CurrentPlace = landmark.placeName;
        TrailDiscovered = discovered;
        StateChanged?.Invoke();
    }

    public void RestartWalk()
    {
        RestorePosition(spawn, DemoRun.Active && TrailDiscovered, (int)QingxiArea.Town, "青溪镇");
    }

    public void RestorePosition(Vector2 feet, bool knownTrail, int area, string place)
    {
        input = Vector2.zero;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0;
        body.position = feet;
        transform.position = new Vector3(feet.x, feet.y, transform.position.z);
        if (character != null) character.flipX = originalFlip;
        cameraVelocity = Vector3.zero;
        if (followCamera != null) followCamera.transform.position = CameraTarget();
        CurrentAreaKind = area;
        CurrentPlace = string.IsNullOrEmpty(place) ? "青溪镇" : place;
        TrailDiscovered = knownTrail;
        UpdateSorting();
        StateChanged?.Invoke();
    }

    void OnApplicationFocus(bool focus)
    {
        if (!focus) input = Vector2.zero;
    }
}
