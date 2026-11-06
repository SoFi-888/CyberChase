using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class MainMenu : MonoBehaviour
{
    [Header("Scene Names (must match your scene files and Build Profiles)")]
    public string[] levelScenes = { "Level1", "Level2", "Level3" };

    [Header("About Screen Text (edit these in the Inspector)")]
    public string gameTitle = "My 3D Game";
    public string developerName = "Your Name";
    [TextArea(3, 8)]
    public string projectOverview =
        "A 3D game prototype featuring a custom character model with real-time " +
        "transformation controls (rotation, scaling and a speed boost), three " +
        "distinct levels and a live transformation HUD.";

    private GameObject mainPanel, levelPanel, aboutPanel;
    private Font font;

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildUI();
        ShowPanel(mainPanel);
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

    void ShowPanel(GameObject panel)
    {
        mainPanel.SetActive(panel == mainPanel);
        levelPanel.SetActive(panel == levelPanel);
        aboutPanel.SetActive(panel == aboutPanel);
    }


    void BuildUI()
    {
        var canvasGO = new GameObject("MenuCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var bgGO = new GameObject("Background", typeof(RectTransform));
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bg = bgGO.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.09f, 0.14f, 1f);
        Stretch(bgGO.GetComponent<RectTransform>());

        mainPanel = MakePanel(canvasGO.transform, "MainPanel");
        MakeLabel(mainPanel.transform, gameTitle, 80, 140);
        MakeButton(mainPanel.transform, "Start Game", () => LoadLevel(0));
        MakeButton(mainPanel.transform, "Level Select", () => ShowPanel(levelPanel));
        MakeButton(mainPanel.transform, "About", () => ShowPanel(aboutPanel));
        MakeButton(mainPanel.transform, "Quit", QuitGame);

        levelPanel = MakePanel(canvasGO.transform, "LevelPanel");
        MakeLabel(levelPanel.transform, "Select Level", 64, 110);
        for (int i = 0; i < levelScenes.Length; i++)
        {
            int index = i;
            MakeButton(levelPanel.transform, "Level " + (i + 1), () => LoadLevel(index));
        }
        MakeButton(levelPanel.transform, "Back", () => ShowPanel(mainPanel));

        // ----- About panel -----
        aboutPanel = MakePanel(canvasGO.transform, "AboutPanel");
        MakeLabel(aboutPanel.transform, "About", 64, 90);
        MakeLabel(aboutPanel.transform, "Developer: " + developerName, 34, 50);
        MakeLabel(aboutPanel.transform, "PROJECT OVERVIEW\n" + projectOverview, 28, 150, 1300, TextAnchor.UpperCenter);
        MakeLabel(aboutPanel.transform,
            "CONTROLS\n" +
            "W A S D  -  Move\n" +
            "Space  -  Jump\n" +
            "Y (hold)  -  Grow bigger\n" +
            "F or Left Click  -  Attack (backstab enemies from behind)\n" +
            "Esc  -  Return to main menu",
            28, 260, 1300, TextAnchor.UpperCenter);
        MakeButton(aboutPanel.transform, "Back", () => ShowPanel(mainPanel));
    }

    GameObject MakePanel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());

        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 18;
        vlg.padding = new RectOffset(80, 80, 40, 40);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        return go;
    }

    Text MakeLabel(Transform parent, string text, int size, float height,
                   float width = 1200, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;
        return t;
    }

    Button MakeButton(Transform parent, string label, UnityAction onClick)
    {
        var go = new GameObject(label + " Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = Color.white;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = new Color(0.20f, 0.35f, 0.60f, 1f);
        colors.highlightedColor = new Color(0.35f, 0.55f, 0.90f, 1f);
        colors.pressedColor = new Color(0.12f, 0.22f, 0.40f, 1f);
        colors.selectedColor = colors.normalColor;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 420;
        le.preferredHeight = 70;

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        Stretch(textGO.GetComponent<RectTransform>());
        var t = textGO.AddComponent<Text>();
        t.font = font;
        t.text = label;
        t.fontSize = 34;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
        return btn;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }
}