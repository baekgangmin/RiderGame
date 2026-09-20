using UnityEngine;
using UnityEditor;
using UnityEditor.AI;

// 씬 전체 자동 세팅 (Player 태그, 카메라, 스태미나 시스템, 마을 블록아웃, 배달 스팟, 자전거, 폰 UI)
// 유니티 상단 메뉴 "RiderGame > Generate Town + Setup Scene" 눌러서 실행
public static class TownGenerator
{
    private const float BlockSize = 10f;
    private const float StreetWidth = 6f;
    private const int GridSize = 5;

    private const float SidewalkMargin = 1.4f; // 차도 양옆에 남겨두는 인도 폭
    private const int MainRoadGapIndex = 3;    // 이 가로줄을 참고 지도의 큰 도로(32번 도로 느낌)로 강조

    private const float WarpAmplitude = 1.0f;    // 격자 교차점을 얼마나 흔들어서 도로를 구불구불하게 만들지(미터)
    private const float BlockRotationRange = 6f; // 블록 전체를 얼마나 회전시켜서 불규칙한 모양으로 보이게 할지(도)

    private static readonly Vector2Int ParkCell = new Vector2Int(0, 1); // 공원 블록 (참고 지도의 갈마공원 느낌)
    private static readonly Vector2Int PondCell = new Vector2Int(4, 3); // 연못 블록 (참고 지도의 월평공원 호수 느낌)

    // 실제 지도(참고 이미지) 느낌 - 밝은 인도 바닥 위에 어두운 차도가 지나가고, 베이지/황토색 건물들이 촘촘하게 들어참
    private static readonly Color GroundColor = new Color(0.90f, 0.88f, 0.84f);   // 인도/보도
    private static readonly Color RoadColor = new Color(0.30f, 0.30f, 0.32f);     // 일반 차도 아스팔트
    private static readonly Color MainRoadColor = new Color(0.24f, 0.24f, 0.26f); // 메인 도로 (더 진하고 넓게)
    private static readonly Color LineWhite = new Color(0.92f, 0.92f, 0.90f);
    private static readonly Color LineYellow = new Color(0.95f, 0.78f, 0.15f);
    private static readonly Color ParkColor = new Color(0.42f, 0.62f, 0.35f);
    private static readonly Color WaterColor = new Color(0.35f, 0.55f, 0.75f);

    private static readonly Color[] BuildingPalette = new Color[]
    {
        new Color(0.82f, 0.73f, 0.60f), // 베이지
        new Color(0.78f, 0.68f, 0.55f), // 짙은 베이지
        new Color(0.86f, 0.80f, 0.70f), // 밝은 회베이지
        new Color(0.74f, 0.66f, 0.60f), // 회갈색
        new Color(0.80f, 0.76f, 0.72f), // 연회색
    };

    [MenuItem("RiderGame/Generate Town + Setup Scene")]
    public static void GenerateTown()
    {
        SetupPlayerAndCamera();
        SetupManagers();
        Transform townParent = SetupGround();

        // 격자 교차점을 살짝씩 흔들어서(warp) 도로/블록이 완전히 반듯하지 않고 구불구불·불규칙하게 보이게 함
        Vector3[,] nodes = BuildNodeGrid();

        BuildTownGrid(townParent, nodes);
        BuildRoadNetwork(townParent, nodes);
        SetupBicycle();
        BakeNavMesh();

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
        if (managers.GetComponent<RouteGuide>() == null)
        {
            managers.AddComponent<RouteGuide>();
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

        ApplyColor(ground, GroundColor);
        MarkNavStatic(ground);

        GameObject town = GameObject.Find("Town");
        if (town != null)
        {
            Object.DestroyImmediate(town);
        }
        town = new GameObject("Town");
        return town.transform;
    }

    // 도로 교차점(격자 마디) 좌표를 미리 계산 - 가장자리는 흔들지 않고, 안쪽 마디들만 살짝씩 랜덤하게 흔들어서
    // 도로가 완전 직선이 아니라 구불구불하게, 블록도 완전한 정사각형 격자가 아니게 만듦
    private static Vector3[,] BuildNodeGrid()
    {
        float step = BlockSize + StreetWidth;
        float half = (GridSize - 1) * step * 0.5f;

        Vector3[,] nodes = new Vector3[GridSize + 1, GridSize + 1];

        for (int col = 0; col <= GridSize; col++)
        {
            for (int row = 0; row <= GridSize; row++)
            {
                float baseX = -half - step * 0.5f + col * step;
                float baseZ = -half - step * 0.5f + row * step;

                bool isBoundary = col == 0 || col == GridSize || row == 0 || row == GridSize;
                float offsetX = isBoundary ? 0f : Random.Range(-WarpAmplitude, WarpAmplitude);
                float offsetZ = isBoundary ? 0f : Random.Range(-WarpAmplitude, WarpAmplitude);

                nodes[col, row] = new Vector3(baseX + offsetX, 0f, baseZ + offsetZ);
            }
        }

        return nodes;
    }

    private static void BuildTownGrid(Transform townParent, Vector3[,] nodes)
    {
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

                // 블록 네 꼭짓점(흔들린 도로 교차점)의 평균 = 이 블록의 실제 중심
                Vector3 corner00 = nodes[col, row];
                Vector3 corner10 = nodes[col + 1, row];
                Vector3 corner01 = nodes[col, row + 1];
                Vector3 corner11 = nodes[col + 1, row + 1];
                Vector3 blockCenter = (corner00 + corner10 + corner01 + corner11) * 0.25f;

                Vector2Int cell = new Vector2Int(row, col);

                if (cell == ParkCell)
                {
                    BuildParkBlock(townParent, blockCenter);
                    continue;
                }

                if (cell == PondCell)
                {
                    BuildPondBlock(townParent, blockCenter);
                    continue;
                }

                string label = row + "_" + col;

                // 블록 하나를 실제 지도처럼 여러 채의 작은 건물로 쪼개서 배치 (사이사이 골목 틈도 생김)
                BuildBlockCluster(townParent, blockCenter, label);

                // 모든 블록이 배달 스팟 후보가 됨 - 실제 픽업/배달 지정은 DeliveryJobManager가 플레이 중 랜덤으로 함
                AttachDeliverySpot(blockCenter, townParent, label);
            }
        }
    }

    // 블록 하나(BlockSize x BlockSize) 안에 들어갈 건물 배치 템플릿 - 큰 건물 하나 / 좌우 2채 / 앞뒤 2채 / 4분할 중 랜덤 선택
    private struct BuildingFootprint
    {
        public float OffsetX;
        public float OffsetZ;
        public float SizeX;
        public float SizeZ;

        public BuildingFootprint(float ox, float oz, float sx, float sz)
        {
            OffsetX = ox;
            OffsetZ = oz;
            SizeX = sx;
            SizeZ = sz;
        }
    }

    private static BuildingFootprint[] GetTemplateFootprints(int template)
    {
        switch (template)
        {
            case 0: // 큰 건물 하나 (학교/관공서 느낌)
                return new BuildingFootprint[]
                {
                    new BuildingFootprint(0f, 0f, 0.82f, 0.82f),
                };
            case 1: // 좌우로 2채
                return new BuildingFootprint[]
                {
                    new BuildingFootprint(-0.23f, 0f, 0.40f, 0.86f),
                    new BuildingFootprint(0.23f, 0f, 0.40f, 0.86f),
                };
            case 2: // 앞뒤로 2채
                return new BuildingFootprint[]
                {
                    new BuildingFootprint(0f, -0.23f, 0.86f, 0.40f),
                    new BuildingFootprint(0f, 0.23f, 0.86f, 0.40f),
                };
            default: // 4분할 - 작은 건물 여러 채 (실제 지도의 밀집된 느낌)
                return new BuildingFootprint[]
                {
                    new BuildingFootprint(-0.23f, -0.23f, 0.40f, 0.40f),
                    new BuildingFootprint(0.23f, -0.23f, 0.40f, 0.40f),
                    new BuildingFootprint(-0.23f, 0.23f, 0.40f, 0.40f),
                    new BuildingFootprint(0.23f, 0.23f, 0.40f, 0.40f),
                };
        }
    }

    private static void BuildBlockCluster(Transform townParent, Vector3 blockCenter, string label)
    {
        int template = Random.Range(0, 4);
        BuildingFootprint[] footprints = GetTemplateFootprints(template);

        // 블록 전체를 살짝 회전시켜서 격자에 딱 맞아떨어지지 않는 불규칙한 느낌을 줌
        float blockRotationDeg = Random.Range(-BlockRotationRange, BlockRotationRange);
        Quaternion blockRotation = Quaternion.Euler(0f, blockRotationDeg, 0f);

        for (int i = 0; i < footprints.Length; i++)
        {
            BuildingFootprint fp = footprints[i];

            // 완전히 똑같은 모양이 반복되지 않도록 위치/크기를 살짝 흔들어줌 (겹치지 않게 항상 살짝 작아지는 쪽으로)
            float jitterX = Random.Range(-0.02f, 0.02f);
            float jitterZ = Random.Range(-0.02f, 0.02f);
            float sizeJitter = Random.Range(-0.04f, 0.01f);

            float width = Mathf.Max(1.5f, (fp.SizeX + sizeJitter) * BlockSize);
            float depth = Mathf.Max(1.5f, (fp.SizeZ + sizeJitter) * BlockSize);

            Vector3 localOffset = new Vector3((fp.OffsetX + jitterX) * BlockSize, 0f, (fp.OffsetZ + jitterZ) * BlockSize);
            Vector3 rotatedOffset = blockRotation * localOffset;
            float height = Random.Range(3f, 14f);

            GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = "Building_" + label + "_" + i;
            building.transform.SetParent(townParent);
            building.transform.position = blockCenter + rotatedOffset + new Vector3(0f, height * 0.5f, 0f);
            building.transform.rotation = blockRotation;
            building.transform.localScale = new Vector3(width, height, depth);

            ApplyColor(building, RandomBuildingColor());
            MarkNavStatic(building);
        }
    }

    private static Color RandomBuildingColor()
    {
        Color baseColor = BuildingPalette[Random.Range(0, BuildingPalette.Length)];
        return new Color(
            Mathf.Clamp01(baseColor.r + Random.Range(-0.04f, 0.04f)),
            Mathf.Clamp01(baseColor.g + Random.Range(-0.04f, 0.04f)),
            Mathf.Clamp01(baseColor.b + Random.Range(-0.04f, 0.04f))
        );
    }

    private static void BuildParkBlock(Transform parent, Vector3 blockCenter)
    {
        GameObject park = GameObject.CreatePrimitive(PrimitiveType.Cube);
        park.name = "Park";
        park.transform.SetParent(parent);
        park.transform.position = new Vector3(blockCenter.x, 0.05f, blockCenter.z);
        park.transform.localScale = new Vector3(BlockSize * 0.95f, 0.1f, BlockSize * 0.95f);

        Collider parkCol = park.GetComponent<Collider>();
        if (parkCol != null)
        {
            Object.DestroyImmediate(parkCol);
        }

        ApplyColor(park, ParkColor);

        for (int i = 0; i < 4; i++)
        {
            float tx = blockCenter.x + Random.Range(-3.5f, 3.5f);
            float tz = blockCenter.z + Random.Range(-3.5f, 3.5f);
            BuildTree(parent, new Vector3(tx, 0f, tz));
        }
    }

    private static void BuildTree(Transform parent, Vector3 pos)
    {
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "TreeTrunk";
        trunk.transform.SetParent(parent);
        trunk.transform.position = pos + new Vector3(0f, 0.75f, 0f);
        trunk.transform.localScale = new Vector3(0.3f, 0.75f, 0.3f);

        Collider trunkCol = trunk.GetComponent<Collider>();
        if (trunkCol != null)
        {
            Object.DestroyImmediate(trunkCol);
        }

        ApplyColor(trunk, new Color(0.4f, 0.28f, 0.18f));

        GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leaves.name = "TreeLeaves";
        leaves.transform.SetParent(parent);
        leaves.transform.position = pos + new Vector3(0f, 1.9f, 0f);
        leaves.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);

        Collider leavesCol = leaves.GetComponent<Collider>();
        if (leavesCol != null)
        {
            Object.DestroyImmediate(leavesCol);
        }

        ApplyColor(leaves, new Color(0.25f, 0.5f, 0.22f));
    }

    private static void BuildPondBlock(Transform parent, Vector3 blockCenter)
    {
        GameObject pond = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pond.name = "Pond";
        pond.transform.SetParent(parent);
        pond.transform.position = new Vector3(blockCenter.x, 0.03f, blockCenter.z);
        pond.transform.localScale = new Vector3(BlockSize * 0.9f, 0.06f, BlockSize * 0.9f);

        Collider pondCol = pond.GetComponent<Collider>();
        if (pondCol != null)
        {
            Object.DestroyImmediate(pondCol);
        }

        ApplyColor(pond, WaterColor);
    }

    // 인도(Ground) 위에 차도 + 중앙선을 그려서 실제 도로처럼 보이게 함.
    // 각 도로를 하나의 긴 직선이 아니라 교차점(nodes)마다 잇는 여러 구간으로 그려서, 마디마다 살짝씩 꺾이며 구불구불해 보이게 함.
    // 한 줄은 "메인 도로"로 더 넓고 노란선으로 강조.
    private static void BuildRoadNetwork(Transform townParent, Vector3[,] nodes)
    {
        // 가로 방향 도로 (동서) - row 하나가 도로 한 줄
        for (int row = 0; row <= GridSize; row++)
        {
            bool isMain = (row == MainRoadGapIndex);
            for (int col = 0; col < GridSize; col++)
            {
                BuildRoadSegment(townParent, nodes[col, row], nodes[col + 1, row], isMain, "H" + row + "_" + col);
            }
        }

        // 세로 방향 도로 (남북) - col 하나가 도로 한 줄
        for (int col = 0; col <= GridSize; col++)
        {
            for (int row = 0; row < GridSize; row++)
            {
                BuildRoadSegment(townParent, nodes[col, row], nodes[col, row + 1], false, "V" + col + "_" + row);
            }
        }
    }

    private static void BuildRoadSegment(Transform parent, Vector3 from, Vector3 to, bool isMain, string label)
    {
        Vector3 diff = to - from;
        float length = diff.magnitude;
        if (length < 0.01f)
        {
            return;
        }

        Vector3 mid = (from + to) * 0.5f;
        float angle = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0f, angle, 0f);

        float roadWidth = isMain ? Mathf.Max(2.5f, StreetWidth - SidewalkMargin) : Mathf.Max(1.8f, StreetWidth - SidewalkMargin * 2f);
        float thickness = 0.06f;

        GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
        road.name = "Road_" + label;
        road.transform.SetParent(parent);
        road.transform.position = new Vector3(mid.x, thickness * 0.5f + 0.02f, mid.z);
        road.transform.rotation = rotation;
        road.transform.localScale = new Vector3(roadWidth, thickness, length);

        Collider roadCol = road.GetComponent<Collider>();
        if (roadCol != null)
        {
            Object.DestroyImmediate(roadCol);
        }

        ApplyColor(road, isMain ? MainRoadColor : RoadColor);

        // 중앙선(차선)
        GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = "Line_" + label;
        line.transform.SetParent(parent);
        float lineWidth = isMain ? 0.35f : 0.18f;
        line.transform.position = new Vector3(mid.x, thickness + 0.03f, mid.z);
        line.transform.rotation = rotation;
        line.transform.localScale = new Vector3(lineWidth, thickness, length);

        Collider lineCol = line.GetComponent<Collider>();
        if (lineCol != null)
        {
            Object.DestroyImmediate(lineCol);
        }

        ApplyColor(line, isMain ? LineYellow : LineWhite);
    }

    private static void ApplyColor(GameObject go, Color color)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material mat = shader != null ? new Material(shader) : new Material(renderer.sharedMaterial);

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else
        {
            mat.color = color;
        }

        renderer.sharedMaterial = mat;
    }

    // 배달 경로 안내(RouteGuide)가 NavMesh로 건물을 피해서 길을 찾을 수 있도록 지형/건물을 내비게이션 정적 오브젝트로 표시
    private static void MarkNavStatic(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic);
    }

    private static void BakeNavMesh()
    {
        NavMeshBuilder.BuildNavMesh();
        Debug.Log("NavMesh 베이크 완료 - 배달 경로 안내선이 이 위에서 동작합니다.");
    }

    private static void AttachDeliverySpot(Vector3 blockCenter, Transform townParent, string label)
    {
        Vector3 basePos = blockCenter;
        basePos.y = 0.3f;
        float halfDepth = BlockSize * 0.5f;
        Vector3 zonePos = basePos + new Vector3(0f, 0f, -(halfDepth + 1.5f));

        GameObject zone = new GameObject("Building_" + label + "_Spot");
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
