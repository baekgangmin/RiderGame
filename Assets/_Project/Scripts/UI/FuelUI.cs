using UnityEngine;
using UnityEngine.UI;

// 연료 게이지 (M9) - 좌하단 체력바 바로 위에 표시. 오토바이/자동차처럼 연료가 필요한 탈것을 타고 있을 때만 보이고
// 자전거/킥보드를 타거나 걸어다닐 때, 또는 아무것도 안 탔을 때는 자동으로 숨겨짐
public class FuelUI : MonoBehaviour
{
    private GameObject barGO;
    private Image fillImage;
    private RectTransform fillRect;

    void Start()
    {
        BuildUI();
    }

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("FuelCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        barGO = new GameObject("FuelBarBG");
        barGO.transform.SetParent(canvasGO.transform, false);
        Image bg = barGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.5f);
        RectTransform bgRect = barGO.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0f);
        bgRect.anchorMax = new Vector2(0f, 0f);
        bgRect.pivot = new Vector2(0f, 0f);
        bgRect.anchoredPosition = new Vector2(30f, 58f); // 체력바(y=30, 높이 22) 바로 위
        bgRect.sizeDelta = new Vector2(190f, 18f);

        GameObject fillGO = new GameObject("FuelBarFill");
        fillGO.transform.SetParent(barGO.transform, false);
        fillImage = fillGO.AddComponent<Image>();
        fillImage.color = new Color(1f, 0.75f, 0.15f);

        fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        barGO.SetActive(false);
    }

    void Update()
    {
        if (barGO == null)
        {
            return;
        }

        bool mounted = VehicleMount.Instance != null && VehicleMount.Instance.IsMounted;
        VehicleCatalog.VehicleDefinition def = mounted ? VehicleCatalog.GetActive() : null;
        bool shouldShow = def != null && def.needsFuel;

        if (barGO.activeSelf != shouldShow)
        {
            barGO.SetActive(shouldShow);
        }

        if (!shouldShow || fillRect == null)
        {
            return;
        }

        float percent = Mathf.Clamp01(VehicleCatalog.GetFuelPercent(def.id));
        fillRect.anchorMax = new Vector2(percent, 1f);
        fillImage.color = percent <= 0.2f ? new Color(0.9f, 0.25f, 0.2f) : new Color(1f, 0.75f, 0.15f);
    }
}
