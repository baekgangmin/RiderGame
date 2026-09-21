using UnityEngine;

// 탈것(자전거 등) 탑승/하차 (M5) - 트리거 범위 안에서 E키로 토글
// 탑승하면 플레이어의 자식으로 붙어서 실제로 "타고 있는" 것처럼 위치/회전을 따라감 (기존: 속도만 빨라짐)
// 핸드폰 UI의 "부르기" 버튼을 누르면 플레이어 위치로 소환됨 (CallToPlayer)
[RequireComponent(typeof(Collider))]
public class VehicleMount : MonoBehaviour
{
    public static VehicleMount Instance;

    [Header("플레이어 태그")]
    public string playerTag = "Player";

    [Header("탑승/하차 키")]
    public KeyCode mountKey = KeyCode.E;

    [Header("탑승 시 플레이어 기준 위치/회전 (로컬)")]
    public Vector3 mountLocalPosition = new Vector3(0f, -0.35f, 0.2f);
    public Vector3 mountLocalEulerAngles = Vector3.zero;

    [Header("하차 시 플레이어 앞으로 떨어지는 거리")]
    public float dismountOffset = 1.5f;

    [Header("핸드폰에서 '부르기'를 눌렀을 때 플레이어 앞 소환 거리")]
    public float callDistance = 2f;

    [Header("쉬프트(가속) 중 연료 소모 배수 - 오토바이/자동차만 해당")]
    public float boostFuelDrainMultiplier = 2.5f;

    public bool IsMounted { get; private set; }

    private bool playerInRange;
    private PlayerMovement playerMovement;
    private Transform playerTransform;
    private Transform originalParent;
    private Collider col;
    private Vector3 originalScale;

    void Awake()
    {
        Instance = this;
        col = GetComponent<Collider>();
        originalParent = transform.parent;
        originalScale = transform.localScale;

        // 부르기 버튼은 범위 밖에서도 눌릴 수 있으니 플레이어 참조를 미리 찾아둠
        GameObject playerGO = GameObject.FindGameObjectWithTag(playerTag);
        if (playerGO != null)
        {
            playerTransform = playerGO.transform;
            playerMovement = playerGO.GetComponent<PlayerMovement>();
        }

        ApplyVisual(VehicleCatalog.GetActive());

        // 시작할 때마다 플레이어 자리에 자전거가 보이지 않도록 숨겨둠 - 핸드폰 "탈것" 탭에서 부르기로 불러오면 됨
        gameObject.SetActive(false);
    }

    // 현재 장비한 탈것 종류에 맞춰 크기/색을 바꿈 - 실제 3D 모델이 따로 없어서 크기+색으로 종류를 구분함
    void ApplyVisual(VehicleCatalog.VehicleDefinition def)
    {
        if (def == null)
        {
            return;
        }

        transform.localScale = originalScale * def.sizeScale;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            Material mat = r.material;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", def.bodyColor);
            }
            else
            {
                mat.color = def.bodyColor;
            }
        }
    }

    // 핸드폰 "탈것" 탭에서 다른 탈것을 고르면 호출됨 - 지금 갖고 있는 위치/보관 상태는 그대로 두고 모양/속도만 바뀜
    public void SelectVehicle(string id)
    {
        if (IsMounted)
        {
            return;
        }

        VehicleCatalog.SetActive(id);
        ApplyVisual(VehicleCatalog.GetActive());
    }

    void Reset()
    {
        Collider c = GetComponent<Collider>();
        c.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInRange = true;
            if (playerMovement == null)
            {
                playerMovement = other.GetComponent<PlayerMovement>();
                playerTransform = other.transform;
            }
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
        if (IsMounted)
        {
            HandleFuelDrain();
        }

        // 같은 프레임에 Mount/Dismount가 동시에 불리지 않도록 한 곳에서만 키 입력을 검사
        if (!Input.GetKeyDown(mountKey))
        {
            return;
        }

        if (IsMounted)
        {
            // 탑승 중에는 범위 체크 불필요 - 이미 붙어있음
            Dismount();
        }
        else if (playerInRange && playerMovement != null)
        {
            Mount();
        }
    }

    // 오토바이/자동차처럼 연료가 필요한 탈것은 타고 있는 동안 연료가 줄고, 다 떨어지면 자동으로 내려짐
    // 쉬프트로 가속 중일 때는(체력 대신) 연료가 더 빨리 줄어듦
    void HandleFuelDrain()
    {
        VehicleCatalog.VehicleDefinition def = VehicleCatalog.GetActive();
        if (def == null || !def.needsFuel)
        {
            return;
        }

        float rate = def.fuelDrainPerSecond;
        if (playerMovement != null && playerMovement.IsBoosting)
        {
            rate *= boostFuelDrainMultiplier;
        }

        VehicleCatalog.ConsumeFuel(def.id, rate * Time.deltaTime);

        if (VehicleCatalog.GetFuel(def.id) <= 0f)
        {
            Debug.Log("연료가 떨어졌어요! 자동으로 내립니다. 주유소에서 채우세요.");
            Dismount();
        }
    }

    public void Mount()
    {
        if (IsMounted || playerTransform == null || playerMovement == null)
        {
            return;
        }

        IsMounted = true;

        // 실제로 플레이어 자식으로 붙여서 위치/회전을 그대로 따라가게 함
        transform.SetParent(playerTransform, false);
        transform.localPosition = mountLocalPosition;
        transform.localRotation = Quaternion.Euler(mountLocalEulerAngles);

        // 탑승 중에는 트리거가 계속 겹쳐서 불필요한 이벤트가 나지 않도록 꺼둠
        if (col != null)
        {
            col.enabled = false;
        }

        VehicleCatalog.VehicleDefinition def = VehicleCatalog.GetActive();
        playerMovement.SetRiding(true, def.walkSpeed, def.runSpeed, def.needsFuel);
        Debug.Log(def.displayName + " 탑승! 실제로 타고 이동합니다.");
    }

    public void Dismount()
    {
        if (!IsMounted || playerTransform == null)
        {
            return;
        }

        IsMounted = false;

        // 부모에서 풀되 현재 월드 위치(플레이어 옆)를 유지한 뒤, 앞쪽으로 살짝 떨어뜨림
        transform.SetParent(originalParent, true);
        transform.position = playerTransform.position + playerTransform.forward * dismountOffset;
        transform.rotation = Quaternion.LookRotation(playerTransform.forward, Vector3.up);

        if (col != null)
        {
            col.enabled = true;
        }

        if (playerMovement != null)
        {
            playerMovement.SetRiding(false);
        }

        Debug.Log(VehicleCatalog.GetActive().displayName + "에서 내림.");
    }

    // 핸드폰 UI의 "부르기" 버튼에서 호출 - 탑승 중이 아닐 때만 플레이어 앞으로 소환
    public void CallToPlayer()
    {
        if (IsMounted || playerTransform == null)
        {
            return;
        }

        transform.SetParent(originalParent, true);
        transform.position = playerTransform.position + playerTransform.forward * callDistance;
        transform.rotation = Quaternion.LookRotation(playerTransform.forward, Vector3.up);
        Debug.Log("탈것을 불렀습니다.");
    }

    // 핸드폰 "탈것" 탭에서 호출 - 안 보이면 플레이어 앞에 불러오고, 보이면 다시 치움
    // 타는 중에는 무시함(타고 있는데 사라지면 안 되니까)
    public bool IsVisible => gameObject.activeSelf;

    public void ToggleVisibility()
    {
        if (IsMounted)
        {
            return;
        }

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
            Debug.Log("탈것을 보관했습니다.");
        }
        else
        {
            gameObject.SetActive(true);
            CallToPlayer();
        }
    }
}
