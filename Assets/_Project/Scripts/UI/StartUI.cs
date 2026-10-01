using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 게임 시작 화면 - Play를 누르면(또는 엔딩 후 "다시 시작"으로 씬을 다시 불러오면) 바로 조작되지 않게
// 시간을 멈춰두고 "배달 시작" 버튼을 띄움. 누르면 시간이 풀리고 DayNightCycle의 날짜 계산이 시작됨.
// 게임이 시작된 뒤에는 화면 위쪽에 작게 "Day N/7" 표시를 계속 띄워줌(2. 날짜 표시 요구사항)
public class StartUI : MonoBehaviour
{
    private GameObject dim;
    private Text dayLabel;
    private DayNightCycle dayNightCycle;

    void Awake()
    {
        dayNightCycle = FindObjectOfType<DayNightCycle>();

        BuildUI();

        // 시작 버튼을 누르기 전까지는 플레이어 조작/배달 타이머/탈것 연료 소모 등 전부 멈춰있어야 함
        Time.timeScale = 0f;
    }

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("StartCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // 엔딩(100)보다는 아래, 폰/주유소 UI보다는 위
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        dim = new GameObject("StartDim");
        dim.transform.SetParent(canvasGO.transform, false);
        Image dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.8f);
        RectTransform dimRect = dim.GetComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        Text title = CreateLabel(dim.transform, "StartTitle", "🛵 라이더 알바", 30, new Vector2(0f, 60f), new Vector2(500f, 50f));
        title.fontStyle = FontStyle.Bold;

        Text subtitle = CreateLabel(dim.transform, "StartSubtitle", "일주일(" + (dayNightCycle != null ? dayNightCycle.totalDays : 7) + "일) 동안 배달하고 최대한 많이 벌어보세요!", 16, new Vector2(0f, 10f), new Vector2(500f, 40f));
        subtitle.color = new Color(0.85f, 0.85f, 0.85f);

        CreateButton(dim.transform, "StartButton", "배달 시작", new Vector2(0f, -70f), new Vector2(220f, 56f), OnStartClicked, new Color(0.2f, 0.55f, 0.3f));

        // 상단 중앙에 작게 떠 있는 날짜 표시 - 시작 전엔 숨겨둠
        GameObject dayGO = new GameObject("DayLabel");
        dayGO.transform.SetParent(canvasGO.transform, false);
        dayLabel = dayGO.AddComponent<Text>();
        dayLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        dayLabel.fontSize = 18;
        dayLabel.fontStyle = FontStyle.Bold;
        dayLabel.alignment = TextAnchor.MiddleCenter;
        dayLabel.color = Color.white;
        RectTransform dayRect = dayGO.GetComponent<RectTransform>();
        dayRect.anchorMin = new Vector2(0.5f, 1f);
        dayRect.anchorMax = new Vector2(0.5f, 1f);
        dayRect.pivot = new Vector2(0.5f, 1f);
        dayRect.anchoredPosition = new Vector2(0f, -16f);
        dayRect.sizeDelta = new Vector2(160f, 32f);
        dayGO.SetActive(false);
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

    private void OnStartClicked()
    {
        dim.SetActive(false);
        Time.timeScale = 1f;

        if (dayNightCycle != null)
        {
            dayNightCycle.BeginDay();
        }

        dayLabel.gameObject.SetActive(true);
    }

    void Update()
    {
        if (dayNightCycle == null || !dayLabel.gameObject.activeSelf)
        {
            return;
        }

        int shownDay = Mathf.Min(dayNightCycle.CurrentDay + 1, dayNightCycle.totalDays);
        dayLabel.text = "Day " + shownDay + " / " + dayNightCycle.totalDays;
    }
}
