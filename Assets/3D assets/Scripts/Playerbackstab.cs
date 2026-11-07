using UnityEngine;
using UnityEngine.UI;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(PlayerMovement))]
public class PlayerBackstab : MonoBehaviour
{
    [Header("Attack")]
    [Tooltip("How far above or below the player the enemy can be.")]
    public float verticalReach = 3f;
    public float attackDuration = 0.9f;
    public float hitDelay = 0.35f;
    public bool autoFaceEnemy = true;
    public bool debugLogs = false;

    [Header("Animator")]
    [Tooltip("Name of the Trigger parameter that plays the backstab animation.")]
    public string backstabTrigger = "Backstab";

    [Header("On-Screen Feedback")]
    public string successMessage = "Backstab Successful";
    public string failMessage = "Attack Failed";
    public float messageTime = 1.5f;
    public int fontSize = 64;
    public Color successColor = new Color(0.3f, 1f, 0.4f);
    public Color failColor = new Color(1f, 0.3f, 0.3f);

    private PlayerMovement movement;
    private bool attacking;

    private Text messageText;
    private float messageTimer;
    private Color messageColor;

    void Start()
    {
        movement = GetComponent<PlayerMovement>();
        BuildMessageUI();
    }

    void Update()
    {
        UpdateMessage();

        if (!attacking && AttackPressed())
            StartCoroutine(AttackRoutine());
    }

    IEnumerator AttackRoutine()
    {
        attacking = true;
        movement.movementLocked = true;
        movement.PlayTrigger(backstabTrigger);

        if (autoFaceEnemy)
        {
            EnemyAI nearest = FindNearestEnemy();
            if (nearest != null) FaceToward(nearest.transform.position);
        }

        yield return new WaitForSeconds(hitDelay);
        ResolveHit();

        yield return new WaitForSeconds(Mathf.Max(0f, attackDuration - hitDelay));

        movement.movementLocked = false;
        attacking = false;
    }

    void ResolveHit()
    {
        EnemyAI target = FindNearestEnemy();

        if (target == null)
        {
            if (debugLogs) Debug.Log("PlayerBackstab: no enemy in range.");
            ShowMessage(failMessage, failColor);
            return;
        }

        float dot = target.DotToward(transform.position);
        bool isBehind = target.IsBehind(transform.position);

        if (debugLogs)
            Debug.Log("PlayerBackstab: dot = " + dot.ToString("0.00") + " (needs <= " + target.BackstabDotLimit.ToString("0.00") + ") -> " +
                      (isBehind ? "BACKSTAB" : "FAILED"));

        if (isBehind)
        {
            target.Die();
            ShowMessage(successMessage, successColor);
        }
        else
        {
            ShowMessage(failMessage, failColor);
        }
    }

    EnemyAI FindNearestEnemy()
    {
        EnemyAI best = null;
        float bestDist = float.MaxValue;

        foreach (EnemyAI e in EnemyAI.All)
        {
            if (e == null || e.IsDead) continue;

            Vector3 delta = e.transform.position - transform.position;
            if (Mathf.Abs(delta.y) > verticalReach) continue;

            delta.y = 0f;
            float d = delta.magnitude;
            if (d <= e.backstabRange && d <= bestDist)
            {
                bestDist = d;
                best = e;
            }
        }
        return best;
    }

    void FaceToward(Vector3 worldPos)
    {
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        movement.FaceDirection(dir);
    }

    void BuildMessageUI()
    {
        GameObject canvasGO = new GameObject("BackstabFeedbackCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject textGO = new GameObject("FeedbackText", typeof(RectTransform));
        textGO.transform.SetParent(canvasGO.transform, false);

        RectTransform rt = textGO.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.78f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1400f, 160f);
        rt.anchoredPosition = Vector2.zero;

        messageText = textGO.AddComponent<Text>();
        messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        messageText.fontSize = fontSize;
        messageText.fontStyle = FontStyle.Bold;
        messageText.alignment = TextAnchor.MiddleCenter;
        messageText.raycastTarget = false;
        messageText.text = "";

        Outline outline = textGO.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(3f, -3f);
    }

    void ShowMessage(string text, Color color)
    {
        if (messageText == null) return;
        messageText.text = text;
        messageColor = color;
        messageText.color = color;
        messageTimer = messageTime;
    }

    void UpdateMessage()
    {
        if (messageText == null || messageTimer <= 0f) return;

        messageTimer -= Time.deltaTime;

        float alpha = Mathf.Clamp01(messageTimer / 0.4f);
        Color c = messageColor;
        c.a = alpha;
        messageText.color = c;

        if (messageTimer <= 0f) messageText.text = "";
    }

    bool AttackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F);
#endif
    }

}