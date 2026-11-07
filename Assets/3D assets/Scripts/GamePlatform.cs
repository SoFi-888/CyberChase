using UnityEngine;

public class GamePlatform : MonoBehaviour
{
    public enum PlatformType { Static, LeftRight, UpDown, ForwardBack }

    [Header("Type")]
    public PlatformType type = PlatformType.Static;

    [Header("Movement (ignored for Static)")]
    [Tooltip("How far the platform travels from where you placed it.")]
    public float moveDistance = 5f;
    [Tooltip("Speed in units per second.")]
    public float speed = 2f;
    [Tooltip("Tick to travel the opposite way first (left instead of right, down instead of up).")]
    public bool reverseDirection = false;
    [Tooltip("Slows down gently at each end instead of turning around suddenly.")]
    public bool smoothEnds = true;
    [Tooltip("Delay in seconds. Give each platform a different value so they don't all move in sync.")]
    public float timeOffset = 0f;

    [Header("Carrying the player")]
    [Tooltip("How close the player's feet must be to the top of the platform to count as standing on it.")]
    public float standingTolerance = 0.35f;

    private Vector3 startPos;
    private SpawnAtCheckpoint player;
    private CharacterController playerController;
    private Collider col;

    void Start()
    {
        startPos = transform.position;

        player = FindFirstObjectByType<SpawnAtCheckpoint>();
        if (player != null) playerController = player.GetComponent<CharacterController>();

        col = GetComponent<Collider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
            Debug.LogWarning("GamePlatform: '" + name + "' had no collider, so one was added.");
        }
    }

    void Update()
    {
        if (type == PlatformType.Static) return;

        Vector3 before = transform.position;

        // Check whether the player is standing on top BEFORE we move
        bool carrying = IsPlayerOnTop();

        transform.position = PositionAt(Time.time);

        // Move the player by the same amount so they ride along
        if (carrying && playerController != null && playerController.enabled)
            playerController.Move(transform.position - before);
    }

    Vector3 PositionAt(float time)
    {
        float distance = Mathf.Max(moveDistance, 0.01f);

        // 0 -> 1 -> 0 -> 1 ... at a steady speed
        float t01 = Mathf.PingPong((time + timeOffset) * speed / distance, 1f);
        float eased = smoothEnds ? Mathf.SmoothStep(0f, 1f, t01) : t01;

        return startPos + Direction() * (distance * eased);
    }

    Vector3 Direction()
    {
        Vector3 dir = Vector3.zero;
        switch (type)
        {
            case PlatformType.LeftRight: dir = Vector3.right; break;
            case PlatformType.UpDown: dir = Vector3.up; break;
            case PlatformType.ForwardBack: dir = Vector3.forward; break;
        }
        return reverseDirection ? -dir : dir;
    }

    bool IsPlayerOnTop()
    {
        if (player == null || playerController == null || col == null) return false;
        if (!playerController.isGrounded) return false;

        Bounds p = player.GetBounds();
        Bounds b = col.bounds;

        // Feet at the top surface of the platform...
        float feet = p.min.y;
        bool atTop = feet >= b.max.y - standingTolerance && feet <= b.max.y + standingTolerance;

        // ...and the player is over the platform, not beside it
        bool overlapsX = p.max.x > b.min.x && p.min.x < b.max.x;
        bool overlapsZ = p.max.z > b.min.z && p.min.z < b.max.z;

        return atTop && overlapsX && overlapsZ;
    }

    void OnDrawGizmosSelected()
    {
        if (type == PlatformType.Static) return;

        Renderer[] rends = GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        Vector3 basePos = Application.isPlaying ? startPos : transform.position;
        Vector3 lowCenter = b.center - transform.position + basePos;
        Vector3 highCenter = lowCenter + Direction() * Mathf.Max(moveDistance, 0.01f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(lowCenter, b.size);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(highCenter, b.size);
        Gizmos.DrawLine(lowCenter, highCenter);
    }
}