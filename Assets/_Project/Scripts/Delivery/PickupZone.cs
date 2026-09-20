using UnityEngine;

// 가게 앞 픽업존 (M3) - 플레이어가 이 존 안에서 스페이스바를 holdDuration초 누르고 있으면 픽업 완료
// 사용법: 빈 GameObject를 만들고 Box Collider를 붙인 뒤 Is Trigger 체크, 이 스크립트를 추가
[RequireComponent(typeof(Collider))]
public class PickupZone : MonoBehaviour
{
    [Header("픽업에 걸리는 시간(초)")]
    public float holdDuration = 3f;

    [Header("배달 제한시간 계산 (거리 기반)")]
    public Transform deliveryPoint; // 배달지 Transform - TownGenerator가 자동으로 연결해줌 (직접 드래그해도 됨)
    public float speedEstimateForTimeLimit = 5f; // 제한시간 계산에 쓸 예상 이동 속도
    public float pathFactor = 1.4f; // 직선거리 대비 실제 이동 경로 보정 배수 (건물 사이로 돌아가는 것 감안)
    public float bufferSeconds = 8f; // 여유 시간

    [Header("deliveryPoint가 비어있을 때 쓸 고정 제한시간(초)")]
    public float fallbackTimeLimit = 30f;

    [Header("플레이어 태그")]
    public string playerTag = "Player";

    private float holdTimer = 0f;
    private bool playerInZone = false;
    private bool pickedUp = false;

    void Reset()
    {
        // Collider를 트리거로 강제 설정 (실수 방지)
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInZone = true;
            holdTimer = 0f;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInZone = false;
            holdTimer = 0f;
        }
    }

    void Update()
    {
        if (pickedUp || !playerInZone)
        {
            return;
        }

        if (Input.GetKey(KeyCode.Space))
        {
            holdTimer += Time.deltaTime;

            // TODO: 여기서 UI 프로그레스바에 (holdTimer / holdDuration) 값을 연결하면 진행률이 보임
            float progress = Mathf.Clamp01(holdTimer / holdDuration);
            Debug.Log($"픽업 진행률: {progress * 100f:F0}%");

            if (holdTimer >= holdDuration)
            {
                CompletePickup();
            }
        }
        else
        {
            // 키를 떼면 진행률 리셋
            holdTimer = 0f;
        }
    }

    void CompletePickup()
    {
        pickedUp = true;

        float limit = fallbackTimeLimit;
        if (deliveryPoint != null)
        {
            float distance = Vector3.Distance(transform.position, deliveryPoint.position);
            limit = (distance / speedEstimateForTimeLimit) * pathFactor + bufferSeconds;
        }

        Debug.Log("픽업 완료! 이제 배달지로 이동하세요.");
        DeliveryManager.StartDelivery(limit);
    }
}
