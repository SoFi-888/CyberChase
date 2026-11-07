using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TransformHUD : MonoBehaviour
{
    public PlayerMovement player;

    [Header("Navigation")]
    public string menuSceneName = "MainMenu";
    public bool escReturnsToMenu = true;

    [Header("Floating Label")]
    [Tooltip("Shows the player's position above their head. It is placed using the camera's view and projection matrices.")]
    public bool showFloatingLabel = true;

    [Header("Move Detection")]
    [Tooltip("The player counts as 'moving' above this speed (units per second).")]
    public float movingSpeedThreshold = 0.2f;
    [Tooltip("A jump in position bigger than this in one frame is a respawn, not a normal move.")]
    public float teleportDistance = 5f;

    private Transform target;
    private CharacterController controller;
    private Camera cam;

    private Text hudText;
    private Text floatText;
    private RectTransform floatRT;
    private Font font;

    private Vector3 lastPos;
    private Vector3 moveStart;
    private bool wasMoving;
    private float speed;
    private float verticalSpeed;

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
        {
            target = player.transform;
            controller = player.GetComponent<CharacterController>();
            lastPos = target.position;
            moveStart = target.position;
        }
        else
        {
            Debug.LogWarning("TransformHUD: no PlayerMovement found in this scene.");
        }

        cam = Camera.main;
        font = LoadFont();
        BuildHUD();
    }

    void Update()
    {
        if (escReturnsToMenu && EscapePressed())
        {
            SceneManager.LoadScene(menuSceneName);
            return;
        }

        if (target == null || hudText == null) return;
        if (cam == null) cam = Camera.main;

        float dt = Time.deltaTime;
        Vector3 pos = target.position;

        if (dt > 0f)
        {
            Vector3 frameDelta = pos - lastPos;

            if (frameDelta.magnitude > teleportDistance)
            {
                moveStart = pos;
                wasMoving = false;
                speed = 0f;
                verticalSpeed = 0f;
            }
            else
            {
                Vector3 flat = new Vector3(frameDelta.x, 0f, frameDelta.z);
                speed = Mathf.Lerp(speed, flat.magnitude / dt, 10f * dt);
                verticalSpeed = Mathf.Lerp(verticalSpeed, frameDelta.y / dt, 10f * dt);

                bool movingNow = frameDelta.magnitude / dt > movingSpeedThreshold;
                if (movingNow && !wasMoving) moveStart = lastPos;
                wasMoving = movingNow;
            }
        }
        lastPos = pos;

        UpdatePanel(pos);
        UpdateFloatingLabel(pos);
    }

    void UpdatePanel(Vector3 newPos)
    {
        Vector3 current = moveStart;

        Matrix4x4 T = Matrix4x4.Translate(newPos - current);
        Vector3 check = T.MultiplyPoint3x4(current);
        float distance = (newPos - current).magnitude;

        bool grounded = player != null ? player.IsGrounded : (controller != null && controller.isGrounded);
        string groundedText = grounded
            ? "<color=#4dff88>YES</color>"
            : "<color=#ff9a3d>NO (in air)</color>";
        float gravity = player != null ? player.gravity : 0f;

        hudText.text =
            "<b><color=#00f2ff>PLAYER HUD</color></b>\n" +
            "Current Position (before move):\n" +
            "  " + V(current) + "\n" +
            "New Position (after move):\n" +
            "  " + V(check) + "\n" +
            "Moved: " + distance.ToString("0.00") + " units\n" +
            "<color=#ffe61a>New = T x Current</color>\n" +
            "  | " + F(T.m00) + F(T.m01) + F(T.m02) + F(T.m03) + " |\n" +
            "  | " + F(T.m10) + F(T.m11) + F(T.m12) + F(T.m13) + " |\n" +
            "  | " + F(T.m20) + F(T.m21) + F(T.m22) + F(T.m23) + " |\n" +
            "  | " + F(T.m30) + F(T.m31) + F(T.m32) + F(T.m33) + " |\n" +
            "Rotation: Y " + target.eulerAngles.y.ToString("0.0") + "\u00B0\n" +
            "Velocity: " + speed.ToString("0.00") + " m/s   Vertical: " + verticalSpeed.ToString("0.00") + " m/s\n" +
            "Grounded: " + groundedText + "   Gravity: " + gravity.ToString("0.00");
    }

    void UpdateFloatingLabel(Vector3 pos)
    {
        if (floatText == null) return;

        if (!showFloatingLabel || cam == null)
        {
            floatText.gameObject.SetActive(false);
            return;
        }
        Vector3 head = pos + Vector3.up * 2.5f;
        if (controller != null)
        {
            Bounds b = controller.bounds;
            head = new Vector3(b.center.x, b.max.y + 0.4f, b.center.z);
        }

        Vector2 screen;
        bool inFront = WorldToScreenWithMatrices(head, cam, out screen);

        floatText.gameObject.SetActive(inFront);
        if (!inFront) return;

        floatRT.position = new Vector3(screen.x, screen.y, 0f);
        floatText.text = "X " + pos.x.ToString("0.0") + "  Y " + pos.y.ToString("0.0") + "  Z " + pos.z.ToString("0.0");
    }

    bool WorldToScreenWithMatrices(Vector3 world, Camera camera, out Vector2 screen)
    {
        Matrix4x4 viewProjection = camera.projectionMatrix * camera.worldToCameraMatrix;
        Vector4 clip = viewProjection * new Vector4(world.x, world.y, world.z, 1f);

        if (clip.w <= 0.0001f)
        {
            screen = Vector2.zero;
            return false;
        }

        Vector3 ndc = new Vector3(clip.x, clip.y, clip.z) / clip.w;
        screen = new Vector2((ndc.x * 0.5f + 0.5f) * Screen.width,
                             (ndc.y * 0.5f + 0.5f) * Screen.height);
        return true;
    }

    void BuildHUD()
    {
        GameObject canvasGO = new GameObject("HUDCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("HUDPanel", typeof(RectTransform));
        panel.transform.SetParent(canvasGO.transform, false);
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);
        bg.raycastTarget = false;
        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0f, 1f);
        prt.anchoredPosition = new Vector2(20f, -20f);
        prt.sizeDelta = new Vector2(760f, 430f);

        GameObject textGO = new GameObject("HUDText", typeof(RectTransform));
        textGO.transform.SetParent(panel.transform, false);
        RectTransform trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(16f, 12f);
        trt.offsetMax = new Vector2(-16f, -12f);

        hudText = textGO.AddComponent<Text>();
        hudText.font = font;
        hudText.fontSize = 24;
        hudText.alignment = TextAnchor.UpperLeft;
        hudText.color = Color.white;
        hudText.supportRichText = true;
        hudText.raycastTarget = false;
        hudText.horizontalOverflow = HorizontalWrapMode.Overflow;
        hudText.verticalOverflow = VerticalWrapMode.Overflow;

        GameObject floatGO = new GameObject("PlayerPositionLabel", typeof(RectTransform));
        floatGO.transform.SetParent(canvasGO.transform, false);
        floatRT = floatGO.GetComponent<RectTransform>();
        floatRT.sizeDelta = new Vector2(520f, 44f);
        floatRT.pivot = new Vector2(0.5f, 0f);

        floatText = floatGO.AddComponent<Text>();
        floatText.font = font;
        floatText.fontSize = 26;
        floatText.fontStyle = FontStyle.Bold;
        floatText.alignment = TextAnchor.MiddleCenter;
        floatText.color = new Color(0f, 0.95f, 1f);
        floatText.raycastTarget = false;
        floatText.horizontalOverflow = HorizontalWrapMode.Overflow;

        Outline outline = floatGO.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
    }
    Font LoadFont()
    {
        Font f = Font.CreateDynamicFontFromOSFont(new string[] { "Consolas", "Courier New", "Lucida Console" }, 24);
        if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f;
    }

    static string V(Vector3 v)
    {
        return "X " + v.x.ToString("0.00") + "   Y " + v.y.ToString("0.00") + "   Z " + v.z.ToString("0.00");
    }

    static string F(float value)
    {
        return value.ToString("0.00").PadLeft(8);
    }

    bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}