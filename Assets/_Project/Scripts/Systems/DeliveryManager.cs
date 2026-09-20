using UnityEngine;

// 배달 상태/정산 관리 (M4) - 정적 클래스라 씬에 오브젝트로 안 넣어도 바로 동작함
public static class DeliveryManager
{
    public static bool HasFood { get; private set; }
    public static int Money { get; private set; }

    private static float pickupTime;
    private static float timeLimit;

    // 폰 UI에서 남은 시간 표시할 때 사용
    public static float RemainingTime
    {
        get
        {
            if (!HasFood)
            {
                return 0f;
            }
            return Mathf.Max(0f, timeLimit - (Time.time - pickupTime));
        }
    }

    [Header("정산 설정")]
    public static int baseFare = 5000;           // 기본 배달비(원)
    public static float latePenaltyPerSecond = 100f; // 지각 1초당 차감(원) - 배달비를 넘으면 보유 금액에서 추가로 차감됨

    // 유니티 에디터의 "Enter Play Mode Options"에서 Reload Domain이 꺼져 있으면
    // static 필드가 이전 플레이 세션 값을 그대로 들고 다음 플레이가 시작될 수 있음
    // (예: 이전에 픽업만 해두고 배달 전에 멈추면, 다음 Play를 누르자마자 시간이 이미 흐르고 있는 것처럼 보임)
    // Play를 누를 때마다(씬이 로드될 때마다) 항상 깨끗한 상태로 시작하도록 강제 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnPlayStart()
    {
        HasFood = false;
        Money = 0;
        pickupTime = 0f;
        timeLimit = 0f;
    }

    public static void StartDelivery(float limitSeconds)
    {
        HasFood = true;
        pickupTime = Time.time;
        timeLimit = limitSeconds;
        Debug.Log($"배달 시작! 제한시간 {timeLimit:F0}초, 기본 배달비 {baseFare}원");
    }

    public static void CompleteDelivery()
    {
        if (!HasFood)
        {
            Debug.Log("배달할 음식이 없어요. 먼저 픽업하세요.");
            return;
        }

        float elapsed = Time.time - pickupTime;
        float lateSeconds = Mathf.Max(0f, elapsed - timeLimit);
        int deduction = Mathf.RoundToInt(lateSeconds * latePenaltyPerSecond);
        int payout = baseFare - deduction; // 많이 늦으면 음수가 될 수 있음 -> 내 돈에서 차감

        Money += payout;

        if (lateSeconds <= 0f)
        {
            Debug.Log($"정시 도착! 소요시간 {elapsed:F1}초 → 배달비 {payout}원 지급 (보유 금액: {Money}원)");
        }
        else if (payout >= 0)
        {
            Debug.Log($"지각 {lateSeconds:F1}초! 배달비 {baseFare}원에서 {deduction}원 차감 → {payout}원 지급 (보유 금액: {Money}원)");
        }
        else
        {
            Debug.Log($"많이 늦었어요! 배달비를 넘는 {-payout}원을 내 돈에서 차감 (보유 금액: {Money}원)");
        }

        HasFood = false;
    }
}
