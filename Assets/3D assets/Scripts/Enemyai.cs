using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class EnemyAI : MonoBehaviour
{
    public static readonly List<EnemyAI> All = new List<EnemyAI>();

    public enum ModelFront { ForwardZ, RightX, BackZ, LeftX }

    [Header("Model Direction (fixes the triangle facing the wrong way)")]
    public ModelFront modelFront = ModelFront.ForwardZ;
    public float fineTuneAngle = 0f;

    [Header("Roaming")]
    public float roamRadius = 8f;
    public float roamSpeed = 2f;
    public float waitAtPoint = 1.5f;
    public float lookAroundSpeed = 60f;

    [Header("Vision (the blue triangle)")]
    public float visionDistance = 8f;
    [Range(10f, 180f)]
    public float viewAngle = 90f;
    public float visionHeight = 3f;
    public float loseSightTime = 2f;
    public bool requireLineOfSight = true;

    [Header("Backstab Zone (the red wedge behind the enemy)")]
    [Range(10f, 180f)]
    public float backstabAngle = 120f;
    public float backstabRange = 4f;

    [Header("Chase")]
    [Tooltip("Keep this lower than the player's Move Speed so the player can escape.")]
    public float chaseSpeed = 4f;
    public float catchDistance = 1.2f;
    [Range(10f, 180f)]
    public float frontTouchAngle = 120f;
    public float catchHeight = 3f;
    public float stunAfterCatch = 2f;

    [Header("Walls (stops enemies walking through walls)")]
    [Tooltip("Turn off for enemies that are allowed to float through walls, like a ghost.")]
    public bool collideWithWalls = true;
    [Tooltip("Which layers count as walls. Everything by default. If enemies get stopped by things they shouldn't, put your walls on their own layer and pick it here.")]
    public LayerMask obstacleLayers = ~0;
    [Tooltip("On: the enemy's size is worked out from its collider. Off: use Body Radius and Body Height below.")]
    public bool autoSizeFromCollider = true;
    public float bodyRadius = 0.5f;
    public float bodyHeight = 2f;
    [Tooltip("Things lower than this (floor edges, small steps) don't block the enemy.")]
    public float stepHeight = 0.4f;

    [Header("Movement Feel")]
    public float turnSpeed = 360f;
    [Tooltip("Turn on for floating enemies like the ghost. Ground enemies (the robot) stay at their starting height.")]
    public bool flying = false;
    public float chaseHeight = 1f;
    public float heightSmoothing = 3f;
    public float bobAmount = 0.15f;
    public float bobSpeed = 2f;
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
    public float BackstabDotLimit
    {
        get { return -Mathf.Cos(backstabAngle * 0.5f * Mathf.Deg2Rad); }
    }

    public float DotToward(Vector3 worldPos)
    {
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.000001f) return 0f;
        dir.Normalize();
        return Vector3.Dot(FacingDirection, dir);
    }

    public bool IsBehind(Vector3 worldPos)
    {
        return DotToward(worldPos) <= BackstabDotLimit;
    }

    private enum State { Roam, Chase }
    private State state = State.Roam;

    private SpawnAtCheckpoint player;
    private Vector3 startPos;
    private float startY;
    private Vector3 roamTarget;
    private Vector3 lastSeenPos;
    private bool waiting;
    private float waitTimer;
    private float lookDirection = 1f;
    private float heading;
    private float lostSightTimer;
    private float ignorePlayerUntil;

    private float feetOffset;
    private bool lastMoveBlocked;
    private float blockedTimer;
    private float chaseBlockedTimer;
    private const float WallSkin = 0.05f;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    float FrontYaw
    {
        get
        {
            float a = 0f;
            switch (modelFront)
            {
                case ModelFront.RightX: a = 90f; break;
                case ModelFront.BackZ: a = 180f; break;
                case ModelFront.LeftX: a = -90f; break;
            }
            return a + fineTuneAngle;
        }
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Start()
    {
        player = FindFirstObjectByType<SpawnAtCheckpoint>();
        if (player == null)
            Debug.LogWarning("EnemyAI: no player found. Add SpawnAtCheckpoint to walkcycle.");

        startPos = transform.position;
        startY = startPos.y;
        heading = transform.eulerAngles.y + FrontYaw;

        Collider own = GetComponentInChildren<Collider>();
        if (autoSizeFromCollider && own != null)
        {
            Bounds b = own.bounds;
            bodyRadius = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z), 0.2f, 1.5f);
            bodyHeight = Mathf.Max(b.size.y, bodyRadius * 2f);
            feetOffset = b.min.y - transform.position.y;
        }

        PickNewRoamTarget();
    }

    void Update()
    {
        if (IsDead || player == null) return;

        Vector3 playerPos = player.transform.position;
        bool ignoring = Time.time < ignorePlayerUntil;
        bool sees = !ignoring && CanSeePlayer(playerPos);

        if (state == State.Roam)
        {
            if (sees)
            {
                state = State.Chase;
                lostSightTimer = 0f;
                chaseBlockedTimer = 0f;
                lastSeenPos = playerPos;
            }
            else
            {
                Roam();
            }
        }

        if (state == State.Chase)
        {
            if (ignoring)
            {
                StopChasing();
            }
            else
            {
                if (sees)
                {
                    lastSeenPos = playerPos;
                    lostSightTimer = 0f;
                }
                else
                {
                    lostSightTimer += Time.deltaTime;
                    if (lostSightTimer >= loseSightTime) StopChasing();
                }
            }

            if (state == State.Chase) Chase();
        }

        if (!ignoring && TouchingPlayer(playerPos))
        {
            player.Respawn();
            ignorePlayerUntil = Time.time + stunAfterCatch;
            StopChasing();
        }

        transform.rotation = Quaternion.Euler(0f, heading - FrontYaw, 0f);
    }

    bool CanSeePlayer(Vector3 playerPos)
    {
        Vector3 to = playerPos - transform.position;
        if (Mathf.Abs(to.y) > visionHeight) return false;

        to.y = 0f;
        float dist = to.magnitude;
        if (dist > visionDistance) return false;

        if (dist >= 0.01f)
        {
            float dot = Vector3.Dot(FacingDirection, to / dist);
            float minDot = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);
            if (dot < minDot) return false;
        }

        return !requireLineOfSight || HasLineOfSight();
    }

    bool HasLineOfSight()
    {
        Vector3 eye = transform.position + Vector3.up * (feetOffset + bodyHeight * 0.7f);
        Vector3 target = player.GetBounds().center;
        Vector3 dir = target - eye;
        float dist = dir.magnitude;
        if (dist < 0.01f) return true;

        int count = Physics.RaycastNonAlloc(eye, dir / dist, hitBuffer, dist, obstacleLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (IsObstacle(hitBuffer[i].collider)) return false;

        return true;
    }

    bool TouchingPlayer(Vector3 playerPos)
    {
        if (Mathf.Abs(transform.position.y - playerPos.y) > catchHeight) return false;

        Bounds b = player.GetBounds();
        Vector3 probe = new Vector3(transform.position.x, b.center.y, transform.position.z);
        Vector3 closest = b.ClosestPoint(probe);
        if (HorizontalDistance(transform.position, closest) >= catchDistance) return false;

        Vector3 aim = (closest - transform.position).sqrMagnitude > 0.0001f ? closest : playerPos;
        float minDot = Mathf.Cos(frontTouchAngle * 0.5f * Mathf.Deg2Rad);
        return DotToward(aim) >= minDot;
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

        if (lastMoveBlocked)
        {
            blockedTimer += Time.deltaTime;
            if (blockedTimer > 0.6f)
            {
                blockedTimer = 0f;
                PickNewRoamTarget();
            }
        }
        else
        {
            blockedTimer = 0f;
        }

        if (HorizontalDistance(transform.position, roamTarget) < 0.2f)
        {
            waiting = true;
            waitTimer = waitAtPoint;
            lookDirection = Random.value < 0.5f ? -1f : 1f;
        }
    }

    void PickNewRoamTarget()
    {
        for (int i = 0; i < 12; i++)
        {
            Vector2 r = Random.insideUnitCircle * roamRadius;
            Vector3 candidate = startPos + new Vector3(r.x, 0f, r.y);

            if (!collideWithWalls || PathIsClear(transform.position, candidate))
            {
                roamTarget = candidate;
                return;
            }
        }

        roamTarget = transform.position;
    }

    bool PathIsClear(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        d.y = 0f;
        float dist = d.magnitude;
        if (dist < 0.01f) return true;

        RaycastHit hit;
        return !FindBlockingHit(from, d / dist, dist + 0.1f, out hit);
    }

    void Chase()
    {
        MoveHorizontal(lastSeenPos, chaseSpeed);
        ApplyHeight(lastSeenPos.y + chaseHeight);

        if (lastMoveBlocked)
        {
            chaseBlockedTimer += Time.deltaTime;
            if (chaseBlockedTimer > 1.5f)
            {
                ignorePlayerUntil = Time.time + 1.5f;
                StopChasing();
            }
        }
        else
        {
            chaseBlockedTimer = 0f;
        }
    }

    void StopChasing()
    {
        state = State.Roam;
        waiting = false;
        chaseBlockedTimer = 0f;
        blockedTimer = 0f;
        PickNewRoamTarget();
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
        lastMoveBlocked = false;

        if (d > 0.001f)
        {
            float stepLength = Mathf.Min(speed * Time.deltaTime, d);
            Vector3 newPos = MoveWithWalls(pos, dir.normalized * stepLength);

            float moved = HorizontalDistance(pos, newPos);
            lastMoveBlocked = collideWithWalls && stepLength > 0.0001f && moved < stepLength * 0.5f;
            pos = newPos;

            float desired = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            heading = Mathf.MoveTowardsAngle(heading, desired, turnSpeed * Time.deltaTime);
        }
        transform.position = pos;
    }

    Vector3 MoveWithWalls(Vector3 pos, Vector3 move)
    {
        if (!collideWithWalls) return pos + move;

        for (int i = 0; i < 3; i++)
        {
            float dist = move.magnitude;
            if (dist < 0.0001f) break;

            Vector3 dir = move / dist;
            RaycastHit hit;
            if (FindBlockingHit(pos, dir, dist + WallSkin, out hit))
            {
                float travel = Mathf.Max(0f, hit.distance - WallSkin);
                pos += dir * travel;

                Vector3 remaining = dir * (dist - travel);
                move = Vector3.ProjectOnPlane(remaining, hit.normal);
                move.y = 0f;
            }
            else
            {
                pos += move;
                break;
            }
        }
        return pos;
    }

    bool FindBlockingHit(Vector3 pos, Vector3 dir, float distance, out RaycastHit best)
    {
        best = default(RaycastHit);

        Vector3 p1, p2;
        GetBodyCapsule(pos, out p1, out p2);

        int count = Physics.CapsuleCastNonAlloc(p1, p2, bodyRadius, dir, hitBuffer, distance,
                                                obstacleLayers, QueryTriggerInteraction.Ignore);
        bool found = false;
        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (!IsObstacle(hitBuffer[i].collider)) continue;
            if (hitBuffer[i].distance < nearest)
            {
                nearest = hitBuffer[i].distance;
                best = hitBuffer[i];
                found = true;
            }
        }
        return found;
    }
    void GetBodyCapsule(Vector3 pos, out Vector3 p1, out Vector3 p2)
    {
        float bottom = feetOffset + stepHeight + bodyRadius;
        float top = feetOffset + bodyHeight - bodyRadius;
        if (top < bottom) top = bottom;

        p1 = pos + Vector3.up * bottom;
        p2 = pos + Vector3.up * top;
    }

    bool IsObstacle(Collider c)
    {
        if (c == null) return false;
        if (c.transform.IsChildOf(transform)) return false;
        if (player != null && c.transform.IsChildOf(player.transform)) return false;
        if (c.GetComponentInParent<EnemyAI>() != null) return false;
        return true;
    }

    void ApplyHeight(float desiredY)
    {
        if (!flying) desiredY = startY;
        else desiredY += Mathf.Sin(Time.time * bobSpeed) * bobAmount;

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
        float h = Application.isPlaying ? heading : transform.eulerAngles.y + FrontYaw;
        Vector3 origin = transform.position + Vector3.up * 0.1f;

        DrawWedge(origin, h, viewAngle, visionDistance,
                  new Color(0f, 0.6f, 1f, 0.25f), new Color(0f, 0.8f, 1f, 1f));

        DrawWedge(origin, h + 180f, backstabAngle, backstabRange,
                  new Color(1f, 0.1f, 0.1f, 0.3f), new Color(1f, 0.2f, 0.2f, 1f));

        DrawWedge(origin, h, frontTouchAngle, catchDistance,
                  new Color(1f, 0.6f, 0f, 0.45f), new Color(1f, 0.7f, 0.1f, 1f));

        Vector3 center = Application.isPlaying ? startPos : transform.position;
        Gizmos.color = Color.green;
#if UNITY_EDITOR
        Handles.color = Color.green;
        Handles.DrawWireDisc(center + Vector3.up * 0.1f, Vector3.up, roamRadius);
#else
        Gizmos.DrawWireSphere(center, roamRadius);
#endif

        if (collideWithWalls)
        {
            Vector3 p1, p2;
            GetBodyCapsule(transform.position, out p1, out p2);
            Gizmos.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
            Gizmos.DrawWireSphere(p1, bodyRadius);
            Gizmos.DrawWireSphere(p2, bodyRadius);
        }
    }

    void DrawWedge(Vector3 origin, float centerYaw, float angle, float length, Color fill, Color line)
    {
        float half = angle * 0.5f;
        Vector3 left = origin + Quaternion.Euler(0f, centerYaw - half, 0f) * Vector3.forward * length;
        Vector3 right = origin + Quaternion.Euler(0f, centerYaw + half, 0f) * Vector3.forward * length;

#if UNITY_EDITOR
        Handles.color = fill;
        Handles.DrawAAConvexPolygon(origin, left, right);
#endif
        Gizmos.color = line;
        Gizmos.DrawLine(origin, left);
        Gizmos.DrawLine(origin, right);
        Gizmos.DrawLine(left, right);
    }
}