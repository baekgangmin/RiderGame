using UnityEngine;

// 배달 상태/정산 관리 (M4) - 정적 클래스라 씬에 오브젝트로 안 넣어도 바로 동작함
public static class DeliveryManager
{
    public static bool HasFood { get; private set; }
    public static int Money { get; private set; }

    // 가장 최근 배달의 결과 (평점 UI 등에서 참고용으로 사용)
    public static int LastRating { get; private set; }
    public static int LastPayout { get; private set; }
    public static int LastTip { get; private set; }
    public static bool LastWasLate { get; private set; }

    private static float pickupTime;
    private static float timeLimit;
    private static int currentFare;

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
    public static int baseFare = 5000;           // 기본 배달비(원) - 여기에 거리 비례 요금이 더해짐
    public static float farePerMeter = 25f;      // 픽업지-배달지 직선거리 1m당 추가 배달비(원) - 콜마다 보상이 달라지게 함
    public static float latePenaltyPerSecond = 100f; // 지각 1초당 차감(원) - 배달비를 넘으면 보유 금액에서 추가로 차감됨

    // 픽업지-배달지 거리로 이번 콜의 배달비를 미리 계산 - 콜받기 화면 미리보기, 실제 정산 모두 이 값을 사용
    public static int CalculateFare(float pickupToDeliveryDistance)
    {
        return baseFare + Mathf.RoundToInt(pickupToDeliveryDistance * farePerMeter);
    }

    // 유니티 에디터의 "Enter Play Mode Options"에서 Reload Domain이 꺼져 있으면
    // static 필드가 이전 플레이 세션 값을 그대로 들고 다음 플레이가 시작될 수 있음
    // (예: 이전에 픽업만 해두고 배달 전에 멈추면, 다음 Play를 누르자마자 시간이 이미 흐르고 있는 것처럼 보임)
    // Play를 누를 때마다(씬이 로드될 때마다) 항상 깨끗한 상태로 시작하도록 강제 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnPlayStart()
    {
        HasFood = false;
        Money = startingMoney; // 테스트하기 편하게 시작 자금을 넉넉히 줌 - 나중에 밸런스 잡을 때 0으로 되돌리면 됨
        pickupTime = 0f;
        timeLimit = 0f;
    }

    [Header("시작 자금 (테스트용)")]
    public static int startingMoney = 200000;

    // fare를 생략하면(음수) 기본 배달비를 씀 - 예전 호출부(PickupZone 등)와의 호환용
    public static void StartDelivery(float limitSeconds, int fare = -1)
    {
        HasFood = true;
        pickupTime = Time.time;
        timeLimit = limitSeconds;
        currentFare = fare >= 0 ? fare : baseFare;
        Debug.Log($"배달 시작! 제한시간 {timeLimit:F0}초, 배달비 {currentFare}원");
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
        int payout = currentFare - deduction; // 많이 늦으면 음수가 될 수 있음 -> 내 돈에서 차감

        // 얼마나 여유있게(또는 늦게) 도착했는지로 배달 평점(1~5)을 매김
        int rating;
        if (lateSeconds <= 0f)
        {
            float ratio = timeLimit > 0f ? elapsed / timeLimit : 1f;
            if (ratio <= 0.5f) { rating = 5; }
            else if (ratio <= 0.8f) { rating = 4; }
            else { rating = 3; }
        }
        else if (lateSeconds <= 5f)
        {
            rating = 2;
        }
        else
        {
            rating = 1;
        }

        // 평점이 좋을 때(정시 도착 + 여유있게)만 손님이 팁을 얹어줌
        int tip = 0;
        if (rating == 5) { tip = Random.Range(800, 2001); }
        else if (rating == 4) { tip = Random.Range(300, 900); }

        Money += payout + tip;

        LastRating = rating;
        LastPayout = payout;
        LastTip = tip;
        LastWasLate = lateSeconds > 0f;

        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        Vector3 sfxPos = playerGO != null ? playerGO.transform.position : Vector3.zero;

        if (lateSeconds <= 0f)
        {
            Debug.Log($"정시 도착! 소요시간 {elapsed:F1}초 → 배달비 {payout}원 + 팁 {tip}원 지급 (보유 금액: {Money}원) - 평점 {rating}점");
            Sfx.PlaySuccess(sfxPos);
        }
        else if (payout >= 0)
        {
            Debug.Log($"지각 {lateSeconds:F1}초! 배달비 {currentFare}원에서 {deduction}원 차감 → {payout}원 지급 (보유 금액: {Money}원) - 평점 {rating}점");
            Sfx.PlayLate(sfxPos);
        }
        else
        {
            Debug.Log($"많이 늦었어요! 배달비를 넘는 {-payout}원을 내 돈에서 차감 (보유 금액: {Money}원) - 평점 {rating}점");
            Sfx.PlayLate(sfxPos);
        }

        DeliveryResultUI.Show(rating, payout, tip, LastWasLate);

        HasFood = false;
    }

    // 상점(음료/탈것 구매 등)에서 사용 - 돈이 모자라면 아무 일도 안 하고 false 반환
    public static bool SpendMoney(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (Money < amount)
        {
            return false;
        }

        Money -= amount;
        return true;
    }
}
