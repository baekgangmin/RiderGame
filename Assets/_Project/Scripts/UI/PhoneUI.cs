using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 핸드폰 UI (M6~M8) - 지도/상점/탈것/콜받기 4개의 "앱"으로 나뉜 폰 화면
// 지도, 콜받기는 우하단의 작은 폰 화면 안에서 바로 조작하고,
// 상점, 탈것은 화면 가운데에 좀 더 크게 띄워서 조작함(항목이 많고 버튼이 작으면 누르기 힘들어서)
public class PhoneUI : MonoBehaviour
{
    private enum AppTab { Map, Shop, Vehicle, Call }

    [Header("색상")]
    public Color dockNormalColor = new Color(0.2f, 0.2f, 0.24f);
    public Color dockActiveColor = new Color(0.3f, 0.55f, 0.95f);
    public Color playerIconColor = new Color(1f, 0.35f, 0f); // 흰색이라 안 보인다고 해서 눈에 띄는 주황색으로 변경

    private AppTab currentTab = AppTab.Map;

    private Transform player;
    private Font font;

    // ---- 작은 폰(우하단) ----
    private Text topBarText;
    private GameObject mapPanel;
    private GameObject callPanel;

    private Camera minimapCamera;
    private RenderTexture minimapTexture;

    private Text callStatusText;
    private Text callInfoText;
    private GameObject callTimerBarGO;
    private Image callTimerFillImage;
    private GameObject acceptButtonGO;

    private GameObject[] dockButtons = new GameObject[4]; // Map, Shop, Vehicle, Call 순서
    private GameObject callBadgeGO;

    // ---- 큰 오버레이(화면 중앙, 상점/탈것용) ----
    private GameObject overlayRootGO;
    private GameObject overlayShopContent;
    private GameObject overlayVehicleContent;
    private Text overlayTitleText;

    private class ShopVehicleRow
    {
        public VehicleCatalog.VehicleDefinition def;
        public Text statusText;
        public GameObject buyButtonGO;
    }
    private readonly List<ShopVehicleRow> shopVehicleRows = new List<ShopVehicleRow>();

    private class OwnedVehicleRow
    {
        public string id;
        public Text statusText;
        public GameObject actionButtonGO;
        public Text actionButtonLabel;
    }
    private readonly List<OwnedVehicleRow> ownedVehicleRows = new List<OwnedVehicleRow>();

    void Start()
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            player = playerGO.transform;
        }

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        EnsureEventSystem();
        BuildUI();
        BuildOverlay();
        SelectTab(AppTab.Map);
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

    private Canvas rootCanvas;

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("PhoneCanvas");
        rootCanvas = canvasGO.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // 폰 몸체 (예전보다 살짝 키움)
        GameObject bodyGO = new GameObject("PhoneBody");
        bodyGO.transform.SetParent(canvasGO.transform, false);
        Image body = bodyGO.AddComponent<Image>();
        body.color = new Color(0.08f, 0.08f, 0.1f, 0.97f);

        RectTransform bodyRect = bodyGO.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(1f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 0f);
        bodyRect.pivot = new Vector2(1f, 0f);
        bodyRect.anchoredPosition = new Vector2(-20f, 20f);
        bodyRect.sizeDelta = new Vector2(210f, 400f);

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
        speakerRect.sizeDelta = new Vector2(50f, 5f);

        // 화면
        GameObject screenGO = new GameObject("Screen");
        screenGO.transform.SetParent(bodyGO.transform, false);
        Image screen = screenGO.AddComponent<Image>();
        screen.color = new Color(0.02f, 0.05f, 0.09f, 1f);
        RectTransform screenRect = screenGO.GetComponent<RectTransform>();
        screenRect.anchorMin = new Vector2(0.5f, 0.5f);
        screenRect.anchorMax = new Vector2(0.5f, 0.5f);
        screenRect.pivot = new Vector2(0.5f, 0.5f);
        screenRect.anchoredPosition = new Vector2(0f, -8f);
        screenRect.sizeDelta = new Vector2(185f, 340f);

        // 하단 홈버튼 장식
        GameObject homeGO = new GameObject("HomeButton");
        homeGO.transform.SetParent(bodyGO.transform, false);
        Image home = homeGO.AddComponent<Image>();
        home.color = new Color(0.25f, 0.25f, 0.28f, 1f);
        RectTransform homeRect = homeGO.GetComponent<RectTransform>();
        homeRect.anchorMin = new Vector2(0.5f, 0f);
        homeRect.anchorMax = new Vector2(0.5f, 0f);
        homeRect.pivot = new Vector2(0.5f, 0f);
        homeRect.anchoredPosition = new Vector2(0f, 12f);
        homeRect.sizeDelta = new Vector2(26f, 26f);

        // --- 화면 내부를 상태바 / 콘텐츠 / 하단 독(앱 아이콘)으로 3분할 ---

        GameObject statusBarGO = new GameObject("StatusBar");
        statusBarGO.transform.SetParent(screenGO.transform, false);
        RectTransform statusBarRect = statusBarGO.AddComponent<RectTransform>();
        statusBarRect.anchorMin = new Vector2(0f, 1f);
        statusBarRect.anchorMax = new Vector2(1f, 1f);
        statusBarRect.offsetMin = new Vector2(3f, -20f);
        statusBarRect.offsetMax = new Vector2(-3f, 0f);

        topBarText = statusBarGO.AddComponent<Text>();
        topBarText.font = font;
        topBarText.fontSize = 12;
        topBarText.color = new Color(1f, 0.85f, 0.3f);
        topBarText.alignment = TextAnchor.MiddleCenter;
        topBarText.text = "보유 금액: 0원";

        GameObject dockGO = new GameObject("Dock");
        dockGO.transform.SetParent(screenGO.transform, false);
        RectTransform dockRect = dockGO.AddComponent<RectTransform>();
        dockRect.anchorMin = new Vector2(0f, 0f);
        dockRect.anchorMax = new Vector2(1f, 0f);
        dockRect.offsetMin = new Vector2(3f, 4f);
        dockRect.offsetMax = new Vector2(-3f, 42f);

        BuildDock(dockGO.transform);

        GameObject contentAreaGO = new GameObject("ContentArea");
        contentAreaGO.transform.SetParent(screenGO.transform, false);
        RectTransform contentRect = contentAreaGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.offsetMin = new Vector2(3f, 42f);
        contentRect.offsetMax = new Vector2(-3f, -22f);

        mapPanel = new GameObject("MapPanel");
        mapPanel.transform.SetParent(contentAreaGO.transform, false);
        RectTransform mapPanelRect = mapPanel.AddComponent<RectTransform>();
        mapPanelRect.anchorMin = Vector2.zero;
        mapPanelRect.anchorMax = Vector2.one;
        mapPanelRect.offsetMin = Vector2.zero;
        mapPanelRect.offsetMax = Vector2.zero;
        BuildMinimap(mapPanel.transform);

        callPanel = new GameObject("CallPanel");
        callPanel.transform.SetParent(contentAreaGO.transform, false);
        RectTransform callPanelRect = callPanel.AddComponent<RectTransform>();
        callPanelRect.anchorMin = Vector2.zero;
        callPanelRect.anchorMax = Vector2.one;
        callPanelRect.offsetMin = Vector2.zero;
        callPanelRect.offsetMax = Vector2.zero;
        BuildCallPanel(callPanel.transform);
    }

    void BuildDock(Transform parent)
    {
        string[] labels = { "지도", "상점", "탈것", "콜받기" };
        float[] xs = { -67.5f, -22.5f, 22.5f, 67.5f };

        for (int i = 0; i < 4; i++)
        {
            AppTab tabForButton = (AppTab)i;
            GameObject btn = CreateButton(parent, "Dock_" + labels[i], labels[i], 11,
                dockNormalColor, Color.white,
                new Vector2(xs[i], 0f), new Vector2(38f, 32f),
                () => SelectTab(tabForButton));

            dockButtons[i] = btn;

            if (tabForButton == AppTab.Call)
            {
                GameObject badge = new GameObject("CallBadge");
                badge.transform.SetParent(btn.transform, false);
                Image badgeImg = badge.AddComponent<Image>();
                badgeImg.color = new Color(0.95f, 0.2f, 0.2f);
                RectTransform badgeRect = badge.GetComponent<RectTransform>();
                badgeRect.anchorMin = new Vector2(1f, 1f);
                badgeRect.anchorMax = new Vector2(1f, 1f);
                badgeRect.pivot = new Vector2(0.5f, 0.5f);
                badgeRect.anchoredPosition = new Vector2(-2f, -2f);
                badgeRect.sizeDelta = new Vector2(9f, 9f);
                callBadgeGO = badge;
            }
        }
    }

    void BuildMinimap(Transform parent)
    {
        GameObject mapGO = new GameObject("MinimapImage");
        mapGO.transform.SetParent(parent, false);
        RawImage rawImage = mapGO.AddComponent<RawImage>();

        RectTransform mapRect = mapGO.GetComponent<RectTransform>();
        mapRect.anchorMin = new Vector2(0.5f, 0.5f);
        mapRect.anchorMax = new Vector2(0.5f, 0.5f);
        mapRect.pivot = new Vector2(0.5f, 0.5f);
        mapRect.anchoredPosition = new Vector2(0f, 0f);
        mapRect.sizeDelta = new Vector2(160f, 160f);

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
        // 기존엔 흰색이라 안 보인다고 해서 눈에 띄는 주황색 + 검은 테두리로 변경
        GameObject iconGO = new GameObject("PlayerIcon");
        iconGO.transform.SetParent(mapGO.transform, false);
        Text iconText = iconGO.AddComponent<Text>();
        iconText.font = font;
        iconText.fontSize = 24;
        iconText.color = playerIconColor;
        iconText.text = "▲";
        iconText.alignment = TextAnchor.MiddleCenter;
        iconText.fontStyle = FontStyle.Bold;

        Outline outline = iconGO.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        RectTransform iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(30f, 30f);
    }

    void BuildCallPanel(Transform parent)
    {
        callStatusText = CreateLabel(parent, "콜 대기 중...", 100f, 15);
        callInfoText = CreateLabel(parent, "", 55f, 13);

        // 콜 수락 제한시간 카운트다운 바
        callTimerBarGO = new GameObject("CallTimerBG");
        callTimerBarGO.transform.SetParent(parent, false);
        Image timerBg = callTimerBarGO.AddComponent<Image>();
        timerBg.color = new Color(0f, 0f, 0f, 0.5f);
        RectTransform timerBgRect = callTimerBarGO.GetComponent<RectTransform>();
        timerBgRect.anchorMin = new Vector2(0.5f, 0.5f);
        timerBgRect.anchorMax = new Vector2(0.5f, 0.5f);
        timerBgRect.pivot = new Vector2(0.5f, 0.5f);
        timerBgRect.anchoredPosition = new Vector2(0f, 15f);
        timerBgRect.sizeDelta = new Vector2(150f, 12f);

        GameObject timerFillGO = new GameObject("CallTimerFill");
        timerFillGO.transform.SetParent(callTimerBarGO.transform, false);
        callTimerFillImage = timerFillGO.AddComponent<Image>();
        callTimerFillImage.color = new Color(1f, 0.75f, 0.15f);
        RectTransform timerFillRect = timerFillGO.GetComponent<RectTransform>();
        timerFillRect.anchorMin = new Vector2(0f, 0f);
        timerFillRect.anchorMax = new Vector2(1f, 1f);
        timerFillRect.pivot = new Vector2(0f, 0.5f);
        timerFillRect.offsetMin = new Vector2(1f, 1f);
        timerFillRect.offsetMax = new Vector2(-1f, -1f);

        acceptButtonGO = CreateButton(parent, "AcceptButton", "수락", 18,
            new Color(0.25f, 0.85f, 0.35f), Color.black,
            new Vector2(0f, -40f), new Vector2(110f, 38f),
            () =>
            {
                if (DeliveryJobManager.Instance != null)
                {
                    DeliveryJobManager.Instance.AcceptJob();
                }
            });
    }

    void BuildOverlay()
    {
        overlayRootGO = new GameObject("OverlayRoot");
        overlayRootGO.transform.SetParent(rootCanvas.transform, false);
        RectTransform overlayRootRect = overlayRootGO.AddComponent<RectTransform>();
        overlayRootRect.anchorMin = Vector2.zero;
        overlayRootRect.anchorMax = Vector2.one;
        overlayRootRect.offsetMin = Vector2.zero;
        overlayRootRect.offsetMax = Vector2.zero;

        // 어두운 배경 - 클릭하면 오버레이 닫힘 (밖을 누르면 닫히는 흔한 모달 패턴)
        GameObject backdropGO = new GameObject("Backdrop");
        backdropGO.transform.SetParent(overlayRootGO.transform, false);
        Image backdropImg = backdropGO.AddComponent<Image>();
        backdropImg.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform backdropRect = backdropGO.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        Button backdropBtn = backdropGO.AddComponent<Button>();
        backdropBtn.transition = Selectable.Transition.None;
        backdropBtn.onClick.AddListener(() => SelectTab(AppTab.Map));

        // 가운데 큰 패널
        GameObject panelGO = new GameObject("OverlayPanel");
        panelGO.transform.SetParent(overlayRootGO.transform, false);
        Image panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.1f, 0.1f, 0.13f, 0.98f);
        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(480f, 420f);

        overlayTitleText = CreateLabel(panelGO.transform, "상점", 180f, 22);
        overlayTitleText.fontStyle = FontStyle.Bold;

        CreateButton(panelGO.transform, "CloseButton", "X", 16,
            new Color(0.5f, 0.15f, 0.15f), Color.white,
            new Vector2(215f, 180f), new Vector2(32f, 32f),
            () => SelectTab(AppTab.Map));

        overlayShopContent = new GameObject("ShopContent");
        overlayShopContent.transform.SetParent(panelGO.transform, false);
        RectTransform shopRect = overlayShopContent.AddComponent<RectTransform>();
        shopRect.anchorMin = new Vector2(0.5f, 0.5f);
        shopRect.anchorMax = new Vector2(0.5f, 0.5f);
        shopRect.pivot = new Vector2(0.5f, 0.5f);
        shopRect.anchoredPosition = new Vector2(0f, -20f);
        shopRect.sizeDelta = new Vector2(440f, 320f);
        BuildOverlayShopContent(overlayShopContent.transform);

        overlayVehicleContent = new GameObject("VehicleContent");
        overlayVehicleContent.transform.SetParent(panelGO.transform, false);
        RectTransform vehicleRect = overlayVehicleContent.AddComponent<RectTransform>();
        vehicleRect.anchorMin = new Vector2(0.5f, 0.5f);
        vehicleRect.anchorMax = new Vector2(0.5f, 0.5f);
        vehicleRect.pivot = new Vector2(0.5f, 0.5f);
        vehicleRect.anchoredPosition = new Vector2(0f, -20f);
        vehicleRect.sizeDelta = new Vector2(440f, 320f);
        BuildOverlayVehicleContent(overlayVehicleContent.transform);

        overlayRootGO.SetActive(false);
    }

    void BuildOverlayShopContent(Transform parent)
    {
        CreateLabel(parent, "🚲 탈것", 145f, 15).alignment = TextAnchor.MiddleLeft;

        // 탈것 종류가 늘어날수록 목록이 길어지므로, 아래 음료 섹션 위치를 고정값 대신
        // 이 목록이 끝나는 지점을 기준으로 계산해서 배치함 (안 그러면 탈것이 많아질 때 음료 목록과 겹침)
        float vy = 112f;
        const float vehicleRowSpacing = 34f;
        foreach (VehicleCatalog.VehicleDefinition def in VehicleCatalog.All)
        {
            VehicleCatalog.VehicleDefinition captured = def;

            GameObject nameGO = new GameObject("VehicleName_" + captured.id);
            nameGO.transform.SetParent(parent, false);
            Text nameText = nameGO.AddComponent<Text>();
            nameText.font = font;
            nameText.fontSize = 14;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;
            RectTransform nameRect = nameGO.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.5f, 0.5f);
            nameRect.anchorMax = new Vector2(0.5f, 0.5f);
            nameRect.pivot = new Vector2(0.5f, 0.5f);
            nameRect.anchoredPosition = new Vector2(-90f, vy);
            nameRect.sizeDelta = new Vector2(220f, 30f);

            GameObject buyBtn = CreateButton(parent, "Buy_" + captured.id, "구매", 13,
                new Color(0.3f, 0.55f, 0.95f), Color.white,
                new Vector2(150f, vy), new Vector2(90f, 32f),
                () => VehicleCatalog.Buy(captured));

            shopVehicleRows.Add(new ShopVehicleRow { def = captured, statusText = nameText, buyButtonGO = buyBtn });

            vy -= vehicleRowSpacing;
        }

        float drinkLabelY = vy - 6f;
        CreateLabel(parent, "🥤 음료 (스태미나 회복)", drinkLabelY, 15).alignment = TextAnchor.MiddleLeft;

        float dy = drinkLabelY - 38f;
        const float drinkRowSpacing = 42f;
        foreach (VehicleShop.DrinkItem drink in VehicleShop.Drinks)
        {
            VehicleShop.DrinkItem captured = drink;

            GameObject nameGO = new GameObject("DrinkName");
            nameGO.transform.SetParent(parent, false);
            Text nameText = nameGO.AddComponent<Text>();
            nameText.font = font;
            nameText.fontSize = 14;
            nameText.color = Color.white;
            nameText.text = captured.displayName + " - " + captured.price + "원";
            nameText.alignment = TextAnchor.MiddleLeft;
            RectTransform nameRect = nameGO.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.5f, 0.5f);
            nameRect.anchorMax = new Vector2(0.5f, 0.5f);
            nameRect.pivot = new Vector2(0.5f, 0.5f);
            nameRect.anchoredPosition = new Vector2(-90f, dy);
            nameRect.sizeDelta = new Vector2(220f, 30f);

            CreateButton(parent, "Buy_" + captured.id, "구매", 14,
                new Color(0.3f, 0.55f, 0.95f), Color.white,
                new Vector2(150f, dy), new Vector2(90f, 32f),
                () => VehicleShop.BuyDrink(captured));

            dy -= drinkRowSpacing;
        }

        RefreshShopVehicleRows(); // 처음 만들 때 바로 한 번 채워서 탭을 열자마자 빈 텍스트가 보이지 않게 함
    }

    void RefreshShopVehicleRows()
    {
        foreach (ShopVehicleRow row in shopVehicleRows)
        {
            bool owned = VehicleCatalog.IsOwned(row.def.id);

            if (row.statusText != null)
            {
                row.statusText.text = owned ? (row.def.displayName + " - 보유 중") : (row.def.displayName + " - " + row.def.price + "원");
            }

            if (row.buyButtonGO != null && row.buyButtonGO.activeSelf == owned)
            {
                row.buyButtonGO.SetActive(!owned);
            }
        }
    }

    void BuildOverlayVehicleContent(Transform parent)
    {
        CreateLabel(parent, "내가 산 탈것", 145f, 15).alignment = TextAnchor.MiddleLeft;
        CreateLabel(parent, "장비 중인 탈것은 부르기/보관하기, 다른 탈것은 눌러서 갈아탐", -145f, 11);
        // 실제 목록은 탭을 열 때마다 RebuildOwnedVehicleRows()에서 새로 만듦 (상점에서 산 만큼 늘어나므로)
    }

    void RebuildOwnedVehicleRows()
    {
        foreach (OwnedVehicleRow row in ownedVehicleRows)
        {
            if (row.statusText != null)
            {
                Destroy(row.statusText.gameObject);
            }
            if (row.actionButtonGO != null)
            {
                Destroy(row.actionButtonGO);
            }
        }
        ownedVehicleRows.Clear();

        float y = 106f;
        foreach (VehicleCatalog.VehicleDefinition def in VehicleCatalog.All)
        {
            if (!VehicleCatalog.IsOwned(def.id))
            {
                continue;
            }

            string capturedId = def.id;

            GameObject nameGO = new GameObject("OwnedVehicleName_" + capturedId);
            nameGO.transform.SetParent(overlayVehicleContent.transform, false);
            Text nameText = nameGO.AddComponent<Text>();
            nameText.font = font;
            nameText.fontSize = 14;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;
            RectTransform nameRect = nameGO.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.5f, 0.5f);
            nameRect.anchorMax = new Vector2(0.5f, 0.5f);
            nameRect.pivot = new Vector2(0.5f, 0.5f);
            nameRect.anchoredPosition = new Vector2(-90f, y);
            nameRect.sizeDelta = new Vector2(220f, 30f);

            GameObject actionBtn = CreateButton(overlayVehicleContent.transform, "VehicleAction_" + capturedId, "", 13,
                new Color(0.3f, 0.55f, 0.95f), Color.white,
                new Vector2(150f, y), new Vector2(100f, 34f),
                () => OnVehicleRowAction(capturedId));

            ownedVehicleRows.Add(new OwnedVehicleRow
            {
                id = capturedId,
                statusText = nameText,
                actionButtonGO = actionBtn,
                actionButtonLabel = actionBtn.GetComponentInChildren<Text>()
            });

            y -= 42f;
        }
    }

    void OnVehicleRowAction(string id)
    {
        if (VehicleMount.Instance == null)
        {
            return;
        }

        if (VehicleCatalog.ActiveId == id)
        {
            // 이미 장비한 탈것이면 부르기/보관하기 토글
            VehicleMount.Instance.ToggleVisibility();
        }
        else
        {
            // 다른 탈것으로 갈아탐 - 지금 있던 자리/보관 상태는 그대로 두고 모양/속도만 바뀜
            VehicleMount.Instance.SelectVehicle(id);
        }
    }

    // 탭 전환 - 지도/콜받기는 작은 폰 화면 안에서, 상점/탈것은 화면 중앙에 크게 뜸
    void SelectTab(AppTab tab)
    {
        // 상점/탈것 아이콘을 이미 켜진 상태에서 다시 누르면 닫고 지도로 돌아감
        if ((tab == AppTab.Shop || tab == AppTab.Vehicle) && currentTab == tab)
        {
            tab = AppTab.Map;
        }

        currentTab = tab;

        bool showShop = tab == AppTab.Shop;
        bool showVehicle = tab == AppTab.Vehicle;
        bool overlayOpen = showShop || showVehicle;

        if (overlayRootGO != null)
        {
            overlayRootGO.SetActive(overlayOpen);
        }
        if (overlayShopContent != null)
        {
            overlayShopContent.SetActive(showShop);
        }
        if (overlayVehicleContent != null)
        {
            overlayVehicleContent.SetActive(showVehicle);
        }
        if (overlayTitleText != null)
        {
            overlayTitleText.text = showShop ? "상점" : "탈것";
        }

        if (showVehicle)
        {
            // 상점에서 산 만큼 목록이 늘어날 수 있으니 탭을 열 때마다 새로 그림
            RebuildOwnedVehicleRows();
            UpdateVehiclePanel(); // 만들자마자 바로 채워서 빈 텍스트가 잠깐 보이지 않게 함
        }

        if (mapPanel != null)
        {
            mapPanel.SetActive(tab == AppTab.Map);
        }
        if (callPanel != null)
        {
            callPanel.SetActive(tab == AppTab.Call);
        }

        UpdateDockHighlight();
    }

    void UpdateDockHighlight()
    {
        for (int i = 0; i < dockButtons.Length; i++)
        {
            if (dockButtons[i] == null)
            {
                continue;
            }

            Image img = dockButtons[i].GetComponent<Image>();
            if (img != null)
            {
                img.color = ((int)currentTab == i) ? dockActiveColor : dockNormalColor;
            }
        }
    }

    GameObject CreateButton(Transform parent, string name, string label, int fontSize, Color bgColor, Color textColor, Vector2 anchoredPosition, Vector2 sizeDelta, System.Action onClick)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = bgColor;

        Button btn = go.AddComponent<Button>();
        // 키보드/게임패드 자동 선택(Navigation)을 꺼서, 마우스로 한 번 누른 버튼이 계속 "선택된 상태"로 남지 않게 함.
        // 이게 켜져 있으면 나중에 스페이스바(=UI Submit 키)를 누를 때 전혀 다른 상황에서(ex: 주유소) 이 버튼이
        // 다시 눌린 것처럼 동작해버리는 문제가 있었음 (ex: 콜 수락 버튼)
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        if (onClick != null)
        {
            btn.onClick.AddListener(() =>
            {
                onClick();
                // 클릭 직후 선택 상태를 바로 해제 - 위 주석과 같은 이유
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            });
        }

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        Text text = labelGO.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = textColor;
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;

        RectTransform labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return go;
    }

    Text CreateLabel(Transform parent, string initialText, float y, int fontSize)
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
        rect.sizeDelta = new Vector2(-16f, 30f);

        return text;
    }

    void Update()
    {
        DeliveryJobManager manager = DeliveryJobManager.Instance;

        // 콜 수락 단축키: F (예전엔 스페이스바였는데, 주유소 열기 등 다른 스페이스바 조작과 겹쳐서 F로 변경)
        // 수락 버튼 클릭은 그대로 유지됨 - 이건 키보드 단축키만 추가하는 것
        if (manager != null && manager.State == DeliveryJobManager.JobState.Proposed && Input.GetKeyDown(KeyCode.F))
        {
            manager.AcceptJob();
        }

        UpdateTopBar();
        UpdateMinimap();

        if (currentTab == AppTab.Call)
        {
            UpdateCallPanel(manager);
        }

        if (currentTab == AppTab.Vehicle)
        {
            UpdateVehiclePanel();
        }

        if (currentTab == AppTab.Shop)
        {
            RefreshShopVehicleRows();
        }

        UpdateCallBadge(manager);
    }

    void UpdateTopBar()
    {
        if (topBarText == null)
        {
            return;
        }

        string text = "💰" + DeliveryManager.Money;

        if (DeliveryManager.HasFood)
        {
            text += "  ⏱" + Mathf.CeilToInt(DeliveryManager.RemainingTime) + "초";
        }

        // 연료 게이지는 이제 좌하단 체력바 위의 전용 바(FuelUI)에서 항상 보여주므로 여기서는 표시하지 않음
        // (탈것 탭 목록에는 여전히 각 탈것 옆에 연료 %가 표시됨)

        topBarText.text = text;
    }

    void UpdateCallPanel(DeliveryJobManager manager)
    {
        if (callStatusText == null)
        {
            return;
        }

        if (manager == null)
        {
            callStatusText.text = "콜 대기 중...";
            callInfoText.text = "";
            SetCallTimerVisible(false);
            SetAcceptVisible(false);
            return;
        }

        switch (manager.State)
        {
            case DeliveryJobManager.JobState.Proposed:
                callStatusText.text = (manager.IsUrgent ? "🔥 긴급 콜 도착!\n" : "새 콜 도착!\n") + manager.CurrentPickupSpot.name + " → " + manager.CurrentDeliverySpot.name;
                callInfoText.text = "거리 " + FormatDistance(manager.PickupToDeliveryDistance) + " · 예상 " + FormatTime(manager.PickupToDeliveryEtaSeconds)
                    + " · 💰" + manager.EstimatedFare + "원" + (manager.IsUrgent ? " (긴급 보너스)" : "");
                SetCallTimerVisible(true);
                SetAcceptVisible(true);

                if (callTimerFillImage != null)
                {
                    float percent = manager.offerTimeLimit > 0f ? Mathf.Clamp01(manager.OfferSecondsRemaining / manager.offerTimeLimit) : 0f;
                    callTimerFillImage.rectTransform.anchorMax = new Vector2(percent, 1f);
                    callTimerFillImage.color = percent <= 0.3f ? new Color(0.9f, 0.25f, 0.2f) : new Color(1f, 0.75f, 0.15f);
                }
                break;

            case DeliveryJobManager.JobState.GoingToPickup:
                callStatusText.text = "픽업 장소로 이동 중\n" + (manager.CurrentPickupSpot != null ? manager.CurrentPickupSpot.name : "-");
                callInfoText.text = player != null && manager.CurrentPickupSpot != null
                    ? "남은 거리: " + FormatDistance(Vector3.Distance(player.position, manager.CurrentPickupSpot.transform.position))
                    : "";
                SetCallTimerVisible(false);
                SetAcceptVisible(false);
                break;

            case DeliveryJobManager.JobState.GoingToDelivery:
                callStatusText.text = "배달지로 이동 중\n" + (manager.CurrentDeliverySpot != null ? manager.CurrentDeliverySpot.name : "-");
                callInfoText.text = player != null && manager.CurrentDeliverySpot != null
                    ? "남은 거리: " + FormatDistance(Vector3.Distance(player.position, manager.CurrentDeliverySpot.transform.position))
                    : "";
                SetCallTimerVisible(false);
                SetAcceptVisible(false);
                break;
        }
    }

    void SetCallTimerVisible(bool visible)
    {
        if (callTimerBarGO != null && callTimerBarGO.activeSelf != visible)
        {
            callTimerBarGO.SetActive(visible);
        }
    }

    void SetAcceptVisible(bool visible)
    {
        if (acceptButtonGO != null && acceptButtonGO.activeSelf != visible)
        {
            acceptButtonGO.SetActive(visible);
        }
    }

    void UpdateVehiclePanel()
    {
        if (VehicleMount.Instance == null)
        {
            return;
        }

        foreach (OwnedVehicleRow row in ownedVehicleRows)
        {
            bool isActive = VehicleCatalog.ActiveId == row.id;
            bool isMounted = isActive && VehicleMount.Instance.IsMounted;
            bool isVisible = isActive && VehicleMount.Instance.IsVisible;

            VehicleCatalog.VehicleDefinition def = VehicleCatalog.GetById(row.id);
            string name = def != null ? def.displayName : row.id;

            // 연료가 필요한 탈것(오토바이/자동차)은 이름 옆에 연료 %도 같이 보여줌
            string fuelSuffix = def != null && def.needsFuel ? " ⛽" + Mathf.RoundToInt(VehicleCatalog.GetFuelPercent(def.id) * 100f) + "%" : "";

            if (row.statusText != null)
            {
                if (isMounted)
                {
                    row.statusText.text = name + fuelSuffix + " - 타는 중";
                }
                else if (isActive && isVisible)
                {
                    row.statusText.text = name + fuelSuffix + " - 장비됨 (소환됨)";
                }
                else if (isActive)
                {
                    row.statusText.text = name + fuelSuffix + " - 장비됨 (보관 중)";
                }
                else
                {
                    row.statusText.text = name + fuelSuffix;
                }
            }

            if (row.actionButtonLabel != null)
            {
                if (isMounted)
                {
                    row.actionButtonLabel.text = "타는 중";
                }
                else if (isActive && isVisible)
                {
                    row.actionButtonLabel.text = "보관하기";
                }
                else if (isActive)
                {
                    row.actionButtonLabel.text = "부르기";
                }
                else
                {
                    row.actionButtonLabel.text = "선택";
                }
            }
        }
    }

    void UpdateCallBadge(DeliveryJobManager manager)
    {
        if (callBadgeGO == null)
        {
            return;
        }

        bool shouldShow = manager != null && manager.State == DeliveryJobManager.JobState.Proposed && currentTab != AppTab.Call;
        if (callBadgeGO.activeSelf != shouldShow)
        {
            callBadgeGO.SetActive(shouldShow);
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

    string FormatDistance(float meters)
    {
        if (meters >= 1000f)
        {
            return (meters / 1000f).ToString("F1") + "km";
        }
        return Mathf.RoundToInt(meters) + "m";
    }

    string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(seconds);
        if (total < 60)
        {
            return total + "초";
        }
        int minutes = total / 60;
        int secs = total % 60;
        return minutes + "분 " + secs + "초";
    }
}
