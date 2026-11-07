using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Text;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class MainMenu : MonoBehaviour
{
    [Header("Scene Names (must match your scene files and Build Profiles)")]
    public string[] levelScenes = { "Level1", "Level2", "Level3" };

    [Header("Menu Text (edit these in the Inspector)")]
    public string gameTitle = "My 3D Game";
    public string tagline = "// SYSTEM ONLINE";
    public string developerName = "Your Name";
    [TextArea(3, 8)]
    public string projectOverview =
        "A cyberpunk 3D stealth-platformer prototype. Sneak up behind patrolling enemies for a " +
        "backstab, dodge spikes, lasers, bombs and lava, and reach the endpoint of each of the three levels.";
    [Tooltip("One control per line, written as:  KEYS  -  What it does")]
    [TextArea(6, 12)]
    public string controlsText =
        "W A S D  -  Move\n" +
        "Space  -  Jump\n" +
        "Hold Shift  -  Run\n" +
        "Hold Right Click  -  Pan the camera\n" +
        "Mouse Scroll  -  Zoom in / out\n" +
        "F  -  Backstab an enemy from behind\n" +
        "Esc  -  Return to the main menu";

    [Header("Cyberpunk Theme")]
    public Color neonCyan = new Color(0f, 0.95f, 1f);
    public Color neonMagenta = new Color(1f, 0.1f, 0.65f);
    public Color neonYellow = new Color(1f, 0.9f, 0.1f);
    public Color backgroundTop = new Color(0.04f, 0f, 0.10f);
    public Color backgroundBottom = new Color(0f, 0.03f, 0.09f);
    [Tooltip("Turn off to stop the moving effects (glitch, scrolling grid, scan bar).")]
    public bool animateEffects = true;


    private class NeonButton
    {
        public Image fill, accentBar;
        public Text label;
        public RectTransform labelRT;
        public Color accent;
        public float hover;
        public bool hovered, pressed;
    }

    private class PulseItem
    {
        public Image img;
        public Color baseColor;
        public float phase;
    }

    private GameObject mainWindow, levelWindow, aboutWindow;
    private GameObject titleGroup;
    private Text titleMain, titleMagenta, titleCyan, taglineText, clockText;
    private RawImage gridRaw;
    private RectTransform scanBand;
    private Image glitchBar;
    private Font font;

    private readonly List<NeonButton> neonButtons = new List<NeonButton>();
    private readonly List<PulseItem> pulseItems = new List<PulseItem>();

    private readonly Color buttonFill = new Color(0.03f, 0.05f, 0.12f, 0.92f);
    private readonly Color windowFill = new Color(0.02f, 0.02f, 0.07f, 0.90f);
    private const float GridTilePixels = 96f;

    private float glitchTimer = 2f;
    private float glitchTimeLeft;
    private bool glitching;
    private float clockTimer;
    private string taglineBase;

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        font = LoadFont();
        EnsureEventSystem();
        BuildUI();
        ShowPanel(mainWindow);
    }

    Font LoadFont()
    {
        Font f = Font.CreateDynamicFontFromOSFont(new string[] { "Consolas", "Lucida Console", "Courier New" }, 32);
        if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f;
    }

    void LoadLevel(int index)
    {
        if (index < 0 || index >= levelScenes.Length)
        {
            Debug.LogWarning("MainMenu: no level at index " + index);
            return;
        }
        SceneManager.LoadScene(levelScenes[index]);
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ShowPanel(GameObject window)
    {
        mainWindow.SetActive(window == mainWindow);
        levelWindow.SetActive(window == levelWindow);
        aboutWindow.SetActive(window == aboutWindow);

        titleGroup.SetActive(window != aboutWindow);

        foreach (NeonButton b in neonButtons)
        {
            b.hovered = false;
            b.pressed = false;
        }
    }

    void Update()
    {
        float t = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        // Buttons light up on hover
        foreach (NeonButton b in neonButtons)
        {
            b.hover = Mathf.MoveTowards(b.hover, b.hovered ? 1f : 0f, dt * 9f);

            Color litFill = new Color(b.accent.r, b.accent.g, b.accent.b, 0.92f);
            Color dark = new Color(0.02f, 0.02f, 0.08f, 1f);

            b.fill.color = b.pressed ? neonYellow : Color.Lerp(buttonFill, litFill, b.hover);
            b.label.color = (b.pressed || b.hover > 0.5f) ? Color.Lerp(b.accent, dark, b.hover) : b.accent;
            b.accentBar.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(8f, 22f, b.hover), 0f);
            b.labelRT.offsetMin = new Vector2(Mathf.Lerp(40f, 64f, b.hover), 0f);
        }

        // Neon borders gently pulse
        foreach (PulseItem p in pulseItems)
        {
            Color c = p.baseColor;
            c.a = p.baseColor.a * (0.7f + 0.3f * Mathf.Sin(t * 2.2f + p.phase));
            p.img.color = c;
        }

        if (!animateEffects) return;

        gridRaw.uvRect = new Rect(0f, Mathf.Repeat(t * 0.12f, 1f), 1920f / GridTilePixels, 1080f / GridTilePixels);

        scanBand.anchoredPosition = new Vector2(0f, 700f - Mathf.Repeat(t * 220f, 1400f));

        UpdateTitleGlitch(dt, t);

        bool cursorOn = Mathf.Repeat(t, 1f) < 0.55f;
        taglineText.text = taglineBase + (cursorOn ? "_" : " ");

        clockTimer -= dt;
        if (clockTimer <= 0f)
        {
            clockTimer = 0.5f;
            clockText.text = "NEON CITY  " + System.DateTime.Now.ToString("HH:mm:ss");
        }
    }

    void UpdateTitleGlitch(float dt, float t)
    {
        glitchTimer -= dt;
        if (!glitching && glitchTimer <= 0f)
        {
            glitching = true;
            glitchTimeLeft = Random.Range(0.08f, 0.22f);
            glitchTimer = Random.Range(2.5f, 5.5f);
        }

        if (glitching)
        {
            float j = Random.Range(8f, 22f);
            titleMagenta.rectTransform.anchoredPosition = new Vector2(-j, Random.Range(-4f, 4f));
            titleCyan.rectTransform.anchoredPosition = new Vector2(j, Random.Range(-4f, 4f));
            titleMain.rectTransform.anchoredPosition = new Vector2(Random.Range(-6f, 6f), 0f);

            glitchBar.gameObject.SetActive(true);
            glitchBar.rectTransform.anchoredPosition = new Vector2(0f, Random.Range(-500f, 500f));
            glitchBar.rectTransform.sizeDelta = new Vector2(0f, Random.Range(4f, 22f));
            glitchBar.color = Random.value < 0.5f
                ? new Color(neonMagenta.r, neonMagenta.g, neonMagenta.b, 0.45f)
                : new Color(neonCyan.r, neonCyan.g, neonCyan.b, 0.45f);

            glitchTimeLeft -= dt;
            if (glitchTimeLeft <= 0f) glitching = false;
        }
        else
        {
            float wobble = Mathf.Sin(t * 1.5f) * 1.5f;
            titleMagenta.rectTransform.anchoredPosition = new Vector2(-4f + wobble, 0f);
            titleCyan.rectTransform.anchoredPosition = new Vector2(4f - wobble, 0f);
            titleMain.rectTransform.anchoredPosition = Vector2.zero;
            glitchBar.gameObject.SetActive(false);
        }
    }

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("MenuCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        Transform root = canvasGO.transform;

        BuildBackground(root);
        BuildTitle(root);
        BuildMainWindow(root);
        BuildLevelWindow(root);
        BuildAboutWindow(root);
        BuildScreenFrame(root);
        BuildOverlays(root);
    }

    void BuildBackground(Transform root)
    {
        MakeRaw(root, "Background", VerticalGradient(backgroundTop, backgroundBottom, 256), Color.white);

        gridRaw = MakeRaw(root, "Grid", GridTexture(128, 2), new Color(neonMagenta.r, neonMagenta.g, neonMagenta.b, 0.28f));
        gridRaw.uvRect = new Rect(0f, 0f, 1920f / GridTilePixels, 1080f / GridTilePixels);

        Color clearTop = new Color(backgroundTop.r, backgroundTop.g, backgroundTop.b, 0f);
        RawImage fade = MakeRaw(root, "HorizonFade", VerticalGradient(backgroundTop, clearTop, 256), Color.white);
        fade.rectTransform.anchorMin = new Vector2(0f, 0.4f);

        scanBand = NewRect("ScanBar", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                           new Vector2(0.5f, 0.5f), new Vector2(2400f, 160f), Vector2.zero);
        Image scan = scanBand.gameObject.AddComponent<Image>();
        scan.color = new Color(neonCyan.r, neonCyan.g, neonCyan.b, 0.05f);
        scan.raycastTarget = false;
    }

    void BuildTitle(Transform root)
    {
        RectTransform group = NewRect("Title", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                      new Vector2(0.5f, 1f), new Vector2(1700f, 240f), new Vector2(0f, -50f));
        titleGroup = group.gameObject;

        string title = gameTitle.ToUpper();
        titleMagenta = MakeText(group, title, 120, neonMagenta, TextAnchor.UpperCenter);
        titleCyan = MakeText(group, title, 120, neonCyan, TextAnchor.UpperCenter);
        titleMain = MakeText(group, title, 120, new Color(0.92f, 1f, 1f), TextAnchor.UpperCenter);

        Outline glow = titleMain.gameObject.AddComponent<Outline>();
        glow.effectColor = new Color(neonCyan.r, neonCyan.g, neonCyan.b, 0.35f);
        glow.effectDistance = new Vector2(3f, -3f);

        taglineBase = tagline.ToUpper();
        RectTransform tagRT = NewRect("Tagline", group, new Vector2(0f, 0f), new Vector2(1f, 0f),
                                      new Vector2(0.5f, 0f), new Vector2(0f, 50f), Vector2.zero);
        taglineText = MakeText(tagRT, taglineBase, 34, neonYellow, TextAnchor.MiddleCenter);
    }

    void BuildMainWindow(Transform root)
    {
        RectTransform content = MakeWindow(root, "MainWindow", "// MAIN_MENU", new Vector2(760f, 620f),
                                           new Vector2(0f, -150f), TextAnchor.MiddleCenter, out mainWindow);

        MakeLabel(content, "> SELECT AN OPTION", 26, 40, 620f, Alpha(neonCyan, 0.7f), TextAnchor.MiddleLeft);
        MakeNeonButton(content, "Start Game", () => LoadLevel(0), neonCyan);
        MakeNeonButton(content, "Level Select", () => ShowPanel(levelWindow), neonCyan);
        MakeNeonButton(content, "About", () => ShowPanel(aboutWindow), neonCyan);
        MakeNeonButton(content, "Quit", QuitGame, neonMagenta);
    }

    void BuildLevelWindow(Transform root)
    {
        RectTransform content = MakeWindow(root, "LevelWindow", "// LEVEL_SELECT", new Vector2(760f, 620f),
                                           new Vector2(0f, -150f), TextAnchor.MiddleCenter, out levelWindow);

        MakeLabel(content, "SELECT MISSION", 48, 70, 620f, neonMagenta, TextAnchor.MiddleLeft);
        for (int i = 0; i < levelScenes.Length; i++)
        {
            int index = i;
            MakeNeonButton(content, "Level " + (i + 1).ToString("00"), () => LoadLevel(index), neonCyan);
        }
        MakeNeonButton(content, "Back", () => ShowPanel(mainWindow), neonMagenta);
    }

    void BuildAboutWindow(Transform root)
    {
        RectTransform content = MakeWindow(root, "AboutWindow", "// ABOUT", new Vector2(1300f, 900f),
                                           Vector2.zero, TextAnchor.UpperCenter, out aboutWindow);

        string mg = Hex(neonMagenta);

        MakeLabel(content, "ABOUT", 56, 70, 1100f, neonCyan, TextAnchor.MiddleLeft);

        MakeLabel(content, "<color=#" + mg + ">// DEVELOPER</color>\n" + developerName,
                  30, 90, 1100f, Color.white, TextAnchor.UpperLeft);

        int overviewLines = Mathf.CeilToInt(projectOverview.Length / 70f);
        MakeLabel(content, "<color=#" + mg + ">// PROJECT OVERVIEW</color>\n" + projectOverview,
                  28, 50f + overviewLines * 34f, 1100f, Color.white, TextAnchor.UpperLeft);

        int controlLines = controlsText.Split('\n').Length;
        MakeLabel(content, "<color=#" + mg + ">// CONTROLS</color>\n" + FormatControls(),
                  28, 50f + controlLines * 36f, 1100f, Color.white, TextAnchor.UpperLeft);

        MakeNeonButton(content, "Back", () => ShowPanel(mainWindow), neonMagenta);
    }

    string FormatControls()
    {
        string yl = Hex(neonYellow);
        StringBuilder sb = new StringBuilder();

        foreach (string raw in controlsText.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;

            int dash = line.IndexOf('-');
            if (dash > 0)
            {
                string keys = line.Substring(0, dash).Trim();
                string action = line.Substring(dash + 1).Trim();
                sb.Append("<color=#" + yl + ">" + ("[ " + keys + " ]").PadRight(26) + "</color>" + action);
            }
            else
            {
                sb.Append(line);
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }
    void BuildScreenFrame(Transform root)
    {
        RectTransform frame = NewRect("ScreenFrame", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                                      Vector2.zero, Vector2.zero);
        frame.offsetMin = new Vector2(28f, 28f);
        frame.offsetMax = new Vector2(-28f, -28f);

        Corners(frame, neonCyan, 90f, 6f);

        MakeCornerText(frame, "NEXUS-OS v2.077", new Vector2(0f, 1f), new Vector2(20f, -14f), neonCyan, TextAnchor.UpperLeft);
        MakeCornerText(frame, "SIGNAL: STABLE", new Vector2(1f, 1f), new Vector2(-20f, -14f), neonMagenta, TextAnchor.UpperRight);
        MakeCornerText(frame, "SYS.STATUS // ONLINE", new Vector2(0f, 0f), new Vector2(20f, 14f), neonCyan, TextAnchor.LowerLeft);
        clockText = MakeCornerText(frame, "NEON CITY  00:00:00", new Vector2(1f, 0f), new Vector2(-20f, 14f), neonMagenta, TextAnchor.LowerRight);
    }
    void BuildOverlays(Transform root)
    {
        RawImage scanlines = MakeRaw(root, "Scanlines", ScanlineTexture(), new Color(1f, 1f, 1f, 1f));
        scanlines.uvRect = new Rect(0f, 0f, 1f, 270f);

        MakeRaw(root, "Vignette", VignetteTexture(128), Color.white);

        RectTransform bar = NewRect("GlitchBar", root, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                                    new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), Vector2.zero);
        glitchBar = bar.gameObject.AddComponent<Image>();
        glitchBar.raycastTarget = false;
        glitchBar.gameObject.SetActive(false);
    }

    RectTransform MakeWindow(Transform parent, string name, string header, Vector2 size, Vector2 pos,
                             TextAnchor align, out GameObject windowRoot)
    {
        RectTransform win = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                    new Vector2(0.5f, 0.5f), size, pos);
        windowRoot = win.gameObject;

        Image bg = win.gameObject.AddComponent<Image>();
        bg.color = windowFill;

        Frame(win, neonCyan, 2f);
        Corners(win, neonMagenta, 36f, 5f);

        // Header bar
        RectTransform head = NewRect("Header", win, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                     new Vector2(0.5f, 1f), new Vector2(0f, 52f), Vector2.zero);
        Image headBg = head.gameObject.AddComponent<Image>();
        headBg.color = new Color(neonCyan.r, neonCyan.g, neonCyan.b, 0.16f);
        headBg.raycastTarget = false;

        Text headText = MakeText(head, header, 26, neonCyan, TextAnchor.MiddleLeft);
        headText.rectTransform.offsetMin = new Vector2(24f, 0f);

        Bar(head, "Dot1", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(14f, 14f), new Vector2(-24f, 0f), neonMagenta);
        Bar(head, "Dot2", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(14f, 14f), new Vector2(-48f, 0f), neonYellow);
        Bar(head, "Dot3", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(14f, 14f), new Vector2(-72f, 0f), neonCyan);
        Bar(head, "Line", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), Vector2.zero, neonCyan);

        RectTransform content = NewRect("Content", win, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                                        Vector2.zero, Vector2.zero);
        content.offsetMax = new Vector2(0f, -52f);

        VerticalLayoutGroup vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = align;
        vlg.spacing = 18f;
        vlg.padding = new RectOffset(60, 60, 36, 36);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        return content;
    }

    Button MakeNeonButton(Transform parent, string text, UnityAction onClick, Color accent)
    {
        GameObject go = new GameObject(text + " Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        NeonButton nb = new NeonButton();
        nb.accent = accent;

        nb.fill = go.AddComponent<Image>();
        nb.fill.color = buttonFill;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = nb.fill;
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(onClick);

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 620f;
        le.preferredHeight = 76f;

        nb.accentBar = Bar(go.transform, "Accent", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                           new Vector2(8f, 0f), Vector2.zero, accent);
        Bar(go.transform, "Underline", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 2f), Vector2.zero, Alpha(accent, 0.5f));

        nb.label = MakeText(go.transform, "> " + text.ToUpper(), 34, accent, TextAnchor.MiddleLeft);
        nb.labelRT = nb.label.rectTransform;
        nb.labelRT.offsetMin = new Vector2(40f, 0f);

        AddEvent(go, EventTriggerType.PointerEnter, e => nb.hovered = true);
        AddEvent(go, EventTriggerType.PointerExit, e => { nb.hovered = false; nb.pressed = false; });
        AddEvent(go, EventTriggerType.Select, e => nb.hovered = true);
        AddEvent(go, EventTriggerType.Deselect, e => nb.hovered = false);
        AddEvent(go, EventTriggerType.PointerDown, e => nb.pressed = true);
        AddEvent(go, EventTriggerType.PointerUp, e => nb.pressed = false);

        neonButtons.Add(nb);
        return btn;
    }

    void AddEvent(GameObject go, EventTriggerType type, UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = go.GetComponent<EventTrigger>();
        if (trigger == null) trigger = go.AddComponent<EventTrigger>();

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = type;
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    void Frame(RectTransform parent, Color color, float th)
    {
        AddPulse(Bar(parent, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, th), Vector2.zero, color), 0f);
        AddPulse(Bar(parent, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, th), Vector2.zero, color), 1f);
        AddPulse(Bar(parent, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(th, 0f), Vector2.zero, color), 2f);
        AddPulse(Bar(parent, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(th, 0f), Vector2.zero, color), 3f);
    }

    void Corners(RectTransform parent, Color color, float length, float th)
    {
        for (int cx = 0; cx <= 1; cx++)
        {
            for (int cy = 0; cy <= 1; cy++)
            {
                Vector2 corner = new Vector2(cx, cy);
                Bar(parent, "CornerH", corner, corner, corner, new Vector2(length, th), Vector2.zero, color);
                Bar(parent, "CornerV", corner, corner, corner, new Vector2(th, length), Vector2.zero, color);
            }
        }
    }

    void AddPulse(Image img, float phase)
    {
        PulseItem p = new PulseItem();
        p.img = img;
        p.baseColor = img.color;
        p.phase = phase;
        pulseItems.Add(p);
    }

    RectTransform NewRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot,
                          Vector2 size, Vector2 pos)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    Image Bar(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot,
              Vector2 size, Vector2 pos, Color color)
    {
        RectTransform rt = NewRect(name, parent, aMin, aMax, pivot, size, pos);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Text MakeText(Transform parent, string text, int size, Color color, TextAnchor anchor)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());

        Text t = go.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = anchor;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    Text MakeLabel(Transform parent, string text, int size, float height, float width, Color color, TextAnchor anchor)
    {
        Text t = MakeText(parent, text, size, color, anchor);
        LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;
        return t;
    }

    Text MakeCornerText(RectTransform parent, string text, Vector2 corner, Vector2 pos, Color color, TextAnchor anchor)
    {
        RectTransform rt = NewRect("Status", parent, corner, corner, corner, new Vector2(700f, 40f), pos);
        Text t = MakeText(rt, text, 22, color, anchor);
        return t;
    }

    RawImage MakeRaw(Transform parent, string name, Texture tex, Color tint)
    {
        RectTransform rt = NewRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        RawImage raw = rt.gameObject.AddComponent<RawImage>();
        raw.texture = tex;
        raw.color = tint;
        raw.raycastTarget = false;
        return raw;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Color Alpha(Color c, float a)
    {
        return new Color(c.r, c.g, c.b, a);
    }

    static string Hex(Color c)
    {
        return ColorUtility.ToHtmlStringRGB(c);
    }

    Texture2D VerticalGradient(Color top, Color bottom, int height)
    {
        Texture2D tex = new Texture2D(1, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < height; y++)
            tex.SetPixel(0, y, Color.Lerp(bottom, top, y / (float)(height - 1)));
        tex.Apply();
        return tex;
    }

    Texture2D GridTexture(int size, int lineWidth)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, (x < lineWidth || y < lineWidth) ? Color.white : clear);
        tex.Apply();
        return tex;
    }

    Texture2D ScanlineTexture()
    {
        Texture2D tex = new Texture2D(1, 4, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;
        tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.32f));
        tex.SetPixel(0, 1, new Color(0f, 0f, 0f, 0f));
        tex.SetPixel(0, 2, new Color(0f, 0f, 0f, 0f));
        tex.SetPixel(0, 3, new Color(0f, 0f, 0f, 0f));
        tex.Apply();
        return tex;
    }

    Texture2D VignetteTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x / (float)(size - 1) - 0.5f) * 2f;
                float dy = (y / (float)(size - 1) - 0.5f) * 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.SmoothStep(0.45f, 1.1f, d) * 0.85f;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }
}