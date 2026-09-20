using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 핸드폰 UI (M6) - 실제 내비게이션처럼 미니맵을 보여주고, 새 배달 요청을 수락하는 화면
// 플레이어 머리 위에서 내려다보는 카메라를 하나 더 만들어서 그 화면을 폰 화면에 그대로 띄움
// 플레이어가 도는 방향에 맞춰 미니맵 카메라도 같이 돌아서 "항상 위쪽 = 내가 보는 방향"이 되게 함
// 배달 마커(기둥)는 위에서 보면 자연스럽게 목적지 점(핀)처럼 보임
public class PhoneUI : MonoBehaviour
{
    private Text statusText;
    private Text timeText;
    private Text moneyText;
    private GameObject acceptButtonGO;

    private Transform player;
    private Camera minimapCamera;
    private RenderTexture minimapTexture;

    void Start()
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            player = playerGO.transform;
        }

        EnsureEventSystem();
        BuildUI();
    }

    void EnsureEventSystem()
    {
        // 버튼 클릭이 동작하려면 씬에 EventSystem이 하나 있어야 함
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }
    }

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("PhoneCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // 폰 몸체
        GameObject bodyGO = new GameObject("PhoneBody");
        bodyGO.transform.SetParent(canvasGO.transform, false);
        Image body = bodyGO.AddComponent<Image>();
        body.color = new Color(0.08f, 0.08f, 0.1f, 0.97f);

        RectTransform bodyRect = bodyGO.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(1f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 0f);
        bodyRect.pivot = new Vector2(1f, 0f);
        bodyRect.anchoredPosition = new Vector2(-20f, 20f);
        bodyRect.sizeDelta = new Vector2(180f, 340f);

        // 상단 스피커 장식
        GameObject speakerGO = new GameObject("Speaker");
        speakerGO.transform.SetParent(bodyGO.transform, false);
        Image speaker = speakerGO.AddComponent<Image>();
        speaker.color = new Color(0.25f, 0.25f, 0.28f, 1f);
        RectTransform speakerRect = speakerGO.GetComponent<RectTransform>();
        speakerRect.anchorMin = new Vector2(0.5f, 1f);
        speakerRect.anchorMax = new Vector2(0.5f, 1f);
        speakerRect.pivot = new Vector2(0.5f, 1f);
        speakerRect.anchoredPosition = new Vector2(0f, -10f);
        speakerRect.sizeDelta = new Vector2(45f, 5f);

        // 화면
        GameObject screenGO = new GameObject("Screen");
        screenGO.transform.SetParent(bodyGO.transform, false);
        Image screen = screenGO.AddComponent<Image>();
        screen.color = new Color(0.02f, 0.05f, 0.09f, 1f);
        RectTransform screenRect = screenGO.GetComponent<RectTransform>();
        screenRect.anchorMin = new Vector2(0.5f, 0.5f);
        screenRect.anchorMax = new Vector2(0.5f, 0.5f);
        screenRect.pivot = new Vector2(0.5f, 0.5f);
        screenRect.anchoredPosition = new Vector2(0f, -5f);
        screenRect.sizeDelta = new Vector2(160f, 280f);

        // 하단 홈버튼 장식
        GameObject homeGO = new GameObject("HomeButton");
        homeGO.transform.SetParent(bodyGO.transform, false);
        Image home = homeGO.AddComponent<Image>();
        home.color = new Color(0.25f, 0.25f, 0.28f, 1f);
        RectTransform homeRect = homeGO.GetComponent<RectTransform>();
        homeRect.anchorMin = new Vector2(0.5f, 0f);
        homeRect.anchorMax = new Vector2(0.5f, 0f);
        homeRect.pivot = new Vector2(0.5f, 0f);
        homeRect.anchoredPosition = new Vector2(0f, 10f);
        homeRect.sizeDelta = new Vector2(24f, 24f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        BuildMinimap(screenGO.transform, font);

        statusText = CreateLabel(screenGO.transform, font, "-", -25f, 14);
        timeText = CreateLabel(screenGO.transform, font, "남은 시간: -", -55f, 14);
        moneyText = CreateLabel(screenGO.transform, font, "보유 금액: 0원", -85f, 14);

        BuildAcceptButton(screenGO.transform, font);
    }

    void BuildMinimap(Transform parent, Font font)
    {
        GameObject mapGO = new GameObject("MinimapImage");
        mapGO.transform.SetParent(parent, false);
        RawImage rawImage = mapGO.AddComponent<RawImage>();

        RectTransform mapRect = mapGO.GetComponent<RectTransform>();
        mapRect.anchorMin = new Vector2(0.5f, 0.5f);
        mapRect.anchorMax = new Vector2(0.5f, 0.5f);
        mapRect.pivot = new Vector2(0.5f, 0.5f);
        mapRect.anchoredPosition = new Vector2(0f, 60f);
        mapRect.sizeDelta = new Vector2(140f, 140f);

        // 미니맵 카메라 - 플레이어 머리 위에서 내려다보고, 그 화면을 렌더텍스처로 뽑아서 위 RawImage에 표시
        GameObject camGO = new GameObject("MinimapCamera");
        minimapCamera = camGO.AddComponent<Camera>();
        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = 35f;
        minimapCamera.nearClipPlane = 1f;
        minimapCamera.farClipPlane = 120f;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.05f, 0.16f, 0.09f);

        minimapTexture = new RenderTexture(256, 256, 16);
        minimapCamera.targetTexture = minimapTexture;
        rawImage.texture = minimapTexture;

        // 지도 위에 고정으로 떠 있는 "나(플레이어)" 아이콘 - 지도 자체가 플레이어 방향에 맞춰 돌기 때문에 항상 위쪽을 향함
        GameObject iconGO = new GameObject("PlayerIcon");
        iconGO.transform.SetParent(mapGO.transform, false);
        Text iconText = iconGO.AddComponent<Text>();
        iconText.font = font;
        iconText.fontSize = 22;
        iconText.color = Color.white;
        iconText.text = "▲";
        iconText.alignment = TextAnchor.MiddleCenter;

        RectTransform iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(30f, 30f);
    }

    void BuildAcceptButton(Transform parent, Font font)
    {
        acceptButtonGO = new GameObject("AcceptButton");
        acceptButtonGO.transform.SetParent(parent, false);

        Image btnImage = acceptButtonGO.AddComponent<Image>();
        btnImage.color = new Color(0.25f, 0.85f, 0.35f);

        Button button = acceptButtonGO.AddComponent<Button>();
        button.onClick.AddListener(() =>
        {
            if (DeliveryJobManager.Instance != null)
            {
                DeliveryJobManager.Instance.AcceptJob();
            }
        });

        RectTransform btnRect = acceptButtonGO.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = new Vector2(0f, -115f);
        btnRect.sizeDelta = new Vector2(100f, 36f);

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(acceptButtonGO.transform, false);
        Text label = labelGO.AddComponent<Text>();
        label.font = font;
        label.fontSize = 18;
        label.color = Color.black;
        label.text = "수락";
        label.alignment = TextAnchor.MiddleCenter;

        RectTransform labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    Text CreateLabel(Transform parent, Font font, string initialText, float y, int fontSize)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);

        Text text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.text = initialText;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-16f, 26f);

        return text;
    }

    void Update()
    {
        DeliveryJobManager manager = DeliveryJobManager.Instance;
        UpdateStatus(manager);
        UpdateTime();
        UpdateMoney();
        UpdateAcceptButton(manager);
        UpdateMinimap();
    }

    void UpdateStatus(DeliveryJobManager manager)
    {
        if (statusText == null)
        {
            return;
        }

        if (manager == null)
        {
            statusText.text = "-";
            return;
        }

        switch (manager.State)
        {
            case DeliveryJobManager.JobState.Proposed:
                statusText.text = "새 배달 요청 도착!";
                break;
            case DeliveryJobManager.JobState.GoingToPickup:
                statusText.text = "픽업 장소: " + (manager.CurrentPickupSpot != null ? manager.CurrentPickupSpot.name : "-");
                break;
            case DeliveryJobManager.JobState.GoingToDelivery:
                statusText.text = "배달지: " + (manager.CurrentDeliverySpot != null ? manager.CurrentDeliverySpot.name : "-");
                break;
        }
    }

    void UpdateTime()
    {
        if (timeText == null)
        {
            return;
        }

        if (DeliveryManager.HasFood)
        {
            timeText.text = "남은 시간: " + Mathf.CeilToInt(DeliveryManager.RemainingTime) + "초";
        }
        else
        {
            timeText.text = "남은 시간: -";
        }
    }

    void UpdateMoney()
    {
        if (moneyText == null)
        {
            return;
        }

        moneyText.text = "보유 금액: " + DeliveryManager.Money + "원";
    }

    void UpdateAcceptButton(DeliveryJobManager manager)
    {
        if (acceptButtonGO == null)
        {
            return;
        }

        bool shouldShow = manager != null && manager.State == DeliveryJobManager.JobState.Proposed;
        if (acceptButtonGO.activeSelf != shouldShow)
        {
            acceptButtonGO.SetActive(shouldShow);
        }
    }

    void UpdateMinimap()
    {
        if (minimapCamera == null || player == null)
        {
            return;
        }

        // 플레이어 머리 위에서 수직으로 내려다보고, 플레이어가 도는 방향에 맞춰 지도도 같이 회전
        Vector3 pos = player.position;
        pos.y += 60f;
        minimapCamera.transform.position = pos;
        minimapCamera.transform.rotation = Quaternion.Euler(90f, player.eulerAngles.y, 0f);
    }
}
