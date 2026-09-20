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
    private GameObject blip;
    private Renderer blipRenderer;
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
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        // 작은 구슬 대신 땅에서 하늘까지 이어지는 기둥(빔)으로 만들어서 건물 사이에서도 멀리서 눈에 띄게 함
        marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "Marker";
        marker.transform.SetParent(transform);
        marker.transform.localPosition = new Vector3(0f, 15f, 0f);
        marker.transform.localScale = new Vector3(0.8f, 15f, 0.8f);

        Collider markerCol = marker.GetComponent<Collider>();
        if (markerCol != null)
        {
            Destroy(markerCol);
        }

        markerRenderer = marker.GetComponent<Renderer>();
        if (shader != null)
        {
            markerRenderer.material = new Material(shader);
        }

        // 미니맵 전용 큰 원반(blip) - 기둥 꼭대기에 납작하게 붙여서 위에서 내려다보면 크게 찍히지만
        // 3인칭 카메라에서 옆에서 보면 두께가 거의 없어 눈에 거슬리지 않음
        blip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        blip.name = "MapBlip";
        blip.transform.SetParent(transform);
        blip.transform.localPosition = new Vector3(0f, 30f, 0f);
        blip.transform.localScale = new Vector3(5f, 0.15f, 5f);

        Collider blipCol = blip.GetComponent<Collider>();
        if (blipCol != null)
        {
            Destroy(blipCol);
        }

        blipRenderer = blip.GetComponent<Renderer>();
        if (shader != null)
        {
            blipRenderer.material = new Material(shader);
        }

        marker.SetActive(false);
        blip.SetActive(false);
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
            if (blip != null)
            {
                blip.SetActive(false);
            }
            return;
        }

        marker.SetActive(true);
        if (blip != null)
        {
            blip.SetActive(true);
        }

        Color c = role == SpotRole.Pickup ? pickupColor : deliveryColor;
        ApplyColor(markerRenderer, c);
        ApplyColor(blipRenderer, c);
    }

    private void ApplyColor(Renderer targetRenderer, Color c)
    {
        if (targetRenderer == null)
        {
            return;
        }

        Material mat = targetRenderer.material;
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", c);
        }
        else
        {
            mat.color = c;
        }

        // 더 눈에 띄도록 살짝 발광 처리
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", c * 1.5f);
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
                DeliveryJobManager.Instance.OnDelivered();
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
            // 씬 로드 직후 등 한 프레임이 유독 길게 걸렸을 때 holdTimer가 한 번에 확 튀어서
            // 스페이스바를 누르자마자 바로 완료돼버리는 걸 막기 위해 프레임당 증가량을 제한함
            holdTimer += Mathf.Min(Time.deltaTime, 0.1f);
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

        if (DeliveryJobManager.Instance != null)
        {
            DeliveryJobManager.Instance.OnPickedUp();
        }
    }
}
