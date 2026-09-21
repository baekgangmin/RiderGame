using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 배달 완료 결과 팝업 (M10) - 배달을 마치면 화면 위쪽에 별점 + 정산 금액을 잠깐 보여주고 자동으로 사라짐
// 씬에 직접 오브젝트를 추가할 필요 없이, 처음 호출될 때 스스로 만들어짐(다른 UI들과 달리 수동 설치 불필요)
public class DeliveryResultUI : MonoBehaviour
{
    private static DeliveryResultUI instance;

    private Text ratingText;
    private Text payoutText;
    private CanvasGroup canvasGroup;
    private Coroutine showRoutine;

    // DeliveryManager 등에서 배달 정산 직후 호출
    public static void Show(int rating, int payout, int tip, bool wasLate)
    {
        EnsureInstance();
        instance.DisplayResult(rating, payout, tip, wasLate);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject go = new GameObject("DeliveryResultUI");
        instance = go.AddComponent<DeliveryResultUI>();
    }

    void Awake()
    {
        BuildUI();
    }

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("DeliveryResultCanvas");
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        canvasGO.AddComponent<CanvasScaler>();

        GameObject panelGO = new GameObject("ResultPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        canvasGroup = panelGO.AddComponent<CanvasGroup>();
        Image bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.78f);
        panelRect.anchorMax = new Vector2(0.5f, 0.78f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(360f, 110f);

        ratingText = CreateLabel(panelGO.transform, "RatingText", "", 30, new Vector2(0f, 20f), new Vector2(320f, 44f));
        payoutText = CreateLabel(panelGO.transform, "PayoutText", "", 19, new Vector2(0f, -24f), new Vector2(320f, 32f));

        canvasGroup.alpha = 0f;
        panelGO.SetActive(false);
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

    private void DisplayResult(int rating, int payout, int tip, bool wasLate)
    {
        string stars = new string('⭐', Mathf.Clamp(rating, 1, 5));
        ratingText.text = stars;
        ratingText.color = wasLate ? new Color(1f, 0.6f, 0.3f) : new Color(0.4f, 1f, 0.5f);

        string payoutLine = payout >= 0 ? ("배달 완료! +" + payout + "원") : ("많이 늦었어요.. " + payout + "원");
        if (tip > 0)
        {
            payoutLine += "  (+팁 " + tip + "원 🎁)";
        }
        payoutText.text = payoutLine;

        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
        }
        showRoutine = StartCoroutine(ShowAndFadeOut());
    }

    private IEnumerator ShowAndFadeOut()
    {
        canvasGroup.gameObject.SetActive(true);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 6f;
            canvasGroup.alpha = Mathf.Clamp01(t);
            yield return null;
        }

        yield return new WaitForSeconds(1.8f);

        t = 1f;
        while (t > 0f)
        {
            t -= Time.deltaTime * 3f;
            canvasGroup.alpha = Mathf.Clamp01(t);
            yield return null;
        }

        canvasGroup.gameObject.SetActive(false);
    }
}
