using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyAI : MonoBehaviour
{
    public static readonly List<EnemyAI> All = new List<EnemyAI>();

    [Header("Roaming")]
    [Tooltip("How far from its starting spot the enemy wanders.")]
    public float roamRadius = 8f;
    public float roamSpeed = 2f;
    public float waitAtPoint = 1.5f;
    public float lookAroundSpeed = 60f;

    [Header("Vision (front only)")]
    public float detectionRadius = 8f;     // how far it can see
    [Tooltip("Total width of the vision cone in degrees. 90 = 45 degrees to each side of straight ahead.")]
    [Range(10f, 180f)]
    public float viewAngle = 90f;
    [Tooltip("While chasing, the player must get farther than this to be lost. Keep it larger than Detection Radius.")]
    public float loseRadius = 12f;

    [Header("Chase")]
    [Tooltip("Keep this lower than the player's Move Speed so the player can escape.")]
    public float chaseSpeed = 4f;
    public float chaseHeight = 1f;
    public float catchDistance = 1.2f;
    public float catchHeight = 3f;
    public float stunAfterCatch = 2f;

    [Header("Movement Feel")]
    public float turnSpeed = 360f;
    public float heightSmoothing = 3f;
    public float bobAmount = 0.15f;
    public float bobSpeed = 2f;
    [Tooltip("If the enemy's face doesn't point where it moves, try 180, 90 or -90. Check the cyan vision cone in the Scene view: it should point out from the enemy's face.")]
    public float modelYawOffset = 0f;
    public float deathShrinkTime = 0.4f;

    public bool IsDead { get; private set; }

    public Vector3 FacingDirection
    {
        get
        {
            float r = heading * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }
    }

    private enum State { Roam, Chase }
    private State state = State.Roam;

    private SpawnAtCheckpoint player;
    private Vector3 startPos;
    private float startY;
    private Vector3 roamTarget;
    private bool waiting;
    private float waitTimer;
    private float lookDirection = 1f;
    private float heading;
    private float ignorePlayerUntil;

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void OnValidate()
    {
        if (loseRadius < detectionRadius) loseRadius = detectionRadius;
    }

    void Start()
    {
        player = FindFirstObjectByType<SpawnAtCheckpoint>();
        if (player == null)
            Debug.LogWarning("EnemyAI: no player found. Add SpawnAtCheckpoint to walkcycle.");

        startPos = transform.position;
        startY = startPos.y;
        heading = transform.eulerAngles.y - modelYawOffset;
        PickNewRoamTarget();
    }

    void Update()
    {
        if (IsDead || player == null) return;

        Vector3 playerPos = player.transform.position;
        float dist = HorizontalDistance(transform.position, playerPos);
        bool ignoring = Time.time < ignorePlayerUntil;

        if (state == State.Roam)
        {
            if (!ignoring && CanSeePlayer(playerPos, dist))
                state = State.Chase;
            else
                Roam();
        }

        if (state == State.Chase)
        {
            if (ignoring || dist > loseRadius)
            {
                state = State.Roam;
                waiting = false;
                PickNewRoamTarget();
            }
            else
            {
                Chase(playerPos);
            }
        }

        transform.rotation = Quaternion.Euler(0f, heading + modelYawOffset, 0f);
    }

    bool CanSeePlayer(Vector3 playerPos, float dist)
    {
        if (dist > detectionRadius) return false;
        if (dist < 0.01f) return true;

        Vector3 toPlayer = playerPos - transform.position;
        toPlayer.y = 0f;
        toPlayer.Normalize();

        float dot = Vector3.Dot(FacingDirection, toPlayer);
        float minDot = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);

        return dot >= minDot;
    }

    void Roam()
    {
        ApplyHeight(startY);

        if (waiting)
        {
            heading += lookDirection * lookAroundSpeed * Time.deltaTime;
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                waiting = false;
                PickNewRoamTarget();
            }
            return;
        }

        MoveHorizontal(roamTarget, roamSpeed);

        if (HorizontalDistance(transform.position, roamTarget) < 0.2f)
        {
            waiting = true;
            waitTimer = waitAtPoint;
            lookDirection = Random.value < 0.5f ? -1f : 1f;
        }
    }

    void PickNewRoamTarget()
    {
        Vector2 r = Random.insideUnitCircle * roamRadius;
        roamTarget = startPos + new Vector3(r.x, 0f, r.y);
    }

    void Chase(Vector3 playerPos)
    {
        MoveHorizontal(playerPos, chaseSpeed);
        ApplyHeight(playerPos.y + chaseHeight);

        float dist = HorizontalDistance(transform.position, playerPos);
        if (dist < catchDistance && Mathf.Abs(transform.position.y - playerPos.y) < catchHeight)
        {
            player.Respawn();
            ignorePlayerUntil = Time.time + stunAfterCatch;
            state = State.Roam;
            waiting = false;
            PickNewRoamTarget();
        }
    }

    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        StartCoroutine(DieRoutine());
    }

    IEnumerator DieRoutine()
    {
        Vector3 startScale = transform.localScale;
        float t = 0f;
        while (t < deathShrinkTime)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t / Mathf.Max(deathShrinkTime, 0.01f));
            yield return null;
        }
        Destroy(gameObject);
    }

    void MoveHorizontal(Vector3 target, float speed)
    {
        Vector3 pos = transform.position;
        Vector3 dir = target - pos;
        dir.y = 0f;
        float d = dir.magnitude;

        if (d > 0.001f)
        {
            pos += dir.normalized * Mathf.Min(speed * Time.deltaTime, d);

            // Turn to face where it is going
            float desired = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            heading = Mathf.MoveTowardsAngle(heading, desired, turnSpeed * Time.deltaTime);
        }
        transform.position = pos;
    }

    void ApplyHeight(float desiredY)
    {
        desiredY += Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, desiredY, heightSmoothing * Time.deltaTime);
        transform.position = pos;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void OnDrawGizmosSelected()
    {
        float h = Application.isPlaying ? heading : transform.eulerAngles.y - modelYawOffset;
        float half = viewAngle * 0.5f;
        Vector3 origin = transform.position;

        Gizmos.color = Color.cyan;
        Vector3 left = Quaternion.Euler(0f, h - half, 0f) * Vector3.forward * detectionRadius;
        Vector3 right = Quaternion.Euler(0f, h + half, 0f) * Vector3.forward * detectionRadius;
        Vector3 center = Quaternion.Euler(0f, h, 0f) * Vector3.forward * detectionRadius;
        Gizmos.DrawLine(origin, origin + left);
        Gizmos.DrawLine(origin, origin + right);
        Gizmos.DrawLine(origin, origin + center);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin, Mathf.Max(loseRadius, detectionRadius));

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(Application.isPlaying ? startPos : origin, roamRadius);
    }
}