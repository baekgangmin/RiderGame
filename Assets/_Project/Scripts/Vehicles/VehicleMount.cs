using UnityEngine;

// 탈것(자전거 등) 탑승/하차 (M5) - 트리거 범위 안에서 E키로 토글
[RequireComponent(typeof(Collider))]
public class VehicleMount : MonoBehaviour
{
    [Header("플레이어 태그")]
    public string playerTag = "Player";

    [Header("탑승/하차 키")]
    public KeyCode mountKey = KeyCode.E;

    private bool playerInRange;
    private bool isMounted;
    private PlayerMovement playerMovement;

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInRange = true;
            playerMovement = other.GetComponent<PlayerMovement>();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInRange = false;
        }
    }

    void Update()
    {
        if (!playerInRange || playerMovement == null)
        {
            return;
        }

        if (Input.GetKeyDown(mountKey))
        {
            isMounted = !isMounted;
            playerMovement.SetRiding(isMounted);
            Debug.Log(isMounted ? "자전거 탑승! 이동 속도 상승." : "자전거에서 내림.");
        }
    }
}
