// using UnityEngine;
// using UnityEngine.UI;
// using UnityEngine.SceneManagement;
// #if ENABLE_INPUT_SYSTEM
// using UnityEngine.InputSystem;
// #endif
//
// public class TransformHUD : MonoBehaviour
// {
//     [Header("Optional - leave empty to auto-find the player")]
//     public PlayerMovement player;
//
//     [Header("Navigation")]
//     public string menuSceneName = "MainMenu";
//     public bool escReturnsToMenu = true;
//
//     private Transform target;
//     private Text hudText;
//     private Vector3 lastPos;
//     private float speed;
//
//     void Start()
//     {
//         if (player == null) player = FindFirstObjectByType<PlayerMovement>();
//         if (player != null)
//         {
//             target = player.transform;
//             lastPos = target.position;
//         }
//         else
//         {
//             Debug.LogWarning("TransformHUD: no PlayerMovement found in this scene.");
//         }
//         BuildHUD();
//     }
//
//     void Update()
//     {
//         if (escReturnsToMenu && EscapePressed())
//         {
//             SceneManager.LoadScene(menuSceneName);
//             return;
//         }
//
//         if (target == null || hudText == null) return;
//
//         if (Time.deltaTime > 0f)
//         {
//             Vector3 delta = target.position - lastPos;
//             delta.y = 0f;
//             float raw = delta.magnitude / Time.deltaTime;
//             speed = Mathf.Lerp(speed, raw, 10f * Time.deltaTime);
//         }
//         lastPos = target.position;
//
//         Vector3 s = target.localScale;
//         Vector3 r = target.eulerAngles;
//
//         hudText.text =
//             "<b>TRANSFORMATION HUD</b>\n" +
//             "Scale:      X " + s.x.ToString("0.00") + "   Y " + s.y.ToString("0.00") + "   Z " + s.z.ToString("0.00") + "\n" +
//             "Rotation:  X " + r.x.ToString("0.0") + "°   Y " + r.y.ToString("0.0") + "°   Z " + r.z.ToString("0.0") + "°\n" +
//             "Velocity:  " + speed.ToString("0.00") + " m/s";
//     }
//
//     void BuildHUD()
//     {
//         var canvasGO = new GameObject("HUDCanvas");
//         var canvas = canvasGO.AddComponent<Canvas>();
//         canvas.renderMode = RenderMode.ScreenSpaceOverlay;
//         canvas.sortingOrder = 100;
//         var scaler = canvasGO.AddComponent<CanvasScaler>();
//         scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
//         scaler.referenceResolution = new Vector2(1920, 1080);
//         scaler.matchWidthOrHeight = 0.5f;
//
//         var panel = new GameObject("HUDPanel", typeof(RectTransform));
//         panel.transform.SetParent(canvasGO.transform, false);
//         var bg = panel.AddComponent<Image>();
//         bg.color = new Color(0f, 0f, 0f, 0.55f);
//         bg.raycastTarget = false;
//         var prt = panel.GetComponent<RectTransform>();
//         prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0f, 1f);
//         prt.anchoredPosition = new Vector2(20f, -20f);
//         prt.sizeDelta = new Vector2(700f, 250f);
//
//         var textGO = new GameObject("HUDText", typeof(RectTransform));
//         textGO.transform.SetParent(panel.transform, false);
//         var trt = textGO.GetComponent<RectTransform>();
//         trt.anchorMin = Vector2.zero;
//         trt.anchorMax = Vector2.one;
//         trt.offsetMin = new Vector2(16f, 12f);
//         trt.offsetMax = new Vector2(-16f, -12f);
//
//         hudText = textGO.AddComponent<Text>();
//         hudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
//         hudText.fontSize = 28;
//         hudText.alignment = TextAnchor.UpperLeft;
//         hudText.color = Color.white;
//         hudText.supportRichText = true;
//         hudText.raycastTarget = false;
//         hudText.horizontalOverflow = HorizontalWrapMode.Overflow;
//         hudText.verticalOverflow = VerticalWrapMode.Overflow;
//     }
//
//     bool EscapePressed()
//     {
// #if ENABLE_INPUT_SYSTEM
//         return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
// #else
//         return Input.GetKeyDown(KeyCode.Escape);
// #endif
//     }
// }