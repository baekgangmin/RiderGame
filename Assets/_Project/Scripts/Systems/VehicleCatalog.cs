using System.Collections.Generic;
using UnityEngine;

// 탈것 종류 정의 + 보유/장비 상태 관리 (M9) - 자전거/킥보드/오토바이/자동차
// 실제 탈것 오브젝트는 씬에 하나뿐이라(VehicleMount), 여기서는 "뭘 갖고 있고 지금 뭘 장비했는지"만 관리하고
// VehicleMount가 이 정보를 보고 자기 모양(크기/색)과 속도를 바꿈
public static class VehicleCatalog
{
    public class VehicleDefinition
    {
        public string id;
        public string displayName;
        public int price;
        public float walkSpeed;
        public float runSpeed;
        public float sizeScale; // 원래 크기 대비 배율 - 오토바이/자동차는 더 크게 보이도록
        public Color bodyColor;
        public bool needsFuel;       // 오토바이/자동차부터 true - 자전거/킥보드는 연료 없이 무제한
        public float maxFuel;
        public float fuelDrainPerSecond; // 탑승 중일 때 초당 소모량

        // 탑승 시 플레이어 기준 위치/회전(로컬) - 탈것마다 크기/모양이 달라서 자전거 기준값을 기본값으로 두고
        // 전용 3D 모델이 있는 탈것은 실제로 타 본 모습을 보고 하나씩 맞춤 (VehicleMount.Mount 참고)
        public Vector3 mountLocalPosition = new Vector3(0f, -0.35f, 0.2f);
        public Vector3 mountLocalEulerAngles = Vector3.zero;
    }

    public static readonly List<VehicleDefinition> All = new List<VehicleDefinition>
    {
        new VehicleDefinition { id = "bike", displayName = "자전거", price = 3000, walkSpeed = 6.5f, runSpeed = 9.5f, sizeScale = 1f, bodyColor = new Color(0.2f, 0.75f, 0.3f), needsFuel = false },
        // 탑승 위치는 실제로 캐릭터를 태워보면서(에디터에서 Bicycle을 Player 자식으로 붙여 눈으로 확인) 맞춘 값 -
        // 발이 발판에 오도록 y를 낮춤
        new VehicleDefinition { id = "kickboard", displayName = "킥보드", price = 8000, walkSpeed = 8f, runSpeed = 11f, sizeScale = 1f, bodyColor = new Color(1f, 0.6f, 0.1f), needsFuel = false,
            mountLocalPosition = new Vector3(0f, -0.346f, 0f), mountLocalEulerAngles = Vector3.zero },
        // 스쿠터 - 실제 배달 라이더들이 가장 많이 타는 탈것이라 여기 추가함. 연료를 쓰는 것 중엔 가장 저렴하고 연료도 아낌
        // 전용 모델 그대로(기본값 -0.35) 태워보니 자연스러웠음 - 명시적으로 남겨서 다른 탈것들과 같은 방식으로 관리
        new VehicleDefinition { id = "scooter", displayName = "스쿠터", price = 15000, walkSpeed = 9.5f, runSpeed = 14f, sizeScale = 1.15f, bodyColor = new Color(0.15f, 0.75f, 0.65f), needsFuel = true, maxFuel = 100f, fuelDrainPerSecond = 0.3f,
            mountLocalPosition = new Vector3(0f, -0.35f, 0.2f), mountLocalEulerAngles = Vector3.zero },
        // 기본값(-0.35)을 그대로 쓰면 캐릭터가 오토바이 몸체 안에 파묻혀 보여서, 더 아래로(-0.55) 내려서
        // 캐릭터가 몸체 위에 걸터앉은 것처럼 보이게 맞춤
        new VehicleDefinition { id = "motorcycle", displayName = "오토바이", price = 25000, walkSpeed = 11f, runSpeed = 16f, sizeScale = 1.3f, bodyColor = new Color(0.85f, 0.15f, 0.15f), needsFuel = true, maxFuel = 100f, fuelDrainPerSecond = 0.5f,
            mountLocalPosition = new Vector3(0f, -0.55f, 0.2f), mountLocalEulerAngles = Vector3.zero },
        // 자동차는 완전히 막힌 차체라 캐릭터가 어차피 거의 안 보임(문제될 정도로 튀어나오진 않아서 기본값 유지)
        new VehicleDefinition { id = "car", displayName = "자동차", price = 60000, walkSpeed = 14.5f, runSpeed = 21f, sizeScale = 1.8f, bodyColor = new Color(0.2f, 0.45f, 0.9f), needsFuel = true, maxFuel = 100f, fuelDrainPerSecond = 0.4f,
            mountLocalPosition = new Vector3(0f, -0.35f, 0.2f), mountLocalEulerAngles = Vector3.zero },
        // 스포츠카 - 최고급 탈것. 제일 빠르지만 비싸고 연료도 제일 빨리 닳음(작은 연료탱크 + 높은 소모율)
        // 기본값(-0.35)은 캐릭터가 차체 위로 붕 떠 보였음 - 좌석 쪽으로 낮춰서(-0.20) 차체 안에 앉은 것처럼 보이게 함
        // (오픈탑이라 워낙 좌석이 낮고 차체가 막혀 있어서, 위로 안 뜨는 대신 살짝 가려 보이는 정도가 최선이었음)
        new VehicleDefinition { id = "sportscar", displayName = "스포츠카", price = 120000, walkSpeed = 17f, runSpeed = 25f, sizeScale = 1.9f, bodyColor = new Color(1f, 0.85f, 0f), needsFuel = true, maxFuel = 80f, fuelDrainPerSecond = 0.6f,
            mountLocalPosition = new Vector3(0f, -0.20f, 0.2f), mountLocalEulerAngles = Vector3.zero },
    };

    private static readonly HashSet<string> owned = new HashSet<string>();
    private static readonly Dictionary<string, float> fuelLevels = new Dictionary<string, float>();

    public static string ActiveId { get; private set; } = "bike";

    // Play를 누를 때마다 항상 "아무것도 안 산" 깨끗한 상태로 시작 - 자전거도 상점에서 사야 함
    // static이라 Reload Domain이 꺼져있으면 이전 세션 값이 남을 수 있음
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnPlayStart()
    {
        owned.Clear();
        ActiveId = "bike"; // 아직 아무것도 못 샀어도 기본 모양(자전거) 기준으로 표시되게만 해둠 - 실제로 부르려면 사야 함

        fuelLevels.Clear();
        foreach (VehicleDefinition def in All)
        {
            if (def.needsFuel)
            {
                fuelLevels[def.id] = def.maxFuel;
            }
        }
    }

    public static float GetFuel(string id)
    {
        return fuelLevels.TryGetValue(id, out float value) ? value : 0f;
    }

    // 연료가 필요 없는 탈것은 항상 100%로 취급 (UI에서 굳이 분기 안 해도 되게)
    public static float GetFuelPercent(string id)
    {
        VehicleDefinition def = GetById(id);
        if (def == null || !def.needsFuel || def.maxFuel <= 0f)
        {
            return 1f;
        }
        return Mathf.Clamp01(GetFuel(id) / def.maxFuel);
    }

    public static void ConsumeFuel(string id, float amount)
    {
        if (!fuelLevels.ContainsKey(id))
        {
            return;
        }
        fuelLevels[id] = Mathf.Max(0f, fuelLevels[id] - amount);
    }

    // 주유소에서 호출 - 최대치를 넘지 않게 채움
    public static void AddFuel(string id, float amount)
    {
        VehicleDefinition def = GetById(id);
        if (def == null || !def.needsFuel)
        {
            return;
        }
        fuelLevels[id] = Mathf.Min(def.maxFuel, GetFuel(id) + amount);
    }

    public static bool IsOwned(string id)
    {
        return owned.Contains(id);
    }

    public static VehicleDefinition GetById(string id)
    {
        return All.Find(d => d.id == id);
    }

    public static VehicleDefinition GetActive()
    {
        return GetById(ActiveId) ?? All[0];
    }

    // 상점에서 구매 - 성공하면 true
    public static bool Buy(VehicleDefinition def)
    {
        if (def == null || IsOwned(def.id))
        {
            return false;
        }

        if (!DeliveryManager.SpendMoney(def.price))
        {
            Debug.Log("돈이 부족해요.");
            return false;
        }

        owned.Add(def.id);
        Debug.Log(def.displayName + " 구매 완료!");
        return true;
    }

    // 소유한 탈것 중 하나를 장비함 - VehicleMount가 이 값을 보고 모양/속도를 바꿈
    public static void SetActive(string id)
    {
        if (!IsOwned(id))
        {
            return;
        }

        ActiveId = id;
    }
}
