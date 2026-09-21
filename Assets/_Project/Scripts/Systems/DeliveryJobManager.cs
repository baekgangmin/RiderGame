using System.Collections.Generic;
using UnityEngine;

// 배달 건(job) 배정 관리 (M6) - 새 배달을 "제안"하고, 폰에서 수락해야 실제로 픽업/배달 안내가 시작됨
public class DeliveryJobManager : MonoBehaviour
{
    public enum JobState { Proposed, GoingToPickup, GoingToDelivery }

    public static DeliveryJobManager Instance { get; private set; }

    private static readonly List<DeliverySpot> spots = new List<DeliverySpot>();

    [Header("콜 수락 제한시간(초) - 안 누르면 자동으로 다음 콜로 넘어감")]
    public float offerTimeLimit = 15f;

    private float offerStartTime;

    public JobState State { get; private set; } = JobState.Proposed;

    public DeliverySpot CurrentPickupSpot { get; private set; }
    public DeliverySpot CurrentDeliverySpot { get; private set; }

    // 콜 수락까지 남은 시간 (Proposed 상태가 아니면 0)
    public float OfferSecondsRemaining
    {
        get
        {
            if (State != JobState.Proposed)
            {
                return 0f;
            }
            return Mathf.Max(0f, offerTimeLimit - (Time.time - offerStartTime));
        }
    }

    // 픽업지 -> 배달지 직선 거리(m) - 콜받기 화면에서 실제 라이더 앱처럼 표시할 때 사용
    public float PickupToDeliveryDistance
    {
        get
        {
            if (CurrentPickupSpot == null || CurrentDeliverySpot == null)
            {
                return 0f;
            }
            return Vector3.Distance(CurrentPickupSpot.transform.position, CurrentDeliverySpot.transform.position);
        }
    }

    // 위 거리를 대략적인 이동 속도로 환산한 예상 소요시간(초) - DeliverySpot의 제한시간 계산과 같은 기준(5m/s, 1.4배)을 사용
    public float PickupToDeliveryEtaSeconds => (PickupToDeliveryDistance / 5f) * 1.4f;

    // "Enter Play Mode Options"에서 Reload Domain이 꺼져 있으면 static 필드(spots 목록)가
    // 이전 플레이 세션 것을 그대로 들고 다음 플레이로 넘어올 수 있음 - Play를 누를 때마다 항상 깨끗하게 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnPlayStart()
    {
        spots.Clear();
        Instance = null;
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ProposeNewJob();
    }

    void Update()
    {
        // 콜을 제안받은 상태에서 제한시간 안에 수락하지 않으면 자동으로 다음 콜로 넘어감
        if (State == JobState.Proposed && OfferSecondsRemaining <= 0f)
        {
            Debug.Log("콜 수락 시간이 지나 다음 콜로 넘어갑니다.");
            ProposeNewJob();
        }
    }

    public static void Register(DeliverySpot spot)
    {
        if (!spots.Contains(spot))
        {
            spots.Add(spot);
        }
    }

    public static void Unregister(DeliverySpot spot)
    {
        spots.Remove(spot);
    }

    // 새 배달 요청을 랜덤으로 고름 - 아직 수락 전이라 마커는 표시하지 않음
    public void ProposeNewJob()
    {
        if (spots.Count < 2)
        {
            Debug.LogWarning("배달 스팟이 2개 미만이라 새 배달을 제안할 수 없습니다.");
            return;
        }

        if (CurrentPickupSpot != null)
        {
            CurrentPickupSpot.SetRole(DeliverySpot.SpotRole.Inactive);
        }
        if (CurrentDeliverySpot != null)
        {
            CurrentDeliverySpot.SetRole(DeliverySpot.SpotRole.Inactive);
        }

        int pickupIndex = Random.Range(0, spots.Count);
        int deliveryIndex;
        do
        {
            deliveryIndex = Random.Range(0, spots.Count);
        } while (deliveryIndex == pickupIndex);

        CurrentPickupSpot = spots[pickupIndex];
        CurrentDeliverySpot = spots[deliveryIndex];

        State = JobState.Proposed;
        offerStartTime = Time.time;

        Debug.Log("새 배달 요청 도착: " + CurrentPickupSpot.name + " -> " + CurrentDeliverySpot.name + " (핸드폰에서 수락하세요)");
    }

    // 폰의 수락 버튼에서 호출됨 - 이때부터 픽업존 마커/안내가 활성화됨
    public void AcceptJob()
    {
        if (State != JobState.Proposed || CurrentPickupSpot == null)
        {
            return;
        }

        State = JobState.GoingToPickup;
        CurrentPickupSpot.SetRole(DeliverySpot.SpotRole.Pickup);

        Debug.Log("배달 수락! " + CurrentPickupSpot.name + "로 이동하세요.");
    }

    // 픽업 완료 시 DeliverySpot이 호출함 - 배달지 마커/안내로 전환
    public void OnPickedUp()
    {
        if (State != JobState.GoingToPickup)
        {
            return;
        }

        State = JobState.GoingToDelivery;

        if (CurrentDeliverySpot != null)
        {
            CurrentDeliverySpot.SetRole(DeliverySpot.SpotRole.Delivery);
        }
    }

    // 배달 완료 시 DeliverySpot이 호출함 - 다음 배달을 새로 제안(수락 대기)
    public void OnDelivered()
    {
        if (CurrentPickupSpot != null)
        {
            CurrentPickupSpot.SetRole(DeliverySpot.SpotRole.Inactive);
        }
        if (CurrentDeliverySpot != null)
        {
            CurrentDeliverySpot.SetRole(DeliverySpot.SpotRole.Inactive);
        }

        ProposeNewJob();
    }
}
