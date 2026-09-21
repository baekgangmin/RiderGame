using UnityEngine;
using UnityEngine.UI;

// 머리 위 진행률 게이지 (M9) - 픽업 등, 제자리에서 진행되는 행동의 진행 상황을 화면(플레이어 머리 위)에 바로 보여주는 범용 게이지
// 월드공간 캔버스 대신, 매 프레임 대상의 머리 위 위치를 화면 좌표로 변환해서 스크린공간 오버레이 UI를 그 자리에 옮겨두는 방식
public class HeadGaugeUI : MonoBehaviour
{
    public static HeadGaugeUI Instance;

    [Header("대상 머리 위 오프셋 (월드 Y)")]
    public float headOffset = 2.2f;

    [Header("게이지 크기")]
    public Vector2 barSize = new Vector2(90f, 12f);

    private GameObject barGO;
    private Image fillImage;
    private RectTransform fillRect;
    private RectTransform bgRect;
    private Text labelText;

    private Transform target;
    private float progress;
    private bool visible;

    void Awake()
    {
        Instance = this;
        BuildUI();
    }

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("HeadGaugeCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        barGO = new GameObject("HeadGaugeBG");
        barGO.transform.SetParent(canvasGO.transform, false);
        Image bg = barGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bgRect = barGO.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0f);
        bgRect.anchorMax = new Vector2(0f, 0f);
        bgRect.pivot = new Vector2(0.5f, 0f);
        bgRect.sizeDelta = barSize;

        GameObject fillGO = new GameObject("HeadGaugeFill");
        fillGO.transform.SetParent(barGO.transform, false);
        fillImage = fillGO.AddComponent<Image>();
        fillImage.color = new Color(0.3f, 0.9f, 0.4f);

        fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        GameObject labelGO = new GameObject("HeadGaugeLabel");
        labelGO.transform.SetParent(barGO.transform, false);
        labelText = labelGO.AddComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 11;
        labelText.alignment = TextAnchor.LowerCenter;
        labelText.color = Color.white;
        labelText.text = "";
        RectTransform labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 2f);
        labelRect.sizeDelta = new Vector2(0f, 16f);

        barGO.SetActive(false);
    }

    // 다른 스크립트(DeliverySpot 등)에서 호출 - 대상 위에 진행률 게이지를 띄움
    public static void Show(Transform followTarget, float progress01, string label = "")
    {
        if (Instance == null)
        {
            return;
        }

        Instance.target = followTarget;
        Instance.progress = Mathf.Clamp01(progress01);
        Instance.visible = true;

        if (Instance.labelText != null)
        {
            Instance.labelText.text = label;
        }
        if (!Instance.barGO.activeSelf)
        {
            Instance.barGO.SetActive(true);
        }
    }

    public static void Hide()
    {
        if (Instance == null)
        {
            return;
        }

        Instance.visible = false;
        Instance.target = null;
        if (Instance.barGO != null)
        {
            Instance.barGO.SetActive(false);
        }
    }

    void LateUpdate()
    {
        if (!visible || target == null || barGO == null || !barGO.activeSelf)
        {
            return;
        }

        if (fillRect != null)
        {
            fillRect.anchorMax = new Vector2(progress, 1f);
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        Vector3 worldPos = target.position + Vector3.up * headOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        if (screenPos.z < 0f)
        {
            // 카메라 뒤쪽이면 화면 밖으로 치워둠
            bgRect.anchoredPosition = new Vector2(-9999f, -9999f);
            return;
        }

        bgRect.anchoredPosition = new Vector2(screenPos.x, screenPos.y);
    }
}
