using UnityEngine;

// 체력(스태미나) 시스템 (M5) - 달리기/부스터에 쓰이는 자원
public class StaminaSystem : MonoBehaviour
{
    public static StaminaSystem Instance;

    [Header("스태미나 설정")]
    public float maxStamina = 100f;
    public float drainPerSecond = 20f;
    public float regenPerSecond = 15f;
    public float regenDelay = 1.5f; // 소진 후 회복 시작까지 대기 시간(초)

    private float currentStamina;
    private float lastUseTime;

    public float StaminaPercent => currentStamina / maxStamina;
    public bool IsExhausted => currentStamina <= 0f;

    void Awake()
    {
        Instance = this;
        currentStamina = maxStamina;
    }

    // 이동 스크립트에서 매 프레임 호출. 스태미나가 있으면 소모하고 true 반환
    public bool TryUseStamina(float deltaTime)
    {
        if (currentStamina <= 0f)
        {
            return false;
        }

        currentStamina -= drainPerSecond * deltaTime;
        currentStamina = Mathf.Max(0f, currentStamina);
        lastUseTime = Time.time;
        return true;
    }

    // 물/음료 아이템 등에서 즉시 회복시킬 때 사용
    public void Restore(float amount)
    {
        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
    }

    void Update()
    {
        if (Time.time - lastUseTime >= regenDelay && currentStamina < maxStamina)
        {
            currentStamina += regenPerSecond * Time.deltaTime;
            currentStamina = Mathf.Min(maxStamina, currentStamina);
        }
    }
}
