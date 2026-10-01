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
    private Transform placeholderVisual;
    private Transform bikeModel;
    private Transform kickboardModel;
    private Transform scooterModel;
    private Transform motorcycleModel;
    private Transform carModel;
    private Transform sportscarModel;

    void Awake()
    {
        Instance = this;
        col = GetComponent<Collider>();
        originalParent = transform.parent;
        placeholderVisual = transform.Find("PlaceholderVisual");
        bikeModel = transform.Find("BikeModel");
        kickboardModel = transform.Find("KickboardModel");
        scooterModel = transform.Find("ScooterModel");
        motorcycleModel = transform.Find("MotorcycleModel");
        carModel = transform.Find("CarModel");
        sportscarModel = transform.Find("SportscarModel");

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

    // id에 해당하는 전용 3D 모델을 반환 (없으면 null) - ApplyVisual/Mount 양쪽에서 같이 씀
    private Transform GetDedicatedModel(string id)
    {
        if (id == "bike") return bikeModel;
        if (id == "kickboard") return kickboardModel;
        if (id == "scooter") return scooterModel;
        if (id == "motorcycle") return motorcycleModel;
        if (id == "car") return carModel;
        if (id == "sportscar") return sportscarModel;
        return null;
    }

    // 현재 장비한 탈것 종류에 맞춰 모양을 바꿈 - 전용 3D 모델이 있는 탈것(자전거/킥보드/스쿠터/오토바이/자동차/스포츠카)은 그 모델을 보여주고,
    // 전용 모델이 없는 탈것은 예전처럼 PlaceholderVisual(원기둥)을 크기+색으로 구분해서 보여줌
    void ApplyVisual(VehicleCatalog.VehicleDefinition def)
    {
        if (def == null)
        {
            return;
        }

        Transform activeModel = GetDedicatedModel(def.id);

        bool useDedicatedModel = activeModel != null;

        if (bikeModel != null)
        {
            bikeModel.gameObject.SetActive(activeModel == bikeModel);
        }
        if (kickboardModel != null)
        {
            kickboardModel.gameObject.SetActive(activeModel == kickboardModel);
        }
        if (scooterModel != null)
        {
            scooterModel.gameObject.SetActive(activeModel == scooterModel);
        }
        if (motorcycleModel != null)
        {
            motorcycleModel.gameObject.SetActive(activeModel == motorcycleModel);
        }
        if (carModel != null)
        {
            carModel.gameObject.SetActive(activeModel == carModel);
        }
        if (sportscarModel != null)
        {
            sportscarModel.gameObject.SetActive(activeModel == sportscarModel);
        }
        if (placeholderVisual != null)
        {
            placeholderVisual.gameObject.SetActive(!useDedicatedModel);
        }

        // 전용 모델(자전거/킥보드/스쿠터/오토바이/자동차/스포츠카)은 각자의 XModelScale로 이미 실제 크기가 맞춰져 있어서
        // 여기서 sizeScale까지 또 곱하면 중복 확대됨(예: 스포츠카는 1.9배 더 커짐) - 전용 모델이 없는 임시 도형에만 sizeScale 적용
        transform.localScale = useDedicatedModel ? Vector3.one : Vector3.one * def.sizeScale;

        if (!useDedicatedModel && placeholderVisual != null)
        {
            Renderer[] renderers = placeholderVisual.GetComponentsInChildren<Renderer>();
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

        // 연료가 이미 0인 채로 탑승했다면(주유소까지 타고 가야 하는 상황) 강제로 안 내리게 함 -
        // 원래는 탑승하자마자 다음 프레임에 "연료 없음"으로 판정돼서 바로 다시 내려졌고(연료를 넣으러
        // 갈 수조차 없는 문제), 타는 중에 다 떨어진 경우에만(아래 로직) 자동으로 내리는 게 맞는 동작임
        if (VehicleCatalog.GetFuel(def.id) <= 0f)
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

        VehicleCatalog.VehicleDefinition mountDef = VehicleCatalog.GetActive();

        // 플레이어(부모)가 축소되어 있으면(예: 0.5배) 자식으로 붙는 탈것의 "겉보기 위치/크기"도 그만큼 같이 줄어듦.
        // mountLocalPosition/mountDef.sizeScale은 "월드 기준" 값으로 다루고 싶어서, 부모 스케일로 나눠 상쇄함 -
        // 그래야 부르기 상태(월드 스케일 그대로)와 탑승 상태의 겉보기 위치/크기가 항상 똑같아 보임
        transform.SetParent(playerTransform, false);
        Vector3 parentScale = playerTransform.lossyScale;
        Vector3 mountLocalPosition = mountDef.mountLocalPosition;
        transform.localPosition = new Vector3(
            mountLocalPosition.x / Mathf.Max(0.0001f, parentScale.x),
            mountLocalPosition.y / Mathf.Max(0.0001f, parentScale.y),
            mountLocalPosition.z / Mathf.Max(0.0001f, parentScale.z));
        transform.localRotation = Quaternion.Euler(mountDef.mountLocalEulerAngles);
        // ApplyVisual과 동일한 이유로, 전용 모델이 있으면 sizeScale을 또 곱하지 않음(중복 확대 방지) -
        // 그래야 탑승 중 크기가 부르기 상태(ApplyVisual)와 똑같이 보임
        bool mountedUsesDedicatedModel = GetDedicatedModel(mountDef.id) != null;
        float effectiveSizeScale = mountedUsesDedicatedModel ? 1f : mountDef.sizeScale;
        transform.localScale = new Vector3(
            effectiveSizeScale / Mathf.Max(0.0001f, parentScale.x),
            effectiveSizeScale / Mathf.Max(0.0001f, parentScale.y),
            effectiveSizeScale / Mathf.Max(0.0001f, parentScale.z));

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
