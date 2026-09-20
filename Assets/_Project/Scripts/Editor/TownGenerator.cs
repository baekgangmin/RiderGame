using UnityEngine;
using UnityEditor;

// 씬 전체 자동 세팅 (Player 태그, 카메라, 스태미나 시스템, 마을 블록아웃, 배달 스팟, 자전거, 폰 UI)
// 유니티 상단 메뉴 "RiderGame > Generate Town + Setup Scene" 눌러서 실행
public static class TownGenerator
{
    private const float BlockSize = 10f;
    private const float StreetWidth = 6f;
    private const int GridSize = 5;

    [MenuItem("RiderGame/Generate Town + Setup Scene")]
    public static void GenerateTown()
    {
        SetupPlayerAndCamera();
        SetupManagers();
        Transform townParent = SetupGround();
        BuildTownGrid(townParent);
        SetupBicycle();

        EditorUtility.DisplayDialog("Town Generator", "씬 세팅 + 마을 생성 완료!\nPlay를 누르면 배달 스팟이 랜덤으로 배정됩니다.\nCtrl+S로 저장하는 것 잊지 마세요.", "확인");
    }

    private static void SetupPlayerAndCamera()
    {
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
        }

        GameObject mainCam = GameObject.Find("Main Camera");
        if (mainCam != null)
        {
            CameraFollow follow = mainCam.GetComponent<CameraFollow>();
            if (follow == null)
            {
                follow = mainCam.AddComponent<CameraFollow>();
            }
            if (player != null)
            {
                follow.target = player.transform;
            }
        }
    }

    private static void SetupManagers()
    {
        GameObject managers = GameObject.Find("Managers");
        if (managers == null)
        {
            managers = new GameObject("Managers");
        }
        if (managers.GetComponent<StaminaSystem>() == null)
        {
            managers.AddComponent<StaminaSystem>();
        }
        if (managers.GetComponent<StaminaUI>() == null)
        {
            managers.AddComponent<StaminaUI>();
        }
        if (managers.GetComponent<DeliveryJobManager>() == null)
        {
            managers.AddComponent<DeliveryJobManager>();
        }
        if (managers.GetComponent<PhoneUI>() == null)
        {
            managers.AddComponent<PhoneUI>();
        }
    }

    private static Transform SetupGround()
    {
        GameObject ground = GameObject.Find("Ground");
        float worldSize = GridSize * (BlockSize + StreetWidth) + StreetWidth;

        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
        }

        ground.transform.position = new Vector3(0f, 0f, 0f);
        ground.transform.localScale = new Vector3(worldSize / 10f, 1f, worldSize / 10f);

        GameObject town = GameObject.Find("Town");
        if (town != null)
        {
            Object.DestroyImmediate(town);
        }
        town = new GameObject("Town");
        return town.transform;
    }

    private static void BuildTownGrid(Transform townParent)
    {
        float step = BlockSize + StreetWidth;
        float half = (GridSize - 1) * step * 0.5f;
        int centerIndex = GridSize / 2;

        for (int row = 0; row < GridSize; row++)
        {
            for (int col = 0; col < GridSize; col++)
            {
                // 정중앙은 플레이어 스폰 자리로 비워둠
                if (row == centerIndex && col == centerIndex)
                {
                    continue;
                }

                float x = col * step - half;
                float z = row * step - half;
                float height = Random.Range(4f, 14f);

                GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
                building.name = "Building_" + row + "_" + col;
                building.transform.SetParent(townParent);
                building.transform.position = new Vector3(x, height * 0.5f, z);
                building.transform.localScale = new Vector3(BlockSize, height, BlockSize);

                // 모든 건물이 배달 스팟 후보가 됨 - 실제 픽업/배달 지정은 DeliveryJobManager가 플레이 중 랜덤으로 함
                AttachDeliverySpot(building, townParent);
            }
        }
    }

    private static void AttachDeliverySpot(GameObject building, Transform townParent)
    {
        Vector3 basePos = building.transform.position;
        basePos.y = 0.3f;
        float halfDepth = building.transform.localScale.z * 0.5f;
        Vector3 zonePos = basePos + new Vector3(0f, 0f, -(halfDepth + 1.5f));

        GameObject zone = new GameObject(building.name + "_Spot");
        zone.transform.SetParent(townParent);
        zone.transform.position = zonePos;

        BoxCollider box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(3f, 2f, 3f);

        zone.AddComponent<DeliverySpot>();
    }

    private static void SetupBicycle()
    {
        GameObject bicycle = GameObject.Find("Bicycle");
        if (bicycle == null)
        {
            bicycle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bicycle.name = "Bicycle";
        }

        bicycle.transform.position = new Vector3(2f, 0.4f, 2f);
        bicycle.transform.localScale = new Vector3(0.5f, 0.8f, 1.2f);

        CapsuleCollider capsule = bicycle.GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            Object.DestroyImmediate(capsule);
        }

        BoxCollider box = bicycle.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = bicycle.AddComponent<BoxCollider>();
        }
        box.isTrigger = true;
        box.size = new Vector3(2f, 2f, 2f);

        if (bicycle.GetComponent<VehicleMount>() == null)
        {
            bicycle.AddComponent<VehicleMount>();
        }
    }
}
