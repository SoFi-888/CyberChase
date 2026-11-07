using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LaserHazard : MonoBehaviour
{
    public enum LaserType { Static, LeftRight, FrontBack, UpDown, Rotate }

    [Header("Type")]
    public LaserType type = LaserType.Static;

    [Header("Moving (LeftRight, FrontBack, UpDown)")]
    [Tooltip("How far the laser travels from where you placed it.")]
    public float moveDistance = 5f;
    [Tooltip("Speed in units per second.")]
    public float moveSpeed = 2f;
    [Tooltip("Tick to travel the opposite way first (left instead of right, back instead of front, down instead of up).")]
    public bool reverseDirection = false;
    [Tooltip("Slows down gently at each end instead of turning around suddenly.")]
    public bool smoothEnds = true;
    [Tooltip("Delay in seconds. Give each laser a different value so they don't all move in sync.")]
    public float timeOffset = 0f;
    [Tooltip("Off: the directions follow the world axes. On: they follow the way the laser itself is turned.")]
    public bool moveInLocalAxes = false;

    [Header("Rotating (Rotate)")]
    [Tooltip("The axis to spin around. (0,1,0) spins flat like a propeller. (0,0,1) spins upright like a windmill.")]
    public Vector3 rotationAxis = Vector3.up;
    [Tooltip("Degrees per second. Use a negative number to spin the other way.")]
    public float rotationSpeed = 90f;
    [Tooltip("Optional. Leave empty to spin around the laser's own center. Assign an empty object to swing around that point instead (like a clock hand).")]
    public Transform pivot;
    [Tooltip("On: the axis above is a world direction. Off: it is relative to the laser's own direction.")]
    public bool useWorldAxis = true;

    [Header("Danger")]
    [Tooltip("Extra reach around the laser so brushing against it counts as touching.")]
    public float touchPadding = 0.05f;

    [Header("Look")]
    [Tooltip("Tints the laser with the color below. Leave off to keep your own material.")]
    public bool applyColor = false;
    public Color laserColor = new Color(1f, 0.1f, 0.1f);

    private SpawnAtCheckpoint player;
    private MeshFilter[] meshFilters;
    private Vector3 startPos;
    private Quaternion startRot;

    void Start()
    {
        player = FindFirstObjectByType<SpawnAtCheckpoint>();
        meshFilters = GetComponentsInChildren<MeshFilter>();
        startPos = transform.position;
        startRot = transform.rotation;

        if (applyColor)
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                r.material.color = laserColor;
    }

    void Update()
    {
        switch (type)
        {
            case LaserType.Static:
                break;
            case LaserType.Rotate:
                Spin();
                break;
            default:
                Move();
                break;
        }

        CheckPlayerTouch();
    }

    // ---------------- Moving ----------------

    void Move()
    {
        float distance = Mathf.Max(moveDistance, 0.01f);

        // 0 -> 1 -> 0 -> 1 ... at a steady speed
        float t01 = Mathf.PingPong((Time.time + timeOffset) * moveSpeed / distance, 1f);
        float eased = smoothEnds ? Mathf.SmoothStep(0f, 1f, t01) : t01;

        transform.position = startPos + MoveDirection(startRot) * (distance * eased);
    }

    Vector3 MoveDirection(Quaternion rotation)
    {
        Vector3 axis = Vector3.zero;
        switch (type)
        {
            case LaserType.LeftRight: axis = Vector3.right; break;
            case LaserType.FrontBack: axis = Vector3.forward; break;
            case LaserType.UpDown: axis = Vector3.up; break;
        }
        if (moveInLocalAxes) axis = rotation * axis;
        return reverseDirection ? -axis : axis;
    }

    // ---------------- Rotating ----------------

    void Spin()
    {
        Vector3 axis = WorldRotationAxis();
        float step = rotationSpeed * Time.deltaTime;

        if (pivot != null)
            transform.RotateAround(pivot.position, axis, step);
        else
            transform.Rotate(axis, step, Space.World);
    }

    Vector3 WorldRotationAxis()
    {
        Vector3 axis = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.up;
        return useWorldAxis ? axis : transform.TransformDirection(axis);
    }

    // ---------------- Touching the player ----------------

    // Uses the real (rotated) shape of the laser, so a long spinning beam only hurts where the beam actually is
    void CheckPlayerTouch()
    {
        if (player == null) return;

        if (meshFilters == null || meshFilters.Length == 0)
        {
            Collider col = GetComponent<Collider>();
            if (col != null && col.bounds.Intersects(player.GetBounds()))
                player.Respawn();
            return;
        }

        foreach (MeshFilter mf in meshFilters)
        {
            if (mf == null || mf.sharedMesh == null) continue;

            Bounds local = mf.sharedMesh.bounds;
            Transform t = mf.transform;
            Vector3 s = t.lossyScale;

            Vector3 center = t.TransformPoint(local.center);
            Vector3 halfExtents =
                Vector3.Scale(local.extents, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)))
                + Vector3.one * touchPadding;

            Collider[] hits = Physics.OverlapBox(center, halfExtents, t.rotation, ~0, QueryTriggerInteraction.Collide);
            foreach (Collider c in hits)
            {
                if (c.transform.IsChildOf(player.transform))
                {
                    player.Respawn();
                    return;
                }
            }
        }
    }

    // ---------------- Scene view preview ----------------

    void OnDrawGizmosSelected()
    {
        if (type == LaserType.Static) return;

        Renderer[] rends = GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        if (type == LaserType.Rotate)
        {
            // Orange circle = the area the spinning laser sweeps, line = the spin axis
            Vector3 axis = WorldRotationAxis();
            Vector3 c = pivot != null ? pivot.position : b.center;
            float reach = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
            if (pivot != null) reach += Vector3.Distance(b.center, pivot.position);

            Gizmos.color = new Color(1f, 0.5f, 0.1f);
            Gizmos.DrawLine(c - axis, c + axis);
#if UNITY_EDITOR
            Handles.color = new Color(1f, 0.5f, 0.1f);
            Handles.DrawWireDisc(c, axis, reach);
#endif
            return;
        }

        // Moving: yellow = start, green = far end
        Vector3 basePos = Application.isPlaying ? startPos : transform.position;
        Quaternion baseRot = Application.isPlaying ? startRot : transform.rotation;
        Vector3 lowCenter = b.center - transform.position + basePos;
        Vector3 highCenter = lowCenter + MoveDirection(baseRot) * Mathf.Max(moveDistance, 0.01f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(lowCenter, b.size);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(highCenter, b.size);
        Gizmos.DrawLine(lowCenter, highCenter);
    }
}