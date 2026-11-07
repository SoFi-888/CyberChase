using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CameraFollow : MonoBehaviour
{
    [Header("Target (leave empty to auto-find the player)")]
    public Transform target;

    [Header("Starting View")]
    [Tooltip("Direction the camera looks at the start, in degrees. 0 = looks along +Z (camera sits on the -Z side of the player). 90 = looks along +X. 180 and 270 = the other two sides.")]
    public float viewYaw = 0f;
    [Tooltip("How far the camera starts from the player. The scroll wheel changes this while playing.")]
    public float distance = 10f;
    [Tooltip("Starting downward angle. 0 = perfectly level side view.")]
    public float pitch = 8f;
    [Tooltip("Nudge the view up (+) or down (-) from the exact center of the player's body.")]
    public float focusOffset = 0f;

    [Header("Right-Click Panning")]
    [Tooltip("How fast the camera turns while the right mouse button is held.")]
    public float panSensitivity = 0.15f;
    [Tooltip("Lowest and highest angle the camera can pan to (degrees).")]
    public float minPitch = -5f;
    public float maxPitch = 80f;
    [Tooltip("Hides and locks the mouse cursor while you pan, so it can't leave the window.")]
    public bool hideCursorWhilePanning = true;
    [Tooltip("When on, the camera swings back to the starting side view after you let go of the right mouse button.")]
    public bool returnToStartViewOnRelease = false;
    public float returnSpeed = 4f;

    [Header("Scroll Zoom")]
    [Tooltip("How much each scroll notch zooms.")]
    public float zoomStep = 1.5f;
    public float minZoom = 3f;
    public float maxZoom = 20f;
    public float zoomSmoothTime = 0.1f;
    [Tooltip("Prints the raw scroll value in the Console, to check that scrolling is detected.")]
    public bool logScroll = false;

    [Header("Zoom In At Walls")]
    [Tooltip("If a wall is between the camera and the player, move the camera closer so the player stays visible.")]
    public bool zoomInAtWalls = true;
    public float collisionRadius = 0.3f;
    public float minDistance = 2f;

    private CharacterController targetController;
    private float currentYaw;
    private float currentPitch;
    private float targetDistance;     // what the scroll wheel asks for
    private float currentDistance;    // what we actually use (smoothed, and pulled in by walls)
    private float zoomVelocity;
    private bool cursorHidden;

    void Start()
    {
        if (target == null)
        {
            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player != null) target = player.transform;
            else Debug.LogWarning("CameraFollow: no player found to follow.");
        }

        if (target != null) targetController = target.GetComponent<CharacterController>();

        currentYaw = viewYaw;
        currentPitch = pitch;
        targetDistance = Mathf.Clamp(distance, minZoom, maxZoom);
        currentDistance = targetDistance;

        transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        if (target != null) transform.position = GetFocus() + BackDirection() * currentDistance;
    }

    void OnDisable()
    {
        SetCursorHidden(false);
    }

    void LateUpdate()
    {
        if (target == null) return;

        // ----- Right mouse button held: pan (orbit) around the player -----
        bool panning = RightMouseHeld();
        if (panning)
        {
            Vector2 delta = ReadMouseDelta();
            currentYaw += delta.x * panSensitivity;
            currentPitch = Mathf.Clamp(currentPitch - delta.y * panSensitivity, minPitch, maxPitch);
        }
        else if (returnToStartViewOnRelease)
        {
            float t = 1f - Mathf.Exp(-returnSpeed * Time.deltaTime);
            currentYaw = Mathf.LerpAngle(currentYaw, viewYaw, t);
            currentPitch = Mathf.Lerp(currentPitch, pitch, t);
        }
        SetCursorHidden(panning && hideCursorWhilePanning);

        // ----- Scroll wheel: zoom in / out -----
        float scroll = ReadScroll();
        if (Mathf.Abs(scroll) > 0.001f)
        {
            // If a wall is holding the camera closer than the requested zoom, start from where the
            // camera really is, so scrolling in responds right away.
            if (scroll > 0f && currentDistance < targetDistance)
                targetDistance = currentDistance;

            targetDistance = Mathf.Clamp(targetDistance - scroll * zoomStep, minZoom, maxZoom);
        }

        transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);

        Vector3 focus = GetFocus();
        Vector3 back = BackDirection();

        // ----- Zoom In At Walls -----
        float desiredDistance = targetDistance;
        if (zoomInAtWalls)
        {
            RaycastHit hit;
            if (Physics.SphereCast(focus, collisionRadius, back, out hit, targetDistance,
                                   ~0, QueryTriggerInteraction.Ignore))
            {
                desiredDistance = Mathf.Min(targetDistance, Mathf.Max(minDistance, hit.distance));
            }
        }

        // Only the zoom is smoothed. The camera follows the player exactly, so they stay centered.
        currentDistance = Mathf.SmoothDamp(currentDistance, desiredDistance, ref zoomVelocity, zoomSmoothTime);
        transform.position = focus + back * currentDistance;
    }

    // The center of the player's body (not their feet)
    Vector3 GetFocus()
    {
        Vector3 center = targetController != null
            ? targetController.bounds.center
            : target.position + Vector3.up * 1.2f;
        return center + Vector3.up * focusOffset;
    }

    // Direction from the player toward the camera
    Vector3 BackDirection()
    {
        return Quaternion.Euler(currentPitch, currentYaw, 0f) * Vector3.back;
    }

    void SetCursorHidden(bool hidden)
    {
        if (hidden == cursorHidden) return;
        cursorHidden = hidden;
        Cursor.lockState = hidden ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !hidden;
    }

    // ---------------- Input (works with the new Input System or the old Input Manager) ----------------

    bool RightMouseHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
        return Input.GetMouseButton(1);
#endif
    }

    Vector2 ReadMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
        return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
    }

    // +1 per notch scrolled up, -1 per notch scrolled down
    float ReadScroll()
    {
        float raw;
#if ENABLE_INPUT_SYSTEM
        raw = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
        raw = Input.mouseScrollDelta.y;
#endif
        if (logScroll && Mathf.Abs(raw) > 0.0001f)
            Debug.Log("CameraFollow: raw scroll value = " + raw);

        // Some systems report about 120 per notch, others report 1. Convert both to 1 per notch.
        float notches = Mathf.Abs(raw) > 5f ? raw / 120f : raw;
        return Mathf.Clamp(notches, -3f, 3f);
    }
}