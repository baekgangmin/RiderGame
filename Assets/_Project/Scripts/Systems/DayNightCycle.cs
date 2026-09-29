using UnityEngine;

// 낮/밤 순환 (M?) - 태양 역할의 Directional Light를 하루 동안 한 바퀴 회전시켜서
// 아침->정오->노을->밤->새벽이 반복되게 함. 하늘색 자체는 Skybox/Procedural이 이 빛의
// 각도를 보고 알아서 계산해주므로(스카이박스는 따로 안 건드림) 빛의 회전/밝기/색만 바꿔주면 됨.
public class DayNightCycle : MonoBehaviour
{
    [Tooltip("하루(정오->노을->밤->새벽->다시 정오)가 도는 데 걸리는 실제 시간(초). 테스트용 3분 = 180초")]
    public float dayLengthSeconds = 180f;

    [Tooltip("낮 최대 광량")]
    public float maxSunIntensity = 2f;
    [Tooltip("밤에도 완전히 깜깜해지지 않게 남겨두는 최소 광량(달빛 느낌)")]
    public float minSunIntensity = 0.05f;

    public Color sunriseColor = new Color(1f, 0.6f, 0.35f);
    public Color noonColor = new Color(1f, 0.98f, 0.92f);
    public Color sunsetColor = new Color(1f, 0.5f, 0.3f);
    public Color nightColor = new Color(0.35f, 0.4f, 0.65f);

    private Light sun;
    private float baseYRotation;

    void Awake()
    {
        sun = GetComponent<Light>();
        baseYRotation = transform.eulerAngles.y;
    }

    void Update()
    {
        if (sun == null || dayLengthSeconds <= 0f)
        {
            return;
        }

        // t: 하루 중 진행률(0~1) - 0/0.5가 일출/일몰, 0.25가 정오, 0.75가 자정
        float t = Mathf.Repeat(Time.time / dayLengthSeconds, 1f);
        float sunAngle = t * 360f;
        transform.rotation = Quaternion.Euler(sunAngle, baseYRotation, 0f);

        // 태양이 지평선 위(0~180도)일 때만 낮 - sin으로 정오(90도)에서 제일 밝고 지평선 근처에서 약해짐.
        // 지평선 아래(180~360도)는 음수가 나오니 0으로 눌러서 밤 동안은 최소 광량만 유지함
        float dayFactor = Mathf.Clamp01(Mathf.Sin(sunAngle * Mathf.Deg2Rad));

        sun.intensity = Mathf.Lerp(minSunIntensity, maxSunIntensity, dayFactor);
        sun.color = EvaluateSunColor(t);
    }

    private Color EvaluateSunColor(float t)
    {
        if (t < 0.25f)
        {
            return Color.Lerp(sunriseColor, noonColor, t / 0.25f);
        }
        if (t < 0.5f)
        {
            return Color.Lerp(noonColor, sunsetColor, (t - 0.25f) / 0.25f);
        }
        if (t < 0.75f)
        {
            return Color.Lerp(sunsetColor, nightColor, (t - 0.5f) / 0.25f);
        }
        return Color.Lerp(nightColor, sunriseColor, (t - 0.75f) / 0.25f);
    }
}
