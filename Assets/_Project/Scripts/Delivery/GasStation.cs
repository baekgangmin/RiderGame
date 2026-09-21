using System.Collections;
using UnityEngine;

// 주유소 (M9) - 오토바이/자동차처럼 연료가 필요한 탈것을 타고 주유 구역 안에서 스페이스바를 누르면
// 결제 창(GasStationUI)이 뜨고, 결제하면 예상 소요 시간 동안 서서히 연료가 채워짐
// 캐노피(지붕)+기둥+주유기+표지판을 직접 만들어서 실제 주유소처럼 보이게 하고, 플레이어 시작 위치 근처에 스스로 자리를 잡음
[RequireComponent(typeof(Collider))]
public class GasStation : MonoBehaviour
{
    public static GasStation Instance;

    [Header("플레이어 태그")]
    public string playerTag = "Player";

    [Header("플레이어 시작 위치 기준 배치 오프셋 (오른쪽 / 앞쪽)")]
    public float spawnOffsetRight = 6f;
    public float spawnOffsetForward = 4f;

    [Header("바닥 높이 (Y) - 플레이어 피벗이 발밑이 아닐 수 있어서 Y는 따로 고정함")]
    public float groundY = 0f;

    [Header("주유 속도(연료 단위/초) / 가격(원/단위) - 결제 금액·소요 시간 계산에 쓰임")]
    public float refuelUnitsPerSecond = 25f;
    public float pricePerFuelUnit = 20f;

    [Header("결제 창을 여는 키")]
    public KeyCode openKey = KeyCode.Space;

    [Header("색상")]
    public Color accentColor = new Color(0.2f, 0.85f, 0.9f); // 표지판/미니맵 색 - 픽업(노랑)/배달(파랑)과 겹치지 않게

    private bool playerInZone;
    private bool isRefueling;
    private Coroutine refuelRoutine;
    private Renderer blipRenderer;
    private Renderer signRenderer;

    void Awake()
    {
        Instance = this;
        PositionAtPlayerSpawn();

        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        BuildStation();
    }

    // Box Collider를 추가한 뒤 이 스크립트를 붙이면(또는 Reset) 주유 구역 크기를 건물 크기에 맞게 자동으로 잡아줌
    void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        BoxCollider box = col as BoxCollider;
        if (box != null)
        {
            box.size = new Vector3(10f, 6f, 8f);
            box.center = new Vector3(0f, 3f, 0f);
        }
    }

    void PositionAtPlayerSpawn()
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag(playerTag);
        if (playerGO == null)
        {
            return;
        }

        Transform playerTransform = playerGO.transform;
        Vector3 pos = playerTransform.position
            + playerTransform.right * spawnOffsetRight
            + playerTransform.forward * spawnOffsetForward;

        // 플레이어 오브젝트의 피벗이 발밑이 아니라 캡슐 중심 등 땅에서 살짝 뜬 위치일 수 있어서
        // 플레이어의 Y값을 그대로 쓰지 않고 바닥 높이로 고정함 - 기둥이 땅에서 뜨는 원인이었음
        pos.y = groundY;
        transform.position = pos;
    }

    void BuildStation()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Color canopyColor = new Color(0.82f, 0.82f, 0.86f);
        Color pumpColor = new Color(0.85f, 0.2f, 0.15f);

        // 지붕(캐노피)
        GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        canopy.name = "Canopy";
        canopy.transform.SetParent(transform);
        canopy.transform.localPosition = new Vector3(0f, 4.2f, 0f);
        canopy.transform.localScale = new Vector3(10f, 0.4f, 6f);
        StripCollider(canopy);
        ApplyColor(canopy.GetComponent<Renderer>(), canopyColor, shader);

        // 지붕을 받치는 기둥 4개
        Vector3[] pillarOffsets =
        {
            new Vector3(-4.3f, 2f, -2.3f),
            new Vector3(4.3f, 2f, -2.3f),
            new Vector3(-4.3f, 2f, 2.3f),
            new Vector3(4.3f, 2f, 2.3f),
        };
        foreach (Vector3 offset in pillarOffsets)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Pillar";
            pillar.transform.SetParent(transform);
            pillar.transform.localPosition = offset;
            pillar.transform.localScale = new Vector3(0.3f, 2f, 0.3f);
            StripCollider(pillar);
            ApplyColor(pillar.GetComponent<Renderer>(), canopyColor, shader);
        }

        // 주유기
        GameObject pump = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pump.name = "Pump";
        pump.transform.SetParent(transform);
        pump.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        pump.transform.localScale = new Vector3(1f, 1.2f, 0.6f);
        StripCollider(pump);
        ApplyColor(pump.GetComponent<Renderer>(), pumpColor, shader);

        // 멀리서도 보이는 표지판 (기존 배달 마커의 "기둥" 역할을 대신함)
        GameObject signPole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        signPole.name = "SignPole";
        signPole.transform.SetParent(transform);
        signPole.transform.localPosition = new Vector3(-5.3f, 3f, -3f);
        signPole.transform.localScale = new Vector3(0.15f, 3f, 0.15f);
        StripCollider(signPole);
        ApplyColor(signPole.GetComponent<Renderer>(), canopyColor, shader);

        GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.name = "Sign";
        sign.transform.SetParent(transform);
        sign.transform.localPosition = new Vector3(-5.3f, 6.3f, -3f);
        sign.transform.localScale = new Vector3(1.6f, 1f, 0.3f);
        StripCollider(sign);
        signRenderer = sign.GetComponent<Renderer>();
        ApplyColor(signRenderer, accentColor, shader);

        // 미니맵 전용 원반 - DeliverySpot 마커와 같은 방식으로 위에서 내려다볼 때 크게 찍힘
        GameObject blip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        blip.name = "GasStationBlip";
        blip.transform.SetParent(transform);
        blip.transform.localPosition = new Vector3(0f, 30f, 0f);
        blip.transform.localScale = new Vector3(5f, 0.15f, 5f);
        StripCollider(blip);
        blipRenderer = blip.GetComponent<Renderer>();
        ApplyColor(blipRenderer, accentColor, shader);
    }

    private void StripCollider(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }
    }

    private void ApplyColor(Renderer targetRenderer, Color c, Shader shader)
    {
        if (targetRenderer == null)
        {
            return;
        }

        Material mat = shader != null ? new Material(shader) : targetRenderer.material;
        targetRenderer.material = mat;

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", c);
        }
        else
        {
            mat.color = c;
        }

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", c * 1.3f);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInZone = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        playerInZone = false;

        // 주유소 구역을 벗어나면 결제 창을 닫고, 주유 중이었다면 중단함
        if (GasStationUI.Instance != null)
        {
            GasStationUI.Instance.Close();
        }
        if (isRefueling && refuelRoutine != null)
        {
            StopCoroutine(refuelRoutine);
            refuelRoutine = null;
            isRefueling = false;
        }
    }

    void Update()
    {
        if (!playerInZone || isRefueling || !Input.GetKeyDown(openKey))
        {
            return;
        }

        if (VehicleMount.Instance == null || !VehicleMount.Instance.IsMounted)
        {
            Debug.Log("탈것을 타고 주유소 안에서 스페이스바를 눌러주세요.");
            return;
        }

        VehicleCatalog.VehicleDefinition def = VehicleCatalog.GetActive();
        if (def == null || !def.needsFuel)
        {
            Debug.Log("지금 탄 탈것은 연료가 필요 없어요.");
            return;
        }

        float missing = def.maxFuel - VehicleCatalog.GetFuel(def.id);
        if (missing <= 0.01f)
        {
            Debug.Log("연료가 이미 가득 찼어요.");
            return;
        }

        int cost = Mathf.CeilToInt(missing * pricePerFuelUnit);
        float duration = missing / refuelUnitsPerSecond;

        if (GasStationUI.Instance != null)
        {
            GasStationUI.Instance.Open(def, missing, cost, duration);
        }
    }

    // 결제 창의 "결제하기" 버튼을 누르면 GasStationUI에서 호출함
    public void StartRefuel(string vehicleId, float amount, int cost, float duration)
    {
        if (isRefueling)
        {
            return;
        }

        if (!DeliveryManager.SpendMoney(cost))
        {
            if (GasStationUI.Instance != null)
            {
                GasStationUI.Instance.ShowMessage("돈이 부족해요!");
            }
            return;
        }

        refuelRoutine = StartCoroutine(RefuelRoutine(vehicleId, amount, duration));
    }

    // 결제한 금액만큼의 연료를 예상 소요 시간 동안 서서히 채우고, 매 프레임 남은 시간을 UI에 알려줌
    private IEnumerator RefuelRoutine(string vehicleId, float totalAmount, float duration)
    {
        isRefueling = true;
        duration = Mathf.Max(0.1f, duration);
        float amountPerSecond = totalAmount / duration;
        float remaining = duration;

        while (remaining > 0f)
        {
            float step = Mathf.Min(Time.deltaTime, remaining);
            VehicleCatalog.AddFuel(vehicleId, amountPerSecond * step);
            remaining -= step;

            if (GasStationUI.Instance != null)
            {
                GasStationUI.Instance.UpdateRefuelProgress(remaining);
            }

            yield return null;
        }

        isRefueling = false;
        refuelRoutine = null;

        if (GasStationUI.Instance != null)
        {
            GasStationUI.Instance.ShowComplete();
        }
    }
}
