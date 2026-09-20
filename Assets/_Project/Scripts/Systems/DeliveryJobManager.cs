using System.Collections.Generic;
using UnityEngine;

// 배달 건(job) 배정 관리 (M6) - 매 배달마다 등록된 스팟 중 2곳을 랜덤으로 골라서
// 하나는 픽업존, 하나는 배달존으로 지정함
public class DeliveryJobManager : MonoBehaviour
{
    public static DeliveryJobManager Instance { get; private set; }

    private static readonly List<DeliverySpot> spots = new List<DeliverySpot>();

    public DeliverySpot CurrentPickupSpot { get; private set; }
    public DeliverySpot CurrentDeliverySpot { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        AssignNewJob();
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

    public void AssignNewJob()
    {
        if (spots.Count < 2)
        {
            Debug.LogWarning("배달 스팟이 2개 미만이라 새 배달을 배정할 수 없습니다.");
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

        CurrentPickupSpot.SetRole(DeliverySpot.SpotRole.Pickup);
        CurrentDeliverySpot.SetRole(DeliverySpot.SpotRole.Delivery);

        Debug.Log("새 배달 배정: " + CurrentPickupSpot.name + " -> " + CurrentDeliverySpot.name);
    }
}
