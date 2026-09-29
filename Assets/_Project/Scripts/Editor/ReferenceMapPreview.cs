using UnityEngine;
using UnityEditor;
using UnityEditor.AI;
using System.Collections.Generic;

// 1단계 체크포인트용 - 사용자가 보낸 네이버 지도 캡쳐(갈마동 일대)에서 눈대중으로 딴 주요 도로 경로만
// 먼저 미리보기로 그려봄. 기존에 잘 동작하는 절차적 TownGenerator는 안 건드리고 별도 메뉴/오브젝트로 분리해서,
// 이 레이아웃이 마음에 안 들면 그냥 지워도 기존 마을에는 영향이 없음.
// 사용자 확인 후 괜찮으면 여기에 건물 배치를 더 얹고, 최종적으로 TownGenerator에 통합할 예정.
public static class ReferenceMapPreview
{
    // 사용자가 그림판으로 직접 그린 마을 설계도(1160x814) 기준 픽셀 좌표 -> 월드 좌표(미터) 변환.
    // 기존 맵보다 커져도 된다고 했으니 스케일을 넉넉하게 잡음(약 290m x 200m)
    private const float PixelToWorldScale = 0.25f;
    private const float ImageCenterX = 580f;
    private const float ImageCenterY = 407f;

    // 이번 체크포인트는 도로만 먼저 확인받는 단계라 건물은 아직 안 만듦
    private const bool IncludeBuildings = false;

    // 인도 폭 - 기존 TownGenerator의 SidewalkWidth와 맞춤. 차도 양옆으로 이만큼 더 넓게 밝은 색을 깔아서 걸을 수 있는 길처럼 보이게 함
    private const float SidewalkWidth = 3.5f;
    private static readonly Color RoadColor = new Color(0.36f, 0.32f, 0.48f);
    private static readonly Color SidewalkColor = new Color(0.88f, 0.87f, 0.85f);

    // 레이어 높이 - 도로가 인도보다 항상 위(RoadLayerY)에 오도록 띄움. 도로/인도 각각 전부 하나의 메시로
    // 합쳐서 만들기 때문에(CreateFlatMeshObject) 기본적으로는 자국 없이 이어지지만, 이음매(원판/사각형)는
    // 일반 조각과 완전히 같은 높이로 겹치면 다시 깜빡거려서(Z-fighting) 이만큼만 살짝 더 위에 그림.
    // SidewalkLayerY는 0으로 두면 Floor 큐브의 윗면(정확히 y=0)과 정확히 같은 높이라 그 둘끼리도 깜빡였어서
    // 바닥보다 살짝 띄움(기존 TownGenerator의 GroundLayerY와 같은 이유)
    private const float RoadLayerY = 0.05f;
    private const float SidewalkLayerY = 0.01f;
    private const float JointLayerLift = 0.006f;

    // 도로 구간 하나(점과 점 사이)를 몇 조각으로 쪼개서 곡선처럼 보이게 할지 - 클수록 부드럽지만 정점 수가 늘어남
    private const int CurveSubdivisions = 6;

    private struct RoadPath
    {
        public string Name;
        public float Width;
        public Vector2[] PixelPoints;

        public RoadPath(string name, float width, Vector2[] pixelPoints)
        {
            Name = name;
            Width = width;
            PixelPoints = pixelPoints;
        }
    }

    // 사용자가 그림판으로 그려준 도로망(검은 선)을 그대로 옮긴 것 - 블록 경계를 따라가는 격자 + 우측 곡선 외곽선 구조.
    // 도로끼리 실제로 맞물리도록 교차점 좌표를 이름 붙여서 한 곳에서만 정의하고 여러 도로에서 재사용함
    private static readonly Vector2 J_TL = new Vector2(10, 10);       // 좌상단 모서리
    private static readonly Vector2 J_T1 = new Vector2(160, 8);       // 상단 / V1(x160)
    private static readonly Vector2 J_T2 = new Vector2(300, 8);       // 상단 / V2(x300)
    private static readonly Vector2 J_T3 = new Vector2(470, 5);       // 상단 / V3(x470)
    private static readonly Vector2 J_T4 = new Vector2(600, 8);       // 상단 / V4(x600)
    private static readonly Vector2 J_T5 = new Vector2(800, 12);      // 상단 / V5(x800)
    private static readonly Vector2 J_TR = new Vector2(870, 20);      // 우상단, 곡선 시작점

    private static readonly Vector2 J_H1_1 = new Vector2(160, 160);
    private static readonly Vector2 J_H1_2 = new Vector2(300, 155);
    private static readonly Vector2 J_H1_3 = new Vector2(470, 165);
    private static readonly Vector2 J_H1_4 = new Vector2(600, 160);
    private static readonly Vector2 J_H1_5 = new Vector2(800, 175);

    private static readonly Vector2 J_H2_1 = new Vector2(160, 260);
    private static readonly Vector2 J_H2_2 = new Vector2(300, 255);
    private static readonly Vector2 J_H2_3 = new Vector2(470, 270);
    private static readonly Vector2 J_H2_4 = new Vector2(650, 265);   // V4/V5는 여기까지 안 내려오고, H2만 곡선 쪽으로 이어짐
    private static readonly Vector2 J_H2_5 = new Vector2(800, 280);   // 우측 곡선과 만나는 지점

    private static readonly Vector2 J_H3_1 = new Vector2(160, 500);
    private static readonly Vector2 J_H3_2 = new Vector2(300, 495);
    private static readonly Vector2 J_H3_3 = new Vector2(470, 500);
    private static readonly Vector2 J_H3_4 = new Vector2(650, 505);
    private static readonly Vector2 J_H3_5 = new Vector2(860, 510);   // 우측 곡선과 만나는 지점

    private static readonly Vector2 J_H4_1 = new Vector2(160, 650);
    private static readonly Vector2 J_H4_2 = new Vector2(300, 655);
    private static readonly Vector2 J_H4_3 = new Vector2(500, 650);

    private static readonly Vector2 J_BL = new Vector2(10, 775);      // 좌하단 모서리
    private static readonly Vector2 J_B1 = new Vector2(150, 788);
    private static readonly Vector2 J_B2 = new Vector2(400, 782);
    private static readonly Vector2 J_BR = new Vector2(700, 790);     // 우하단, 곡선 끝점

    // 우측 곡선 외곽선(대덕대로 느낌의 굽은 경계) 중간 지점들
    private static readonly Vector2 J_RC1 = new Vector2(895, 120);
    private static readonly Vector2 J_RC2 = new Vector2(865, 270);    // ≈ J_H2_5
    private static readonly Vector2 J_RC3 = new Vector2(858, 420);
    private static readonly Vector2 J_RC4 = new Vector2(790, 470);
    private static readonly Vector2 J_RC5 = new Vector2(775, 560);
    private static readonly Vector2 J_RC6 = new Vector2(800, 650);
    private static readonly Vector2 J_RC7 = new Vector2(760, 705);

    private static readonly RoadPath[] Roads = new RoadPath[]
    {
        new RoadPath("TopEdge", 12f, new Vector2[] { J_TL, J_T1, J_T2, J_T3, J_T4, J_T5, J_TR }),
        new RoadPath("RightCurve", 14f, new Vector2[] { J_TR, J_RC1, J_RC2, J_RC3, J_RC4, J_RC5, J_RC6, J_RC7, J_BR }),
        new RoadPath("BottomEdge", 12f, new Vector2[] { J_BR, J_B2, J_B1, J_BL }),
        new RoadPath("LeftEdge", 12f, new Vector2[] { J_BL, new Vector2(10, 650), new Vector2(10, 500), new Vector2(10, 260), new Vector2(10, 160), J_TL }),

        new RoadPath("V1", 9f, new Vector2[] { J_T1, J_H1_1, J_H2_1, J_H3_1, J_H4_1, new Vector2(160, 775) }),
        new RoadPath("V2", 9f, new Vector2[] { J_T2, J_H1_2, J_H2_2, J_H3_2, J_H4_2 }),
        new RoadPath("V3", 9f, new Vector2[] { J_T3, J_H1_3, J_H2_3, J_H3_3 }),
        new RoadPath("V4", 9f, new Vector2[] { J_T4, J_H1_4, J_H2_4 }),
        new RoadPath("V5", 9f, new Vector2[] { J_T5, J_H1_5, J_H2_5 }),

        new RoadPath("H1", 9f, new Vector2[] { J_H1_1, J_H1_2, J_H1_3, J_H1_4, J_H1_5 }),
        new RoadPath("H2", 9f, new Vector2[] { J_H2_1, J_H2_2, J_H2_3, J_H2_4, J_H2_5 }),
        new RoadPath("H3", 9f, new Vector2[] { J_H3_1, J_H3_2, J_H3_3, J_H3_4, J_H3_5 }),
        new RoadPath("H4", 9f, new Vector2[] { J_H4_1, J_H4_2, J_H4_3 }),
    };

    // 도로가 꺾이거나 여러 개가 만나는 교차점들 - 전부 정사각형으로 덮어서 이어붙임
    private static readonly (Vector2 point, float size)[] Junctions = new (Vector2, float)[]
    {
        (J_T1, 9f), (J_T2, 9f), (J_T3, 9f), (J_T4, 9f), (J_T5, 9f),
        (J_H1_1, 9f), (J_H1_2, 9f), (J_H1_3, 9f), (J_H1_4, 9f), (J_H1_5, 9f),
        (J_H2_1, 9f), (J_H2_2, 9f), (J_H2_3, 9f), (J_H2_4, 9f), (J_H2_5, 14f),
        (J_H3_1, 9f), (J_H3_2, 9f), (J_H3_3, 9f), (J_H3_4, 9f), (J_H3_5, 14f),
        (J_H4_1, 9f), (J_H4_2, 9f), (J_H4_3, 9f),
        (J_TR, 14f), (J_BR, 14f), (J_BL, 12f), (J_TL, 12f), (J_B1, 12f), (J_B2, 12f),
    };

    private struct ParkArea
    {
        public string Name;
        public Vector2 PixelCenter;
        public Vector2 PixelSize;

        public ParkArea(string name, Vector2 pixelCenter, Vector2 pixelSize)
        {
            Name = name;
            PixelCenter = pixelCenter;
            PixelSize = pixelSize;
        }
    }

    private static readonly ParkArea[] Parks = new ParkArea[]
    {
        new ParkArea("한마음어린이공원", new Vector2(170, 355), new Vector2(90, 90)),
        new ParkArea("건능골어린이공원", new Vector2(655, 275), new Vector2(130, 110)),
    };

    private struct BuildingSpot
    {
        public string Name;
        public Vector2 PixelCenter;
        public Vector2 PixelSize;
        public float RotationDeg;
        public Color Color;

        public BuildingSpot(string name, Vector2 pixelCenter, Vector2 pixelSize, float rotationDeg, Color color)
        {
            Name = name;
            PixelCenter = pixelCenter;
            PixelSize = pixelSize;
            RotationDeg = rotationDeg;
            Color = color;
        }
    }

    // 화사한 토이 팔레트 - 기존 TownGenerator의 BuildingPalette와 같은 톤
    private static readonly Color ColorPink = new Color(1.00f, 0.78f, 0.80f);
    private static readonly Color ColorYellow = new Color(1.00f, 0.88f, 0.55f);
    private static readonly Color ColorSky = new Color(0.65f, 0.85f, 1.00f);
    private static readonly Color ColorMint = new Color(0.70f, 0.92f, 0.75f);
    private static readonly Color ColorLavender = new Color(0.90f, 0.75f, 1.00f);
    private static readonly Color ColorHospitalPink = new Color(0.95f, 0.65f, 0.68f); // 지도에서 분홍색으로 강조된 양녕요양병원

    // 지도에 라벨 붙은 주요 건물들 - 크기 3단계(소형 상가/편의점, 중형 빌딩/빌라, 대형 아파트단지·병원)로 대략 구분해서 배치.
    // 정확한 축척/각도가 아니라 "이 자리에 이 정도 크기 건물이 있다"는 느낌만 옮긴 것
    private static readonly BuildingSpot[] Buildings = new BuildingSpot[]
    {
        // 소형 상가/편의점
        new BuildingSpot("GS25_top", new Vector2(95, 65), new Vector2(26, 22), 0f, ColorMint),
        new BuildingSpot("CU", new Vector2(260, 45), new Vector2(26, 22), 0f, ColorMint),
        new BuildingSpot("다이소", new Vector2(385, 50), new Vector2(26, 22), 0f, ColorYellow),
        new BuildingSpot("삼양슈퍼", new Vector2(465, 50), new Vector2(28, 22), 0f, ColorYellow),
        new BuildingSpot("GS칼텍스", new Vector2(270, 195), new Vector2(22, 22), 0f, ColorMint),
        new BuildingSpot("초가집", new Vector2(510, 195), new Vector2(24, 22), 0f, ColorLavender),
        new BuildingSpot("신협", new Vector2(280, 345), new Vector2(24, 20), 0f, ColorSky),
        new BuildingSpot("중문복지재단", new Vector2(345, 310), new Vector2(26, 22), 0f, ColorSky),
        new BuildingSpot("GS25_bottom", new Vector2(15, 420), new Vector2(26, 22), 0f, ColorMint),
        new BuildingSpot("진강회관", new Vector2(650, 615), new Vector2(26, 22), 0f, ColorLavender),
        new BuildingSpot("현대할인마트", new Vector2(700, 440), new Vector2(30, 24), 0f, ColorYellow),

        // 중형 빌딩/빌라
        new BuildingSpot("영지빌딩", new Vector2(355, 15), new Vector2(50, 34), 0f, ColorSky),
        new BuildingSpot("석섬빌딩", new Vector2(215, 110), new Vector2(55, 40), 0f, ColorLavender),
        new BuildingSpot("이마트에브리데이", new Vector2(195, 195), new Vector2(55, 40), 0f, ColorPink),
        new BuildingSpot("우신빌딩", new Vector2(405, 200), new Vector2(50, 38), 0f, ColorSky),
        new BuildingSpot("향기로운지역아동센터", new Vector2(95, 245), new Vector2(55, 42), 0f, ColorMint),
        new BuildingSpot("갈마그랜드프라자", new Vector2(195, 470), new Vector2(55, 42), 0f, ColorLavender),
        new BuildingSpot("대호빌라", new Vector2(465, 365), new Vector2(50, 40), 0f, ColorPink),
        new BuildingSpot("청우빌딩", new Vector2(520, 490), new Vector2(50, 40), 0f, ColorSky),
        new BuildingSpot("쌍용할인마트", new Vector2(595, 355), new Vector2(55, 40), 0f, ColorYellow),

        // 대형 아파트 단지 / 병원
        new BuildingSpot("양녕요양병원", new Vector2(280, 280), new Vector2(110, 90), 0f, ColorHospitalPink),
        new BuildingSpot("갈마1단지아파트", new Vector2(40, 505), new Vector2(90, 70), 0f, ColorPink),
        new BuildingSpot("쌍용갈마아파트_1", new Vector2(700, 265), new Vector2(85, 65), 0f, ColorSky),
        new BuildingSpot("쌍용갈마아파트_2", new Vector2(745, 320), new Vector2(85, 65), 0f, ColorSky),
        new BuildingSpot("쌍용갈마아파트_3", new Vector2(715, 395), new Vector2(85, 65), 0f, ColorSky),
        new BuildingSpot("쌍용갈마아파트_4", new Vector2(665, 355), new Vector2(85, 65), 0f, ColorSky),
        new BuildingSpot("광도푸른아파트", new Vector2(605, 560), new Vector2(90, 70), 0f, ColorLavender),

        // 남쪽 빌라 단지(201,301~307) - 갈마로 대각선 각도에 맞춰 살짝 회전시켜서 줄지어 있는 느낌을 줌
        new BuildingSpot("201", new Vector2(160, 600), new Vector2(45, 35), 20f, ColorYellow),
        new BuildingSpot("301", new Vector2(245, 540), new Vector2(45, 35), 20f, ColorYellow),
        new BuildingSpot("302", new Vector2(300, 505), new Vector2(45, 35), 20f, ColorYellow),
        new BuildingSpot("303", new Vector2(270, 590), new Vector2(45, 35), 20f, ColorYellow),
        new BuildingSpot("304", new Vector2(365, 525), new Vector2(45, 35), 20f, ColorYellow),
        new BuildingSpot("305", new Vector2(330, 615), new Vector2(45, 35), 20f, ColorYellow),
        new BuildingSpot("306", new Vector2(415, 555), new Vector2(45, 35), 20f, ColorYellow),
        new BuildingSpot("307", new Vector2(430, 615), new Vector2(45, 35), 20f, ColorYellow),
    };

    [MenuItem("RiderGame/Preview Reference Map Roads")]
    public static void GeneratePreview()
    {
        GameObject old = GameObject.Find("ReferenceMapPreview");
        if (old != null)
        {
            Object.DestroyImmediate(old);
        }

        GameObject root = new GameObject("ReferenceMapPreview");
        root.transform.position = new Vector3(600f, 0f, 0f); // 기존 Town과 겹치지 않게 옆으로 떨어뜨려 배치

        // 바닥 - 인도색 큰 평면 (새 설계도 캔버스 1160x814 기준)
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(root.transform, false);
        floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        floor.transform.localScale = new Vector3(1160f * PixelToWorldScale, 0.1f, 814f * PixelToWorldScale);
        ApplyColor(floor, new Color(0.95f, 0.93f, 0.88f));
        MarkNavStatic(floor);

        // 1+2. 도로/인도 - 예전엔 조각마다 따로 GameObject를 만들어서 겹치는 자리에 자국(턱)이 남았는데,
        // TownGenerator의 Ground 메시처럼 도로 전체를 하나의 메시로(같은 높이로) 합쳐서 이 문제를 없앰.
        // 도로는 RoadLayerY, 인도는 그보다 낮은 SidewalkLayerY - 딱 두 층만 있고 층 안에서는 전부 같은 높이라
        // 겹쳐도 자연스럽게 하나로 이어져 보임
        List<Vector3> roadVerts = new List<Vector3>();
        List<int> roadTris = new List<int>();
        List<Color32> roadColors = new List<Color32>();
        List<Vector3> sidewalkVerts = new List<Vector3>();
        List<int> sidewalkTris = new List<int>();
        List<Color32> sidewalkColors = new List<Color32>();

        foreach (RoadPath road in Roads)
        {
            Vector2[] smooth = SmoothPath(road.PixelPoints, CurveSubdivisions);

            for (int i = 0; i < smooth.Length - 1; i++)
            {
                Vector3 from = PixelToWorld(smooth[i]);
                Vector3 to = PixelToWorld(smooth[i + 1]);
                AddMeshQuadBetween(roadVerts, roadTris, roadColors, from, to, road.Width, RoadLayerY, RoadColor);
                AddMeshQuadBetween(sidewalkVerts, sidewalkTris, sidewalkColors, from, to, road.Width + SidewalkWidth * 2f, SidewalkLayerY, SidewalkColor);
            }

            // 짧은 직선 조각들이 살짝씩 각도를 바꾸며 이어지다 보니 꺾이는 바깥쪽에 작은 틈이 생김 -
            // 이음매(양 끝 제외한 중간 점들)마다 원판을 끼워 넣어서 메움. 조각들과 정확히 같은 높이면
            // 겹치는 부분에서 다시 깜빡이므로(Z-fighting), 이음매만 고정으로 살짝 더 높게 그려서 항상 위에 덮이게 함
            for (int i = 1; i < smooth.Length - 1; i++)
            {
                Vector3 p = PixelToWorld(smooth[i]);
                AddMeshCircle(roadVerts, roadTris, roadColors, p, road.Width, RoadLayerY + JointLayerLift, RoadColor);
                AddMeshCircle(sidewalkVerts, sidewalkTris, sidewalkColors, p, road.Width + SidewalkWidth * 2f, SidewalkLayerY + JointLayerLift, SidewalkColor);
            }
        }

        foreach ((Vector2 point, float size) junction in Junctions)
        {
            Vector3 p = PixelToWorld(junction.point);
            AddMeshSquare(roadVerts, roadTris, roadColors, p, junction.size, RoadLayerY + JointLayerLift, RoadColor);
            AddMeshSquare(sidewalkVerts, sidewalkTris, sidewalkColors, p, junction.size + SidewalkWidth * 2f, SidewalkLayerY + JointLayerLift, SidewalkColor);
        }

        GameObject roadMesh = CreateFlatMeshObject(root.transform, "RoadMesh", roadVerts, roadTris, roadColors);
        MarkNavStatic(roadMesh);
        GameObject sidewalkMesh = CreateFlatMeshObject(root.transform, "SidewalkMesh", sidewalkVerts, sidewalkTris, sidewalkColors);
        MarkNavStatic(sidewalkMesh);

        int fillerAdded = 0;
        List<(Vector3 pos, float radius)> placed = new List<(Vector3, float)>();

        if (IncludeBuildings)
        {
            foreach (ParkArea park in Parks)
            {
                Vector3 center = PixelToWorld(park.PixelCenter);
                GameObject parkGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                parkGo.name = "Park_" + park.Name;
                parkGo.transform.SetParent(root.transform, false);
                parkGo.transform.localPosition = center + new Vector3(0f, 0.02f, 0f);
                parkGo.transform.localScale = new Vector3(park.PixelSize.x * PixelToWorldScale, 0.05f, park.PixelSize.y * PixelToWorldScale);
                ApplyColor(parkGo, new Color(0.55f, 0.82f, 0.42f));
                MarkNavStatic(parkGo);
            }

            // 3. 도로/인도/공원을 깐 다음, 비어있는 자리에 건물을 세운다 (ResolvePlacement가 도로+인도+공원+다른 건물을 다 피해서 배치함)
            foreach (BuildingSpot spot in Buildings)
            {
                PlaceBuilding(root.transform, spot.Name, spot.PixelCenter, spot.PixelSize, spot.RotationDeg, spot.Color, placed);
            }

            // 라벨 없는 작은 건물들을 더 채워서 지도처럼 빽빽한 느낌을 냄. 완전 무작위 위치/회전 대신
            // 격자 칸을 따라 살짝만 흔들어서(jitter) 줄이 맞아 보이게 하고, 회전도 축에 맞춰서(0도) 정렬된 느낌을 줌
            Color[] fillerPalette = { ColorPink, ColorYellow, ColorSky, ColorMint, ColorLavender };
            const float cellWidth = 58f;
            const float cellHeight = 52f;
            const float fillChance = 0.68f;

            for (float cellY = 20f; cellY < 794f; cellY += cellHeight)
            {
                for (float cellX = 20f; cellX < 1140f; cellX += cellWidth)
                {
                    if (Random.value > fillChance)
                    {
                        continue;
                    }

                    Vector2 pixelPos = new Vector2(cellX + Random.Range(-6f, 6f), cellY + Random.Range(-6f, 6f));
                    Vector2 pixelSize = new Vector2(Random.Range(24f, 38f), Random.Range(20f, 32f));
                    Color color = fillerPalette[Random.Range(0, fillerPalette.Length)];

                    bool placedOk = PlaceBuilding(root.transform, "Filler_" + fillerAdded, pixelPos, pixelSize, 0f, color, placed, requireInsideMap: true);
                    if (placedOk)
                    {
                        fillerAdded++;
                    }
                }
            }
        }

        // 배달 스팟까지 다 붙었으면 네비메시를 구움 - 도로/인도/공원(걸을 수 있는 바닥)은 NavigationStatic으로
        // 마크됐고 건물도 같이 마크돼서 구멍(장애물)으로 자동 반영됨
        NavMeshBuilder.BuildNavMesh();

        Debug.Log("ReferenceMapPreview 생성 완료 - Town 오브젝트 옆(x=600)에 놓여있음. 이번엔 도로+인도만 만들었어요(건물은 다음 단계). 그려주신 도로망이랑 비교해서 확인해주세요.");
    }

    // 도로/교차점/공원/다른 건물과 벌어져 보이도록 밀어낼 때 추가로 두는 여유 간격(미터)
    private const float ClearanceMargin = 2.2f;

    // 건물 하나를 배치 - 도로/교차점/공원/이미 놓인 건물과 겹치지 않게 자동으로 밀어낸 뒤 큐브로 생성하고 placed에 등록함.
    // requireInsideMap이 true면(랜덤 채움 건물용) 밀려난 뒤에도 지도 범위 밖으로 나가면 포기하고 false를 반환함
    private static bool PlaceBuilding(Transform parent, string name, Vector2 pixelCenter, Vector2 pixelSize, float rotationDeg, Color color, List<(Vector3 pos, float radius)> placed, bool requireInsideMap = false)
    {
        Vector3 rawCenter = PixelToWorld(pixelCenter);
        float width = Mathf.Max(1.5f, pixelSize.x * PixelToWorldScale);
        float depth = Mathf.Max(1.5f, pixelSize.y * PixelToWorldScale);
        // 회전해도 안전하게, 대각선 절반 길이를 반지름으로 써서 도로/다른 건물과 안 겹치게 밀어냄
        float buildingRadius = new Vector2(width, depth).magnitude * 0.5f;

        Vector3 center = ResolvePlacement(rawCenter, buildingRadius, placed);

        if (requireInsideMap)
        {
            float halfW = 1160f * PixelToWorldScale * 0.5f - 2f;
            float halfH = 814f * PixelToWorldScale * 0.5f - 2f;
            if (Mathf.Abs(center.x) > halfW || Mathf.Abs(center.z) > halfH)
            {
                return false;
            }
        }

        float height = Mathf.Lerp(3f, 12f, Mathf.InverseLerp(20f, 100f, Mathf.Max(pixelSize.x, pixelSize.y)));

        GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
        building.name = "Building_" + name;
        building.transform.SetParent(parent, false);
        building.transform.localPosition = center + new Vector3(0f, height * 0.5f, 0f);
        Quaternion rotation = Quaternion.Euler(0f, rotationDeg, 0f);
        building.transform.localRotation = rotation;
        building.transform.localScale = new Vector3(width, height, depth);
        ApplyColor(building, color);
        MarkNavStatic(building);

        // 기존 TownGenerator처럼 건물마다 배달 스팟을 하나씩 붙임(건물 바로 앞쪽에, 회전된 건물이면 그 방향으로)
        Vector3 spotPos = center + rotation * new Vector3(0f, 0f, -(depth * 0.5f + 1.5f));
        spotPos.y = 0.3f;
        GameObject spot = new GameObject("Building_" + name + "_Spot");
        spot.transform.SetParent(parent, false);
        spot.transform.localPosition = spotPos;
        BoxCollider spotCollider = spot.AddComponent<BoxCollider>();
        spotCollider.isTrigger = true;
        spotCollider.size = new Vector3(3f, 2f, 3f);
        spot.AddComponent<DeliverySpot>();

        placed.Add((center, buildingRadius));
        return true;
    }

    private static void MarkNavStatic(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic);
    }

    // 찍은 점들을 그대로 다 지나가면서(교차점 위치는 안 바뀜) 그 사이를 부드러운 곡선으로 잇는 Catmull-Rom 스플라인.
    // 점이 2개뿐이면(순수 직선 도로) 그대로 반환함
    private static Vector2[] SmoothPath(Vector2[] points, int subdivisions)
    {
        if (points.Length < 3 || subdivisions < 2)
        {
            return points;
        }

        List<Vector2> result = new List<Vector2>();
        int n = points.Length;
        for (int i = 0; i < n - 1; i++)
        {
            Vector2 p0 = points[Mathf.Max(0, i - 1)];
            Vector2 p1 = points[i];
            Vector2 p2 = points[i + 1];
            Vector2 p3 = points[Mathf.Min(n - 1, i + 2)];

            for (int s = 0; s < subdivisions; s++)
            {
                float t = (float)s / subdivisions;
                result.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }
        result.Add(points[n - 1]);
        return result.ToArray();
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static Vector3 PixelToWorld(Vector2 pixel)
    {
        float x = (pixel.x - ImageCenterX) * PixelToWorldScale;
        float z = (pixel.y - ImageCenterY) * PixelToWorldScale;
        return new Vector3(x, 0f, z);
    }

    // 건물 좌표를 지도에서 눈대중으로 딴 거라 도로랑 겹치는 경우가 있었음(예: GS칼텍스, 갈마그랜드프라자, 빌라 306/307
    // 등이 도로 위에 놓였음) - 하나하나 손으로 좌표를 고치는 대신, 도로 중심선/교차점 채우기/공원/이미 놓인 다른 건물과
    // 너무 가까우면 자동으로 옆으로 밀어내는 방식으로 해결함. ClearanceMargin만큼은 항상 여유를 둬서 도로랑 아예 안 닿게 함
    private static Vector3 ResolvePlacement(Vector3 rawCenter, float buildingRadius, List<(Vector3 pos, float radius)> otherBuildings)
    {
        Vector3 pos = rawCenter;

        for (int pass = 0; pass < 6; pass++)
        {
            bool moved = false;

            // 주의: road.Width/교차점 크기(16f/10f)는 BuildRoadStrip/BuildJunctionFill에서 이미 "미터" 단위로
            // 그대로(픽셀 스케일 변환 없이) 쓰이는 값이라, 여기서도 PixelToWorldScale을 다시 곱하면 안 됨
            // (예전엔 실수로 한 번 더 곱해서 실제 도로 폭보다 훨씬 좁게 계산됐었음 - 그래서 밀어내도 계속 겹쳐 보였음)
            // 건물은 차도뿐 아니라 그 옆 인도까지 벗어나 있어야 하니, 회피 기준 폭에 인도 폭도 더함
            foreach (RoadPath road in Roads)
            {
                float roadHalfWidth = (road.Width + SidewalkWidth * 2f) * 0.5f;
                Vector2[] smooth = SmoothPath(road.PixelPoints, CurveSubdivisions);
                for (int i = 0; i < smooth.Length - 1; i++)
                {
                    Vector3 a = PixelToWorld(smooth[i]);
                    Vector3 b = PixelToWorld(smooth[i + 1]);
                    moved |= PushAwayFromSegment(ref pos, a, b, roadHalfWidth + buildingRadius + ClearanceMargin);
                }
            }

            foreach ((Vector2 point, float size) junction in Junctions)
            {
                moved |= PushAwayFromPoint(ref pos, PixelToWorld(junction.point), (junction.size + SidewalkWidth * 2f) * 0.7f + buildingRadius + ClearanceMargin);
            }

            foreach (ParkArea park in Parks)
            {
                Vector3 parkCenter = PixelToWorld(park.PixelCenter);
                float parkRadius = new Vector2(park.PixelSize.x, park.PixelSize.y).magnitude * 0.5f * PixelToWorldScale;
                moved |= PushAwayFromPoint(ref pos, parkCenter, parkRadius + buildingRadius + ClearanceMargin);
            }

            for (int i = 0; i < otherBuildings.Count; i++)
            {
                moved |= PushAwayFromPoint(ref pos, otherBuildings[i].pos, otherBuildings[i].radius + buildingRadius + ClearanceMargin);
            }

            if (!moved)
            {
                break;
            }
        }

        return pos;
    }

    private static bool PushAwayFromSegment(ref Vector3 pos, Vector3 a, Vector3 b, float minDistance)
    {
        Vector3 closest = ClosestPointOnSegment(pos, a, b);
        return PushAwayFromPoint(ref pos, closest, minDistance);
    }

    private static bool PushAwayFromPoint(ref Vector3 pos, Vector3 obstacle, float minDistance)
    {
        Vector3 delta = pos - obstacle;
        delta.y = 0f;
        float dist = delta.magnitude;
        if (dist >= minDistance)
        {
            return false;
        }

        Vector3 pushDir = dist > 0.001f ? delta.normalized : Vector3.right;
        pos += pushDir * (minDistance - dist);
        return true;
    }

    private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f)
        {
            return a;
        }

        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lenSq);
        return a + ab * t;
    }

    // 아래 세 함수(AddMeshQuadBetween/AddMeshCircle/AddMeshSquare)는 개별 GameObject를 만드는 대신
    // 공유 정점/삼각형 리스트에 도형을 추가하기만 함 - CreateFlatMeshObject가 마지막에 한 번에 메시 하나로 합침.
    // 전부 같은 높이(y)로 합쳐지므로 겹쳐도 자국(턱)이나 깜빡임 없이 하나로 이어져 보임

    private const int JointCircleSegments = 12;

    private static void AddMeshQuadBetween(List<Vector3> verts, List<int> tris, List<Color32> colors, Vector3 from, Vector3 to, float width, float y, Color color)
    {
        Vector3 diff = to - from;
        float length = diff.magnitude;
        if (length < 0.01f)
        {
            return;
        }

        Vector3 dir = diff / length;
        Vector3 right = new Vector3(dir.z, 0f, -dir.x) * (width * 0.5f);
        Vector3 f = new Vector3(from.x, y, from.z);
        Vector3 t = new Vector3(to.x, y, to.z);

        int baseIndex = verts.Count;
        verts.Add(f - right);
        verts.Add(t - right);
        verts.Add(t + right);
        verts.Add(f + right);
        Color32 c32 = color;
        colors.Add(c32); colors.Add(c32); colors.Add(c32); colors.Add(c32);

        tris.Add(baseIndex); tris.Add(baseIndex + 1); tris.Add(baseIndex + 2);
        tris.Add(baseIndex); tris.Add(baseIndex + 2); tris.Add(baseIndex + 3);
    }

    private static void AddMeshCircle(List<Vector3> verts, List<int> tris, List<Color32> colors, Vector3 center, float diameter, float y, Color color)
    {
        AddMeshPolygon(verts, tris, colors, center, diameter, y, color, JointCircleSegments);
    }

    private static void AddMeshSquare(List<Vector3> verts, List<int> tris, List<Color32> colors, Vector3 center, float size, float y, Color color)
    {
        AddMeshPolygon(verts, tris, colors, center, size, y, color, 4);
    }

    // sides가 4면 각진 사각형(교차로 채우기용), 그 이상이면 원에 가까운 다각형(곡선 이음매용)을 부채꼴 삼각형으로 만듦
    private static void AddMeshPolygon(List<Vector3> verts, List<int> tris, List<Color32> colors, Vector3 center, float size, float y, Color color, int sides)
    {
        Vector3 c = new Vector3(center.x, y, center.z);
        float radius = size * 0.5f * (sides == 4 ? 1.4142f : 1f); // 사각형은 대각선까지 덮어야 도로 폭을 확실히 채움
        Color32 c32 = color;

        int baseIndex = verts.Count;
        verts.Add(c);
        colors.Add(c32);
        for (int s = 0; s <= sides; s++)
        {
            float angle = (sides == 4 ? Mathf.PI * 0.25f : 0f) + (s % sides) * (Mathf.PI * 2f / sides);
            verts.Add(c + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            colors.Add(c32);
        }
        for (int s = 0; s < sides; s++)
        {
            tris.Add(baseIndex);
            tris.Add(baseIndex + 1 + s);
            tris.Add(baseIndex + 2 + s);
        }
    }

    private static GameObject CreateFlatMeshObject(Transform parent, string name, List<Vector3> verts, List<int> tris, List<Color32> colors)
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.name = name + "Mesh";

        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        Shader shader = Shader.Find("Custom/VertexColorUnlit");
        renderer.sharedMaterial = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        MeshCollider collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;

        return go;
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
}
