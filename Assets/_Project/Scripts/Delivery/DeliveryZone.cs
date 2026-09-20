using UnityEngine;

// 배달지 존 (M4) - 음식을 들고 이 존에 들어오면 바로 정산됨
[RequireComponent(typeof(Collider))]
public class DeliveryZone : MonoBehaviour
{
    [Header("플레이어 태그")]
    public string playerTag = "Player";

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            DeliveryManager.CompleteDelivery();
        }
    }
}
