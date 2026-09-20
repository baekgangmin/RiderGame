using UnityEngine;

// 배달 가능한 스팟 (M6) - 마을의 모든 건물 앞에 하나씩 붙음
// DeliveryJobManager가 매 배달마다 이 중 2개를 랜덤으로 골라서 하나는 Pickup, 하나는 Delivery로 지정함
[RequireComponent(typeof(Collider))]
public class DeliverySpot : MonoBehaviour
{
    public enum SpotRole { Inactive, Pickup, Delivery }

    [Header("픽업 홀드 시간(초)")]
    public float holdDuration = 3f;

    [Header("배달 제한시간 계산")]
    public float speedEstimateForTimeLimit = 5f;
    public float pathFactor = 1.4f;
    public float bufferSeconds = 8f;

    [Header("플레이어 태그")]
    public string playerTag = "Player";

    [Header("마커 색상")]
    public Color pickupColor = new Color(1f, 0.85f, 0.1f);   // 노랑
    public Color deliveryColor = new Color(0.2f, 0.6f, 1f);  // 파랑

    public SpotRole Role { get; private set; } = SpotRole.Inactive;

    private GameObject marker;
    private Renderer markerRenderer;
    private float holdTimer;
    private bool playerInZone;

    void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        DeliveryJobManager.Register(this);
        CreateMarker();
        SetRole(SpotRole.Inactive);
    }

    void OnDestroy()
    {
        DeliveryJobManager.Unregister(this);
    }

    void CreateMarker()
    {
        marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "Marker";
        marker.transform.SetParent(transform);
        marker.transform.localPosition = new Vector3(0f, 10f, 0f);
        marker.transform.localScale = Vector3.one * 2.5f;

        Collider markerCol = marker.GetComponent<Collider>();
        if (markerCol != null)
        {
            Destroy(markerCol);
        }

        markerRenderer = marker.GetComponent<Renderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
        {
            markerRenderer.material = new Material(shader);
        }

        marker.SetActive(false);
    }

    public void SetRole(SpotRole role)
    {
        Role = role;
        holdTimer = 0f;
        playerInZone = false;

        if (marker == null)
        {
            return;
        }

        if (role == SpotRole.Inactive)
        {
            marker.SetActive(false);
            return;
        }

        marker.SetActive(true);
        Color c = role == SpotRole.Pickup ? pickupColor : deliveryColor;

        if (markerRenderer != null && markerRenderer.material.HasProperty("_BaseColor"))
        {
            markerRenderer.material.SetColor("_BaseColor", c);
        }
        else if (markerRenderer != null)
        {
            markerRenderer.material.color = c;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        if (Role == SpotRole.Pickup)
        {
            playerInZone = true;
            holdTimer = 0f;
        }
        else if (Role == SpotRole.Delivery && DeliveryManager.HasFood)
        {
            DeliveryManager.CompleteDelivery();
            SetRole(SpotRole.Inactive);

            if (DeliveryJobManager.Instance != null)
            {
                DeliveryJobManager.Instance.AssignNewJob();
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag) && Role == SpotRole.Pickup)
        {
            playerInZone = false;
            holdTimer = 0f;
        }
    }

    void Update()
    {
        if (Role != SpotRole.Pickup || !playerInZone || DeliveryManager.HasFood)
        {
            return;
        }

        if (Input.GetKey(KeyCode.Space))
        {
            holdTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(holdTimer / holdDuration);
            Debug.Log($"픽업 진행률: {progress * 100f:F0}%");

            if (holdTimer >= holdDuration)
            {
                CompletePickup();
            }
        }
        else
        {
            holdTimer = 0f;
        }
    }

    void CompletePickup()
    {
        float limit = 30f;
        DeliverySpot deliverySpot = DeliveryJobManager.Instance != null ? DeliveryJobManager.Instance.CurrentDeliverySpot : null;

        if (deliverySpot != null)
        {
            float distance = Vector3.Distance(transform.position, deliverySpot.transform.position);
            limit = (distance / speedEstimateForTimeLimit) * pathFactor + bufferSeconds;
        }

        Debug.Log("픽업 완료! 이제 배달지로 이동하세요.");
        DeliveryManager.StartDelivery(limit);

        SetRole(SpotRole.Inactive);
    }
}
