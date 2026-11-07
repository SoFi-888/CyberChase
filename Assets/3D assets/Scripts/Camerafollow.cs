using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float viewYaw = 0f;
    public float distance = 10f;
    public float pitch = 8f;
    public float focusOffset = 0f;

    public float panSensitivity = 0.15f;
    public float minPitch = -5f;
    public float maxPitch = 80f;
    public bool hideCursorWhilePanning = true;
    public bool returnToStartViewOnRelease = false;
    public float returnSpeed = 4f;

    public float zoomStep = 1.5f;
    public float minZoom = 3f;
    public float maxZoom = 20f;
    public float zoomSmoothTime = 0.1f;
    public bool logScroll = false;

    [Tooltip("If a wall is between the camera and the player, move the camera closer so the player stays visible.")]
    public bool zoomInAtWalls = true;
    public float collisionRadius = 0.3f;
    public float minDistance = 2f;

    private CharacterController targetController;
    private float currentYaw;
    private float currentPitch;
    private float targetDistance;
    private float currentDistance;
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

        float scroll = ReadScroll();
        if (Mathf.Abs(scroll) > 0.001f)
        {

            if (scroll > 0f && currentDistance < targetDistance)
                targetDistance = currentDistance;

            targetDistance = Mathf.Clamp(targetDistance - scroll * zoomStep, minZoom, maxZoom);
        }

        transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);

        Vector3 focus = GetFocus();
        Vector3 back = BackDirection();

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

        currentDistance = Mathf.SmoothDamp(currentDistance, desiredDistance, ref zoomVelocity, zoomSmoothTime);
        transform.position = focus + back * currentDistance;
    }

    Vector3 GetFocus()
    {
        Vector3 center = targetController != null
            ? targetController.bounds.center
            : target.position + Vector3.up * 1.2f;
        return center + Vector3.up * focusOffset;
    }

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

        float notches = Mathf.Abs(raw) > 5f ? raw / 120f : raw;
        return Mathf.Clamp(notches, -3f, 3f);
    }
}