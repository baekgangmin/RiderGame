using UnityEngine;
using UnityEditor;
using UnityEditor.AI;
using System.Collections.Generic;

// 씬 전체 자동 세팅 (Player 태그, 카메라, 스태미나 시스템, 마을 블록아웃, 배달 스팟, 자전거, 폰 UI)
// 유니티 상단 메뉴 "RiderGame > Generate Town + Setup Scene" 눌러서 실행
public static class TownGenerator
{
    // 맵을 더 넓게 느끼도록 블록/도로/격자 크기를 키움 (기존 5x5·86m -> 7x7·147m -> 7x7·179m)
    private const float BlockSize = 13f;
    // 나중에 다른 차량(교통 체증) AI가 다닐 수 있도록 도로 통로 자체를 더 넓힘 (기존 7 -> 11)
    // 참고 영상(4차선 느낌)에 맞춰 차도 폭을 기존의 2배로 늘림 (일반 7m -> 14m, 메인 8.5m -> 17m)
    // 인도를 더 넓히기 위해 StreetWidth도 그만큼 같이 늘림(차도 폭 자체는 그대로 유지됨 - 아래 GetRoadWidth 참고)
    private const float StreetWidth = 21f;
    private const int GridSize = 7;

    // 인도 폭(차도 한쪽당) - 예전엔 그냥 차도 옆에 남는 빈 바닥이 인도 역할을 했는데,
    // 이제는 실제 오브젝트로 분리해서 만들어서 나중에 보행자 NPC가 다닐 자리를 명확히 구분해둠
    // 좀 더 넓게 (기존 2m -> 3.5m)
    private const float SidewalkWidth = 3.5f;
    private const float MainRoadExtraWidth = 3f; // 메인 도로는 차도를 이만큼 더 넓게 (그만큼 인도가 살짝 좁아짐) - 차도 폭과 함께 2배로
    private const int MainRoadGapIndex = GridSize / 2; // 이 가로줄을 참고 지도의 큰 도로(32번 도로 느낌)로 강조

    // 일단 도로를 전부 반듯한 격자로 만들기 위해 흔들림(warp)을 꺼둠 - 0으로 두면 모든 교차점이 정확히 격자 위치에 놓여
    // 도로가 꺾이지 않고 전부 직선이 됨. 나중에 다시 자연스러운 느낌을 원하면 이 값을 0보다 크게 되돌리면 됨
    private const float WarpAmplitude = 0f;      // 격자 교차점을 얼마나 흔들어서 도로를 구불구불하게 만들지(미터)
    private const float BlockRotationRange = 0f; // 블록 전체를 얼마나 회전시켜서 불규칙한 모양으로 보이게 할지(도) - 도로와 함께 반듯하게

    private const float RoadThickness = 0.06f; // 연석/중앙선/횡단보도가 바닥 위로 얼마나 떠서 그려질지 기준값

    // Ground 메시 안에서 기본 바닥(y=0)과 도로/교차로 사각형이 같은 높이(y=0)에 딱 겹쳐 있으면, 같은 메시 안이라도
    // 렌더링 순서가 매 프레임 안정적으로 보장되지 않아 Z-fighting(깜빡임)이 날 수 있음. 그래서 도로 쪽만 아주
    // 살짝(사람 눈에는 안 보일 정도로) 위로 띄워서 항상 도로가 이기도록 높이로 확실히 순서를 고정함
    private const float GroundLayerY = 0.01f;

    // 도로/차선/인도/연석이 교차로에서 서로 겹쳐서 깜빡이는 문제 방지용 -
    // 모든 도로를 교차점(node)까지 꽉 채우는 대신, 각 교차점에서 도로 폭만큼 안쪽으로 잘라서 짧게 만들고
    // 교차로 자리는 별도의 사각형 하나(RoadFill)로 통째로 채움 - 여러 개가 겹치는 대신 이음새 없이 붙게 됨
    // (지금은 도로 흔들림(WarpAmplitude)을 꺼놔서 각도 어긋남 걱정이 없으므로 여유를 최소한으로 줄임 -
    // 여유가 클수록 RoadFill이 실제 도로 폭보다 쓸데없이 넓어져서 연석이 안쪽으로 들어가 보이는 틈이 생겼었음)
    private const float IntersectionFillMargin = 0.2f;

    // 참고 영상 스타일 - 메인 도로 중앙선은 얇은 노란선 한 줄 대신 두 줄을 나란히 그림(중앙분리선 느낌)
    private const float CenterLineWidth = 0.18f;
    private const float DoubleLineGap = 0.25f; // 이중선 두 줄 사이 간격

    private static readonly Vector2Int ParkCell = new Vector2Int(1, 2);  // 공원 블록 (참고 지도의 갈마공원 느낌)
    private static readonly Vector2Int PondCell = new Vector2Int(5, 4); // 연못 블록 (참고 지도의 월평공원 호수 느낌)

    // 실제 지도(참고 이미지) 느낌 - 밝은 인도 바닥 위에 어두운 차도가 지나가고, 베이지/황토색 건물들이 촘촘하게 들어참
    // 인도는 별도 오브젝트 없이 이 Ground 바닥이 그대로 인도 역할을 함(균일한 색 하나로 이어짐)
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
        BuildIntersectionFills(townParent, nodes);
        FinalizeGroundMesh(townParent);
        BuildIntersectionCrosswalks(townParent, nodes);
        SetupBicycle();
        BakeNavMesh();

        Debug.Log("Town Generator: 씬 세팅 + 마을 생성 완료! Play를 누르면 배달 스팟이 랜덤으로 배정됩니다. Ctrl+S로 저장하는 것 잊지 마세요.");
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

    // 도로/인도/교차로를 전부 따로따로 만들어 붙이면(조각마다 트림 계산이 살짝만 어긋나도) 틈이나 겹침(깜빡임)이
    // 생기는 문제가 반복됐음 - 그래서 이 조각들을 전부 하나의 메시(정점 색으로 도로/인도 색을 구분)로 합쳐서
    // 이음매 자체가 없는 하나의 평평한 판으로 만듦. 건물/인도 위 장식물은 그 위에 살짝 얹힘.
    // Add~ 계열 함수들은 실제 오브젝트를 만들지 않고 이 리스트에 사각형 정점만 쌓아두고,
    // FinalizeGroundMesh에서 한 번에 메시 하나로 굽는다.
    private static List<Vector3> _groundVerts;
    private static List<int> _groundTris;
    private static List<Color32> _groundColors;

    private static void AddGroundQuad(Vector3 center, Quaternion rotation, float width, float length, Color color)
    {
        Vector3 right = rotation * Vector3.right * (width * 0.5f);
        Vector3 fwd = rotation * Vector3.forward * (length * 0.5f);

        int baseIndex = _groundVerts.Count;
        _groundVerts.Add(center - right - fwd);
        _groundVerts.Add(center - right + fwd);
        _groundVerts.Add(center + right + fwd);
        _groundVerts.Add(center + right - fwd);

        Color32 c32 = color;
        _groundColors.Add(c32);
        _groundColors.Add(c32);
        _groundColors.Add(c32);
        _groundColors.Add(c32);

        _groundTris.Add(baseIndex);
        _groundTris.Add(baseIndex + 1);
        _groundTris.Add(baseIndex + 2);
        _groundTris.Add(baseIndex);
        _groundTris.Add(baseIndex + 2);
        _groundTris.Add(baseIndex + 3);
    }

    private static Transform SetupGround()
    {
        GameObject oldGround = GameObject.Find("Ground");
        if (oldGround != null)
        {
            Object.DestroyImmediate(oldGround);
        }

        _groundVerts = new List<Vector3>();
        _groundTris = new List<int>();
        _groundColors = new List<Color32>();

        float worldSize = GridSize * (BlockSize + StreetWidth) + StreetWidth;
        AddGroundQuad(Vector3.zero, Quaternion.identity, worldSize, worldSize, GroundColor);

        GameObject town = GameObject.Find("Town");
        if (town != null)
        {
            Object.DestroyImmediate(town);
        }
        town = new GameObject("Town");
        return town.transform;
    }

    // AddGroundQuad로 쌓아둔 사각형들을 실제 메시 하나로 굽고, "Ground" 오브젝트 하나로 만듦.
    // 정점 색을 그대로 보여주는 VertexColorUnlit 셰이더를 써서, 도로/인도 색 구분이 색칠(정점 색)만으로
    // 이루어지고 조각 사이 경계가 없어서 틈/겹침/깜빡임이 구조적으로 생기지 않음.
    private static void FinalizeGroundMesh(Transform parent)
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(_groundVerts);
        mesh.SetColors(_groundColors);
        mesh.SetTriangles(_groundTris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.name = "GroundMesh";

        GameObject ground = new GameObject("Ground");
        ground.transform.SetParent(parent);
        ground.transform.position = Vector3.zero;

        ground.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = ground.AddComponent<MeshRenderer>();
        Shader shader = Shader.Find("Custom/VertexColorUnlit");
        renderer.sharedMaterial = new Material(shader);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        // 플레이어/차량이 이 위에 서 있을 수 있도록 물리 충돌용 메시 콜라이더도 붙임(예전엔 Plane 프리미티브가 기본 제공하던 것)
        ground.AddComponent<MeshCollider>().sharedMesh = mesh;

        MarkNavStatic(ground);

        _groundVerts = null;
        _groundTris = null;
        _groundColors = null;
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
        // 도로/차선/인도/연석 전체를 교차점 자리만큼 짧게 잘라서, 교차로에서는 도로끼리 겹치지 않고
        // 대신 BuildIntersectionFills가 채워주는 사각형 하나에 서로 이어붙게 함
        for (int row = 0; row <= GridSize; row++)
        {
            bool isMain = (row == MainRoadGapIndex);
            float hTrim = GetNodeTrim(isMain); // 가로 도로는 양 끝 모두 같은 줄(row) 소속이라 트림 값이 같음
            for (int col = 0; col < GridSize; col++)
            {
                BuildRoadSegment(townParent, nodes[col, row], nodes[col + 1, row], isMain, "H" + row + "_" + col, hTrim, hTrim);
            }
        }

        // 세로 방향 도로 (남북) - col 하나가 도로 한 줄
        // 양 끝(row, row+1)이 각각 메인 도로 줄인지 아닌지에 따라 그 교차점의 트림 크기가 다르므로 끝마다 따로 계산
        for (int col = 0; col <= GridSize; col++)
        {
            for (int row = 0; row < GridSize; row++)
            {
                float trimFrom = GetNodeTrim(row == MainRoadGapIndex);
                float trimTo = GetNodeTrim(row + 1 == MainRoadGapIndex);
                BuildRoadSegment(townParent, nodes[col, row], nodes[col, row + 1], false, "V" + col + "_" + row, trimFrom, trimTo);
            }
        }
    }

    private static void BuildRoadSegment(Transform parent, Vector3 from, Vector3 to, bool isMain, string label, float trimFrom, float trimTo)
    {
        Vector3 diff = to - from;
        float fullLength = diff.magnitude;
        if (fullLength < 0.01f)
        {
            return;
        }

        Vector3 fullMid = (from + to) * 0.5f;
        float angle = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0f, angle, 0f);
        Vector3 dirUnit = diff / fullLength;

        // 도로/차선/인도/연석을 전부 이 트림된 길이·중심으로 통일해서 만듦 -
        // 교차점 쪽 끝은 도로 폭만큼 짧아지고, 그 빈 자리는 BuildIntersectionFills의 사각형이 대신 채움
        float length = Mathf.Max(0.3f, fullLength - trimFrom - trimTo);
        Vector3 mid = fullMid + dirUnit * ((trimFrom - trimTo) * 0.5f);

        // 차도 폭 - 인도(SidewalkWidth) 양쪽을 뺀 나머지가 차도. 메인 도로는 그만큼 차도를 더 넓게 씀
        float roadWidth = GetRoadWidth(isMain);
        float thickness = RoadThickness;

        // 도로 자체는 별도 오브젝트가 아니라 하나로 합쳐지는 지면 메시(Ground)에 사각형 하나로 얹음 -
        // 그래서 여기선 위치/색만 큐에 쌓아두고, 실제 오브젝트는 FinalizeGroundMesh에서 한 번에 만들어짐
        AddGroundQuad(new Vector3(mid.x, GroundLayerY, mid.z), rotation, roadWidth, length, isMain ? MainRoadColor : RoadColor);

        // 중앙선(차선) - 메인 도로는 얇은 노란선 두 줄, 일반 도로는 흰선 한 줄. 실제 교차로처럼 중앙선은 교차로 안쪽까지는 안 그림
        BuildCenterLine(parent, mid, rotation, length, thickness, isMain, label);
    }

    // 교차점 하나를 통째로 덮는 사각형 크기 - 이 자리에서 만나는 가로/세로 도로 폭 중 더 넓은 쪽 기준으로,
    // 흔들린(warp) 도로가 살짝 다른 각도로 들어와도 틈 없이 덮이도록 여유(IntersectionFillMargin)를 더함
    private static float GetIntersectionFillSize(bool isMainRow)
    {
        float hWidth = GetRoadWidth(isMainRow);
        float vWidth = GetRoadWidth(false);
        return Mathf.Max(hWidth, vWidth) + IntersectionFillMargin;
    }

    // 이 교차점에 붙는 도로/차선/인도/연석이 양 끝에서 잘려나가야 하는 길이 (교차로 사각형의 절반)
    private static float GetNodeTrim(bool isMainRow)
    {
        return GetIntersectionFillSize(isMainRow) * 0.5f;
    }

    // 각 교차점을 도로색 사각형 하나로 통째로 채움 - 다른 도로 조각과 겹치는 대신, 잘려나간 도로들이 이 사각형에
    // 이음새 없이 맞닿게 됨 (이제는 별도 오브젝트가 아니라 Ground 메시에 사각형만 추가함)
    private static void BuildIntersectionFills(Transform parent, Vector3[,] nodes)
    {
        for (int row = 0; row <= GridSize; row++)
        {
            bool isMainRow = (row == MainRoadGapIndex);
            float fillSize = GetIntersectionFillSize(isMainRow);

            for (int col = 0; col <= GridSize; col++)
            {
                Vector3 node = nodes[col, row];
                AddGroundQuad(new Vector3(node.x, GroundLayerY, node.z), Quaternion.identity, fillSize, fillSize, isMainRow ? MainRoadColor : RoadColor);
            }
        }
    }

    // 메인 도로는 얇은 노란선 두 줄을 나란히(중앙분리선 느낌), 일반 도로는 흰선 한 줄만 그림
    private static void BuildCenterLine(Transform parent, Vector3 mid, Quaternion rotation, float length, float thickness, bool isMain, string label)
    {
        if (isMain)
        {
            Vector3 perp = rotation * Vector3.right;
            float halfOffset = DoubleLineGap * 0.5f + CenterLineWidth * 0.5f;
            BuildLineStripe(parent, mid + perp * halfOffset, rotation, length, thickness, LineYellow, label + "_A");
            BuildLineStripe(parent, mid - perp * halfOffset, rotation, length, thickness, LineYellow, label + "_B");
        }
        else
        {
            BuildLineStripe(parent, mid, rotation, length, thickness, LineWhite, label);
        }
    }

    private static void BuildLineStripe(Transform parent, Vector3 center, Quaternion rotation, float length, float thickness, Color color, string label)
    {
        GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = "Line_" + label;
        line.transform.SetParent(parent);
        line.transform.position = new Vector3(center.x, thickness + 0.03f, center.z);
        line.transform.rotation = rotation;
        line.transform.localScale = new Vector3(CenterLineWidth, thickness, length);

        Collider lineCol = line.GetComponent<Collider>();
        if (lineCol != null)
        {
            Object.DestroyImmediate(lineCol);
        }

        DisableShadows(line);
        ApplyColor(line, color);
    }

    private static float GetRoadWidth(bool isMain)
    {
        return (StreetWidth - SidewalkWidth * 2f) + (isMain ? MainRoadExtraWidth : 0f);
    }

    // 실제 사거리처럼, 횡단보도는 교차로 "한가운데"가 아니라 각 도로가 교차로로 들어오는 자리(진입부)마다 따로 놓음.
    // 교차로 중심은 그대로 비워두고(그냥 도로), 동/서/남/북 각 방향 도로 위에 횡단보도를 얹음.
    // 도로 폭(메인/일반)에 따라 횡단보도 폭·깊이가 조금씩 달라 보인다는 피드백이 있어서, 도로 종류와 상관없이
    // 모든 횡단보도가 항상 같은 크기(UnifiedCrosswalkWidth x UnifiedCrosswalkDepth)로 나오게 고정함
    private const float UnifiedCrosswalkWidth = 14f; // 모든 횡단보도의 폭(도로를 가로지르는 방향)
    private const float UnifiedCrosswalkDepth = 3f;  // 모든 횡단보도의 깊이(진행 방향)
    private const float CrosswalkStartGap = 0.4f;    // 교차로 사각형 바로 바깥쪽에서 살짝 띄우는 여유 (노드 기준 9m 지점에서 시작)

    private static void BuildIntersectionCrosswalks(Transform parent, Vector3[,] nodes)
    {
        // 시작 위치도 도로 종류와 상관없이 완전히 고정값으로 통일함 - 메인 도로 쪽 교차로 사각형(RoadFill)이
        // 가장 크므로, 그 기준(isMainRow: true)으로 한 번만 계산해서 어떤 교차로에서도 절대 겹치지 않게 함.
        // (전에는 행마다 다시 계산해서, 같은 col이라도 메인/일반 행에 따라 노드 기준 오프셋이 달라져 있었음)
        float edgeDistance = GetNodeTrim(true) + CrosswalkStartGap;

        for (int row = 0; row <= GridSize; row++)
        {
            for (int col = 0; col <= GridSize; col++)
            {
                Vector3 node = nodes[col, row];
                string label = row + "_" + col;

                // 동/서 - 가로 도로 위에, 교차로(RoadFill) 바로 바깥쪽에 놓음
                if (col < GridSize)
                {
                    Vector3 dir = (nodes[col + 1, row] - node).normalized;
                    BuildApproachCrosswalk(parent, node, dir, UnifiedCrosswalkWidth, edgeDistance, UnifiedCrosswalkDepth, label + "_E");
                }
                if (col > 0)
                {
                    Vector3 dir = (nodes[col - 1, row] - node).normalized;
                    BuildApproachCrosswalk(parent, node, dir, UnifiedCrosswalkWidth, edgeDistance, UnifiedCrosswalkDepth, label + "_W");
                }

                // 남/북 - 세로 도로 위에, 교차로(RoadFill) 바로 바깥쪽에 놓음
                if (row < GridSize)
                {
                    Vector3 dir = (nodes[col, row + 1] - node).normalized;
                    BuildApproachCrosswalk(parent, node, dir, UnifiedCrosswalkWidth, edgeDistance, UnifiedCrosswalkDepth, label + "_N");
                }
                if (row > 0)
                {
                    Vector3 dir = (nodes[col, row - 1] - node).normalized;
                    BuildApproachCrosswalk(parent, node, dir, UnifiedCrosswalkWidth, edgeDistance, UnifiedCrosswalkDepth, label + "_S");
                }
            }
        }
    }

    // 도로 한 방향(dir)에 놓는 횡단보도 한 세트 - edgeDistance만큼 떨어진 자리(도로 진입부)부터 도로 쪽으로 놓임.
    // 참고 영상처럼 도로 위에 별도의 색깔 판(블록)을 올리지 않고, 중앙선(Line_)과 같은 높이에 흰 줄무늬만
    // 바로 그려서 도로 바탕색이 줄무늬 사이로 그대로 비치는 "그림" 방식으로 만듦.
    private static void BuildApproachCrosswalk(Transform parent, Vector3 node, Vector3 dir, float crossWidth, float edgeDistance, float depth, string label)
    {
        float stripeThickness = 0.03f;
        float stripeY = RoadThickness + 0.03f; // 중앙선(Line_)과 동일한 높이

        Quaternion rotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);
        Vector3 bandCenter = node + dir * (edgeDistance + depth * 0.5f);

        // 줄무늬를 진행 방향과 "나란하게"(세로로 긴 줄) 놓고, 그 줄들을 도로 폭 방향으로 나란히 반복함
        // 개수는 줄이고 각 줄은 더 넓게
        float stripeSpacing = 2.1f;
        int stripeCount = Mathf.Max(3, Mathf.RoundToInt(crossWidth / stripeSpacing));
        float stripeWidth = (crossWidth / stripeCount) * 0.4f; // 도로 폭 방향 쪽 두께
        Vector3 perp = rotation * Vector3.right;

        // 양 끝 줄무늬의 바깥쪽 모서리가 연석 바로 안쪽(도로 폭 끝)에 딱 닿도록 맨 끝부터 맨 끝까지 고르게 분배함 -
        // 예전엔 중앙 기준으로만 간격을 나눠서 양옆에 연석까지 안 닿는 빈 틈이 생겨 횡단보도가 좁아 보였음
        float edgeToEdge = crossWidth - stripeWidth;

        for (int i = 0; i < stripeCount; i++)
        {
            float t = stripeCount > 1 ? (float)i / (stripeCount - 1) : 0.5f;
            float offset = Mathf.Lerp(-edgeToEdge * 0.5f, edgeToEdge * 0.5f, t);

            GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "Crosswalk_" + label + "_Stripe" + i;
            stripe.transform.SetParent(parent);
            stripe.transform.position = new Vector3(bandCenter.x, stripeY, bandCenter.z) + perp * offset;
            stripe.transform.rotation = rotation;
            stripe.transform.localScale = new Vector3(stripeWidth, stripeThickness, depth);

            Collider stripeCol = stripe.GetComponent<Collider>();
            if (stripeCol != null)
            {
                Object.DestroyImmediate(stripeCol);
            }

            DisableShadows(stripe);
            ApplyColor(stripe, LineWhite);
        }
    }

    // 도로/인도처럼 얇고 서로 다닥다닥 붙어있는 바닥 장식 전용 - 그림자를 켜두면
    // 섀도우맵 정밀도 문제로 표면이 깜빡거리는 원인이 되므로 아예 끔 (건물/공원/연못 등은 그대로 그림자 유지)
    private static void DisableShadows(GameObject go)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
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
