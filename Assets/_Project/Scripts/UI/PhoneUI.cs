using UnityEngine;
using UnityEngine.UI;

// 핸드폰 UI (M6) - 실제 폰처럼 생긴 베젤 안에 목적지, 남은 시간, 보유 금액을 표시
// 음식을 픽업한 상태(배달 중)일 때는 배달지 방향을 가리키는 내비게이션 화살표도 같이 보여줌
public class PhoneUI : MonoBehaviour
{
    private Text destinationText;
    private Text timeText;
    private Text moneyText;
    private RectTransform arrowRect;

    private Transform player;

    void Start()
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            player = playerGO.transform;
        }

        BuildUI();
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

        destinationText = CreateLabel(screenGO.transform, font, "목적지: -", 110f, 16);
        timeText = CreateLabel(screenGO.transform, font, "남은 시간: -", 75f, 16);
        moneyText = CreateLabel(screenGO.transform, font, "보유 금액: 0원", 40f, 16);

        // 배달 중일 때만 보이는 내비게이션 화살표 (배달지 방향을 가리킴)
        GameObject arrowGO = new GameObject("NavArrow");
        arrowGO.transform.SetParent(screenGO.transform, false);
        Text arrowText = arrowGO.AddComponent<Text>();
        arrowText.font = font;
        arrowText.fontSize = 48;
        arrowText.color = new Color(0.3f, 0.9f, 0.4f);
        arrowText.text = "▲";
        arrowText.alignment = TextAnchor.MiddleCenter;

        arrowRect = arrowGO.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowRect.anchoredPosition = new Vector2(0f, -70f);
        arrowRect.sizeDelta = new Vector2(60f, 60f);

        arrowGO.SetActive(false);
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
        rect.sizeDelta = new Vector2(-16f, 34f);

        return text;
    }

    void Update()
    {
        UpdateDestination();
        UpdateTime();
        UpdateMoney();
        UpdateArrow();
    }

    void UpdateDestination()
    {
        if (destinationText == null)
        {
            return;
        }

        DeliveryJobManager manager = DeliveryJobManager.Instance;
        if (manager == null)
        {
            destinationText.text = "목적지: -";
            return;
        }

        if (DeliveryManager.HasFood)
        {
            string name = manager.CurrentDeliverySpot != null ? manager.CurrentDeliverySpot.name : "-";
            destinationText.text = "배달지: " + name;
        }
        else
        {
            string name = manager.CurrentPickupSpot != null ? manager.CurrentPickupSpot.name : "-";
            destinationText.text = "픽업 장소: " + name;
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

    void UpdateArrow()
    {
        if (arrowRect == null)
        {
            return;
        }

        bool shouldShowArrow = DeliveryManager.HasFood && player != null
            && DeliveryJobManager.Instance != null
            && DeliveryJobManager.Instance.CurrentDeliverySpot != null;

        if (arrowRect.gameObject.activeSelf != shouldShowArrow)
        {
            arrowRect.gameObject.SetActive(shouldShowArrow);
        }

        if (!shouldShowArrow)
        {
            return;
        }

        // 플레이어 기준 로컬 좌표로 목적지를 변환해서 화살표가 가리킬 각도를 계산
        Vector3 targetPos = DeliveryJobManager.Instance.CurrentDeliverySpot.transform.position;
        Vector3 localTarget = player.InverseTransformPoint(targetPos);
        float angleDeg = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;

        arrowRect.localEulerAngles = new Vector3(0f, 0f, -angleDeg);
    }
}
