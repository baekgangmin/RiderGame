using UnityEngine;
using UnityEngine.UI;

// 좌하단 스태미나 게이지 (M5) - 씬에 아무것도 없어도 실행하면 자동으로 UI를 만듦
// Image.fillAmount 대신 RectTransform 너비를 직접 줄이는 방식이라 더 확실하게 동작함
public class StaminaUI : MonoBehaviour
{
    private Image fillImage;
    private RectTransform fillRect;

    void Start()
    {
        BuildUI();
    }

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("StaminaCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject bgGO = new GameObject("StaminaBarBG");
        bgGO.transform.SetParent(canvasGO.transform, false);
        Image bg = bgGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.5f);
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0f);
        bgRect.anchorMax = new Vector2(0f, 0f);
        bgRect.pivot = new Vector2(0f, 0f);
        bgRect.anchoredPosition = new Vector2(30f, 30f);
        bgRect.sizeDelta = new Vector2(190f, 22f);

        GameObject fillGO = new GameObject("StaminaBarFill");
        fillGO.transform.SetParent(bgGO.transform, false);
        fillImage = fillGO.AddComponent<Image>();
        fillImage.color = new Color(0.2f, 0.8f, 0.3f);

        fillRect = fillGO.GetComponent<RectTransform>();
        // 왼쪽에 고정하고, 오른쪽 경계(anchorMax.x)만 스태미나 비율만큼 움직여서 너비를 줄임
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
    }

    void Update()
    {
        if (fillRect == null || StaminaSystem.Instance == null)
        {
            return;
        }

        float percent = Mathf.Clamp01(StaminaSystem.Instance.StaminaPercent);

        // 오른쪽 경계를 percent 위치로 당겨서 바 너비를 직접 줄임
        fillRect.anchorMax = new Vector2(percent, 1f);

        fillImage.color = percent <= 0.2f
            ? new Color(0.85f, 0.2f, 0.2f)
            : new Color(0.2f, 0.8f, 0.3f);
    }
}
