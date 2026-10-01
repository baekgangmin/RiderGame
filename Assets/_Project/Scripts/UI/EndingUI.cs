using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 엔딩 화면 (M? - 게임 마무리) - 정해둔 날짜(DayNightCycle.totalDays)가 끝나면 EndingManager가 이걸 띄움
// GasStationUI와 같은 패턴(코드로 직접 Canvas/패널 생성)으로 만듦
public class EndingUI : MonoBehaviour
{
    public static EndingUI Instance;

    private GameObject dim;
    private Text infoText;

    void Awake()
    {
        Instance = this;
        BuildUI();
    }

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("EndingCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // 다른 UI(핸드폰, 주유소 결제창)보다도 항상 위에 뜨도록
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        dim = new GameObject("Dim");
        dim.transform.SetParent(canvasGO.transform, false);
        Image dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.75f);
        RectTransform dimRect = dim.GetComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        GameObject panel = new GameObject("EndingPanel");
        panel.transform.SetParent(dim.transform, false);
        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.07f, 0.09f, 0.97f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(460f, 380f);
        panelRect.anchoredPosition = Vector2.zero;

        Text title = CreateLabel(panel.transform, "EndingTitle", "🏁 일주일간의 배달 완료!", 24, new Vector2(0f, 150f), new Vector2(420f, 40f));
        title.fontStyle = FontStyle.Bold;

        infoText = CreateLabel(panel.transform, "EndingInfo", "", 18, new Vector2(0f, 10f), new Vector2(420f, 220f));
        infoText.alignment = TextAnchor.UpperCenter;

        CreateButton(panel.transform, "RestartButton", "다시 시작", new Vector2(0f, -150f), new Vector2(200f, 50f), OnRestartClicked, new Color(0.2f, 0.55f, 0.3f));

        dim.SetActive(false);
    }

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

        CreateLabel(go.transform, name + "Label", label, 18, Vector2.zero, size);

        return btn;
    }

    // EndingManager에서 호출 - 최종 통계를 보여주고 게임을 멈춤
    public void Show(int days, int deliveryCount, int totalEarned, int finalMoney)
    {
        infoText.text =
            days + "일 동안 수고했어요!\n\n" +
            "완료한 배달: " + deliveryCount + "건\n" +
            "벌어들인 돈: 💰" + totalEarned + "원\n" +
            "최종 보유 금액: 💰" + finalMoney + "원";

        dim.SetActive(true);
    }

    private void OnRestartClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
