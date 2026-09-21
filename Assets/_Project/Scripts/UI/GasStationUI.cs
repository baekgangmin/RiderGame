using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 주유소 결제 창 (M9) - 주유소 구역 안에서 스페이스바를 누르면 뜨는 UI
// 필요한 연료량 / 결제 금액 / 예상 소요 시간을 보여주고, 결제하면 GasStation이 그 시간 동안 서서히 연료를 채우는 동안
// 남은 시간을 계속 갱신해서 보여주다가 완료되면 "주유 완료!" 문구를 띄움
public class GasStationUI : MonoBehaviour
{
    public static GasStationUI Instance;

    private GameObject panel;
    private Text infoText;
    private Text statusText;
    private Button payButton;
    private Button closeButton;

    private string pendingVehicleId;
    private float pendingAmount;
    private int pendingCost;
    private float pendingDuration;
    private bool isOpen;

    void Awake()
    {
        Instance = this;
        BuildUI();
    }

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("GasStationCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // 핸드폰 UI 등 다른 UI보다 항상 위에 뜨도록
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // 버튼 클릭을 받으려면 EventSystem이 필요함 - 씬에 아직 없으면 여기서 만들어둠
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        // 어두운 배경 딤 - 패널 밖 클릭 방지 겸 집중도를 높임
        GameObject dim = new GameObject("Dim");
        dim.transform.SetParent(canvasGO.transform, false);
        Image dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.4f);
        RectTransform dimRect = dim.GetComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        panel = new GameObject("GasStationPanel");
        panel.transform.SetParent(dim.transform, false);
        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.07f, 0.09f, 0.95f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(420f, 300f);
        panelRect.anchoredPosition = Vector2.zero;

        Text title = CreateLabel(panel.transform, "GasStationTitle", "⛽ 주유소", 22, new Vector2(0f, 115f), new Vector2(380f, 36f));
        title.fontStyle = FontStyle.Bold;

        infoText = CreateLabel(panel.transform, "GasStationInfo", "", 17, new Vector2(0f, 25f), new Vector2(380f, 140f));
        infoText.alignment = TextAnchor.UpperCenter;

        statusText = CreateLabel(panel.transform, "GasStationStatus", "", 16, new Vector2(0f, -45f), new Vector2(380f, 40f));
        statusText.color = new Color(1f, 0.85f, 0.3f);

        payButton = CreateButton(panel.transform, "PayButton", "결제하기", new Vector2(-95f, -115f), new Vector2(170f, 46f), OnPayClicked, new Color(0.2f, 0.55f, 0.3f));
        closeButton = CreateButton(panel.transform, "CloseButton", "닫기", new Vector2(95f, -115f), new Vector2(170f, 46f), OnCloseClicked, new Color(0.35f, 0.35f, 0.38f));

        dim.SetActive(false);
    }

    // ---- UI 생성 헬퍼 ----
    private Text CreateLabel(Transform parent, string name, string text, int fontSize, Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = text;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        return t;
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        CreateLabel(go.transform, name + "Label", label, 16, Vector2.zero, size);

        return btn;
    }

    // GasStation에서 호출 - 결제 창을 엶
    public void Open(VehicleCatalog.VehicleDefinition def, float missingAmount, int cost, float duration)
    {
        pendingVehicleId = def.id;
        pendingAmount = missingAmount;
        pendingCost = cost;
        pendingDuration = duration;
        isOpen = true;

        float missingPercent = def.maxFuel > 0f ? (missingAmount / def.maxFuel) * 100f : 0f;
        infoText.text =
            def.displayName + " 연료 보충\n\n" +
            "필요한 연료: " + missingPercent.ToString("F0") + "%\n" +
            "결제 금액: 💰" + cost + "원\n" +
            "예상 소요 시간: " + duration.ToString("F1") + "초";
        statusText.color = new Color(1f, 0.85f, 0.3f);
        statusText.text = "";

        payButton.gameObject.SetActive(true);
        closeButton.gameObject.SetActive(true);
        panel.transform.parent.gameObject.SetActive(true); // Dim 오브젝트
    }

    private void OnPayClicked()
    {
        if (GasStation.Instance == null)
        {
            return;
        }

        payButton.gameObject.SetActive(false);
        statusText.color = new Color(1f, 0.85f, 0.3f);
        statusText.text = "⛽ 주유 시작...";

        GasStation.Instance.StartRefuel(pendingVehicleId, pendingAmount, pendingCost, pendingDuration);
    }

    // GasStation의 주유 코루틴에서 매 프레임 호출 - 남은 시간을 표시
    public void UpdateRefuelProgress(float remainingSeconds)
    {
        if (!isOpen)
        {
            return;
        }
        statusText.text = "⛽ 주유 중... 남은 시간: " + Mathf.Max(0f, remainingSeconds).ToString("F1") + "초";
    }

    // 주유 완료 시 GasStation에서 호출
    public void ShowComplete()
    {
        if (!isOpen)
        {
            return;
        }
        statusText.color = new Color(0.3f, 0.9f, 0.4f);
        statusText.text = "✅ 주유 완료!";
        StartCoroutine(AutoCloseAfter(1.6f));
    }

    // 돈이 부족한 경우 등 GasStation에서 즉시 알려줄 때 사용
    public void ShowMessage(string message)
    {
        statusText.color = new Color(0.95f, 0.3f, 0.25f);
        statusText.text = message;
        payButton.gameObject.SetActive(true);
    }

    private IEnumerator AutoCloseAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Close();
    }

    private void OnCloseClicked()
    {
        Close();
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }
        isOpen = false;
        if (panel != null)
        {
            panel.transform.parent.gameObject.SetActive(false); // Dim 오브젝트
        }
    }
}
