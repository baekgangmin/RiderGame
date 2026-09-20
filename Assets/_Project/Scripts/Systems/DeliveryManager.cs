using UnityEngine;

// 배달 상태/정산 관리 (M4) - 정적 클래스라 씬에 오브젝트로 안 넣어도 바로 동작함
public static class DeliveryManager
{
    public static bool HasFood { get; private set; }

    private static float pickupTime;
    private static float timeLimit;

    [Header("정산 설정")]
    public static int baseFare = 5000;           // 기본 배달비(원)
    public static float latePenaltyPerSecond = 100f; // 지각 1초당 차감(원)

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
        int payout = Mathf.Max(0, baseFare - deduction);

        if (lateSeconds <= 0f)
        {
            Debug.Log($"정시 도착! 소요시간 {elapsed:F1}초 → 배달비 {payout}원 지급");
        }
        else
        {
            Debug.Log($"지각 {lateSeconds:F1}초! 배달비 {baseFare}원에서 {deduction}원 차감 → {payout}원 지급");
        }

        HasFood = false;
    }
}
