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

    // 도로/인도 확정됐으니 이제 손그림 지도 기준 건물도 배치함
    private const bool IncludeBuildings = true;

    // 인도 폭 - 기존 TownGenerator의 SidewalkWidth와 맞춤. 차도 양옆으로 이만큼 더 넓게 깔아서 걸을 수 있는 길처럼 보이게 함
    private const float SidewalkWidth = 3.5f;
    // 토이 파스텔톤 대신 실제 아스팔트/보도블럭에 가까운 색으로
    private static readonly Color RoadColor = new Color(0.13f, 0.13f, 0.14f);      // 짙은 아스팔트 회색
    private static readonly Color SidewalkColor = new Color(0.74f, 0.72f, 0.68f);  // 밝은 보도블럭 회베이지

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
        new ParkArea("한마음어린이공원", new Vector2(230, 420), new Vector2(110, 90)),
        new ParkArea("건능골어린이공원", new Vector2(755, 300), new Vector2(130, 100)),
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

    // Varco3D로 만든 실제 3D 모델(어제 만든 5개 + 오늘 만든 10개) - 이름/접두어로 매칭되면 색깔 큐브 대신 이 모델을 세움.
    // TownGenerator의 BuildingModelDef와 같은 패턴(피벗이 세로 중앙 부근이라 바닥 정렬 필요, 에셋마다 원본 크기가
    // 제각각이라 고정 Scale 대신 목표 크기에 맞춰 자동으로 배율을 계산함 - FitByHeight면 높이, 아니면 가로 폭 기준)
    private struct RealModelDef
    {
        public string AssetPath;
        public Vector3 EulerAngles;
        public float TargetSize;
        public bool FitByHeight;

        // 오늘 새로 변환한 12개는 Blender에서 glTF -> FBX로 내보낼 때 축(axis_forward/up)을 명시적으로
        // 맞춰서 유니티 기준으로 이미 똑바로 서 있음(추가 보정 불필요). 반대로 어제 만든 5개 건물(Shop/
        // Apartment/Office/Cafe/Landmark, TownGenerator에서 재사용)은 변환 방식이 달라서 TownGenerator와
        // 똑같이 -90(X) 보정이 필요함 - eulerOverride로 그 경우만 명시적으로 넘겨줌
        public RealModelDef(string assetPath, float targetSize, bool fitByHeight = false, Vector3? eulerOverride = null)
        {
            AssetPath = assetPath;
            EulerAngles = eulerOverride ?? Vector3.zero;
            TargetSize = targetSize;
            FitByHeight = fitByHeight;
        }
    }

    private const string BuildingsAssetDir = "Assets/_Project/Art/Buildings/";
    private const string PropsAssetDir = "Assets/_Project/Art/Props/";
    private static readonly Vector3 LegacyModelEulerCorrection = new Vector3(-90f, 0f, 0f);

    // 건물 이름(정확히 일치) -> 모델. 격자로 채우는 이름 없는 빌라 묶음은 접두어로 따로 매칭함(아래 GetBuildingModel 참고)
    private static readonly Dictionary<string, RealModelDef> BuildingModelByName = new Dictionary<string, RealModelDef>
    {
        { "GS25_top", new RealModelDef(BuildingsAssetDir + "ConvenienceStore.fbx", 6f) },
        { "CU", new RealModelDef(BuildingsAssetDir + "ConvenienceStore.fbx", 6f) },
        { "GS25_bottom", new RealModelDef(BuildingsAssetDir + "ConvenienceStore.fbx", 6f) },
        { "GS칼텍스", new RealModelDef(BuildingsAssetDir + "ConvenienceStore.fbx", 6f) },
        { "다이소", new RealModelDef(BuildingsAssetDir + "Supermarket.fbx", 7f) },
        { "삼양슈퍼", new RealModelDef(BuildingsAssetDir + "Supermarket.fbx", 7f) },
        { "쌍용할인마트", new RealModelDef(BuildingsAssetDir + "Supermarket.fbx", 7f) },
        { "석섬빌딩", new RealModelDef(BuildingsAssetDir + "Office.fbx", 8f, eulerOverride: LegacyModelEulerCorrection) },
        { "우신빌딩", new RealModelDef(BuildingsAssetDir + "Office.fbx", 8f, eulerOverride: LegacyModelEulerCorrection) },
        { "중문복지재단", new RealModelDef(BuildingsAssetDir + "Office.fbx", 7f, eulerOverride: LegacyModelEulerCorrection) },
        { "신협", new RealModelDef(BuildingsAssetDir + "Office.fbx", 6f, eulerOverride: LegacyModelEulerCorrection) },
        { "청우빌딩", new RealModelDef(BuildingsAssetDir + "Office.fbx", 8f, eulerOverride: LegacyModelEulerCorrection) },
        { "초가집", new RealModelDef(BuildingsAssetDir + "SnackShop.fbx", 6f) },
        { "진강회관", new RealModelDef(BuildingsAssetDir + "SnackShop.fbx", 6f) },
        { "양녕요양병원", new RealModelDef(BuildingsAssetDir + "Hospital.fbx", 12f) },
        { "갈마그랜드프라자", new RealModelDef(BuildingsAssetDir + "Landmark.fbx", 9f, eulerOverride: LegacyModelEulerCorrection) },
        { "105", new RealModelDef(BuildingsAssetDir + "Apartment.fbx", 8f, eulerOverride: LegacyModelEulerCorrection) },
        { "103", new RealModelDef(BuildingsAssetDir + "Apartment.fbx", 8f, eulerOverride: LegacyModelEulerCorrection) },
        { "갈마1단지아파트", new RealModelDef(BuildingsAssetDir + "Apartment.fbx", 8f, eulerOverride: LegacyModelEulerCorrection) },
        { "쌍용갈마아파트_1", new RealModelDef(BuildingsAssetDir + "Apartment.fbx", 8f, eulerOverride: LegacyModelEulerCorrection) },
        { "쌍용갈마아파트_2", new RealModelDef(BuildingsAssetDir + "Apartment.fbx", 8f, eulerOverride: LegacyModelEulerCorrection) },
        { "쌍용갈마아파트_3", new RealModelDef(BuildingsAssetDir + "Apartment.fbx", 8f, eulerOverride: LegacyModelEulerCorrection) },
        { "쌍용갈마아파트_4", new RealModelDef(BuildingsAssetDir + "Apartment.fbx", 8f, eulerOverride: LegacyModelEulerCorrection) },
        { "광도푸른아파트", new RealModelDef(BuildingsAssetDir + "Apartment.fbx", 10f, eulerOverride: LegacyModelEulerCorrection) },
        { "대호빌라", new RealModelDef(BuildingsAssetDir + "Villa.fbx", 9.1f) },
        { "201", new RealModelDef(BuildingsAssetDir + "Villa.fbx", 7.8f) },
        { "202", new RealModelDef(BuildingsAssetDir + "Villa.fbx", 7.8f) },
        { "203", new RealModelDef(BuildingsAssetDir + "Villa.fbx", 7.8f) },
        { "301", new RealModelDef(BuildingsAssetDir + "Villa.fbx", 9.1f) },
        { "302", new RealModelDef(BuildingsAssetDir + "Villa.fbx", 9.1f) },
        { "303", new RealModelDef(BuildingsAssetDir + "Villa.fbx", 9.1f) },
        { "304", new RealModelDef(BuildingsAssetDir + "Villa.fbx", 9.1f) },
        { "305", new RealModelDef(BuildingsAssetDir + "Villa.fbx", 9.1f) },
        { "306", new RealModelDef(BuildingsAssetDir + "Villa.fbx", 9.1f) },
        { "307", new RealModelDef(BuildingsAssetDir + "Villa.fbx", 7.8f) },
    };

    // 격자로 채우는 이름 없는 빌라 묶음(TopClusterL_0, Col1_RowB_0 ...)은 접두어만 보고 매칭
    private static readonly (string prefix, RealModelDef model)[] BuildingModelByPrefix = new (string, RealModelDef)[]
    {
        ("TopClusterL", new RealModelDef(BuildingsAssetDir + "Villa.fbx", 6.5f)),
        ("Col1_RowB", new RealModelDef(BuildingsAssetDir + "Villa.fbx", 6.5f)),
        ("Col2_RowB", new RealModelDef(BuildingsAssetDir + "Villa.fbx", 6.5f)),
        ("Col1_RowC", new RealModelDef(BuildingsAssetDir + "Villa.fbx", 6.5f)),
    };

    // 공원에 놓을 소품들 - 건물과 달리 지도상 크기 기준이 없어서 실제감 있는 목표 크기를 직접 정함
    private static readonly RealModelDef PropBench = new RealModelDef(PropsAssetDir + "Bench.fbx", 1.6f);
    private static readonly RealModelDef PropStreetlamp = new RealModelDef(PropsAssetDir + "Streetlamp.fbx", 3.2f, fitByHeight: true);
    private static readonly RealModelDef PropTrashCan = new RealModelDef(PropsAssetDir + "TrashCan.fbx", 0.9f, fitByHeight: true);
    private static readonly RealModelDef PropSwing = new RealModelDef(PropsAssetDir + "Swing.fbx", 2.2f, fitByHeight: true);
    private static readonly RealModelDef PropSlide = new RealModelDef(PropsAssetDir + "Slide.fbx", 1.8f, fitByHeight: true);
    private static readonly RealModelDef PropFountain = new RealModelDef(PropsAssetDir + "Fountain.fbx", 2.5f);

    private static bool TryGetBuildingModel(string name, out RealModelDef model)
    {
        if (BuildingModelByName.TryGetValue(name, out model))
        {
            return true;
        }

        foreach (var entry in BuildingModelByPrefix)
        {
            if (name.StartsWith(entry.prefix))
            {
                model = entry.model;
                return true;
            }
        }

        model = default;
        return false;
    }

    // 배달 콜이 "가게에서 받아서 빌라/아파트로 배달"하는 느낌이 나도록, 이미 정해둔 모델 종류(에셋 경로)로
    // 가게/주거를 구분함 - 편의점·슈퍼·분식집류는 Shop, 빌라·아파트는 Residential, 나머지(병원/사무실/랜드마크
    // 등)는 Other로 둬서 픽업/배달 후보 어디에도 안 들어가게 함
    // 마을 전체를 다시 만들지 않고 기존 스팟들의 분류만 다시 매길 때(예: DeliverySpot.Category 직렬화
    // 버그 수정 후) 쓸 수 있게 public으로 열어둠
    public static DeliverySpot.SpotCategory GetSpotCategory(string name)
    {
        if (!TryGetBuildingModel(name, out RealModelDef model))
        {
            return DeliverySpot.SpotCategory.Other;
        }

        string path = model.AssetPath;
        if (path.Contains("ConvenienceStore") || path.Contains("Supermarket") || path.Contains("SnackShop"))
        {
            return DeliverySpot.SpotCategory.Shop;
        }

        if (path.Contains("Villa") || path.Contains("Apartment"))
        {
            return DeliverySpot.SpotCategory.Residential;
        }

        return DeliverySpot.SpotCategory.Other;
    }

    // TownGenerator의 SpawnBuildingModel과 같은 패턴(피벗 보정, 바닥 정렬, 콜라이더 부착)이지만,
    // Blender에서 glTF -> FBX로 내보낼 때 텍스처를 파일 안에 그대로 심어보려 했지만(embed_textures)
    // 유니티 FBX 임포터가 그 임베디드 텍스처를 못 읽어서(서브에셋에 Texture2D가 아예 안 생김) 계속 흰색으로만
    // 나왔음 - 그래서 대신 베이스 컬러 텍스처를 "<모델이름>_BaseColor.png"로 fbx 옆에 따로 저장해두고,
    // 여기서 직접 불러와 새 머티리얼을 만들어 입힘(미리보기는 매번 새로 지었다 짓는 거라 애셋으로 저장할 필요는 없음)
    private static readonly Dictionary<string, Material> BaseColorMaterialCache = new Dictionary<string, Material>();

    private static void ApplyBaseColorTexture(Renderer[] renderers, string modelAssetPath)
    {
        if (!BaseColorMaterialCache.TryGetValue(modelAssetPath, out Material material))
        {
            string texturePath = modelAssetPath.Substring(0, modelAssetPath.Length - System.IO.Path.GetExtension(modelAssetPath).Length) + "_BaseColor.png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                BaseColorMaterialCache[modelAssetPath] = null;
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            material.name = System.IO.Path.GetFileNameWithoutExtension(modelAssetPath) + "_Mat";
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            BaseColorMaterialCache[modelAssetPath] = material;
        }

        if (material == null)
        {
            return;
        }

        foreach (Renderer r in renderers)
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = material;
            }

            r.sharedMaterials = mats;
        }
    }

    // 에셋마다 원본 크기가 달라서 고정 Scale 대신 한 번 스케일 1로 재보고 목표 크기에 맞춰 배율을 계산함
    private static GameObject SpawnRealModel(Transform parent, string childName, RealModelDef def, Vector3 groundPosition, Quaternion rotation)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(def.AssetPath);
        if (asset == null)
        {
            Debug.LogWarning(childName + " 3D 모델을 못 찾았어요(" + def.AssetPath + "). Unity가 아직 임포트하지 않았을 수 있어요.");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        instance.name = childName;
        instance.transform.localPosition = groundPosition;
        instance.transform.localRotation = rotation * Quaternion.Euler(def.EulerAngles);
        instance.transform.localScale = Vector3.one;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return instance;
        }

        ApplyBaseColorTexture(renderers, def.AssetPath);

        Bounds bounds = renderers[0].bounds;
        for (int r = 1; r < renderers.Length; r++)
        {
            bounds.Encapsulate(renderers[r].bounds);
        }

        float currentSize = def.FitByHeight ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.z);
        float scaleFactor = currentSize > 0.0001f ? def.TargetSize / currentSize : 1f;
        instance.transform.localScale = Vector3.one * scaleFactor;

        bounds = renderers[0].bounds;
        for (int r = 1; r < renderers.Length; r++)
        {
            bounds.Encapsulate(renderers[r].bounds);
        }

        // Varco3D 모델은 피벗이 세로 중앙 부근인 경우가 많아서, 렌더러 바운즈 바닥(min.y)이
        // groundPosition.y에 오도록 위로 띄워서 항상 바닥에 발이 닿게 함
        float sinkOffset = groundPosition.y - bounds.min.y;
        instance.transform.position += new Vector3(0f, sinkOffset, 0f);
        bounds.center += new Vector3(0f, sinkOffset, 0f);

        Vector3 lossyScale = instance.transform.lossyScale;
        BoxCollider collider = instance.AddComponent<BoxCollider>();
        collider.center = instance.transform.InverseTransformPoint(bounds.center);
        collider.size = new Vector3(
            bounds.size.x / Mathf.Max(0.0001f, lossyScale.x),
            bounds.size.y / Mathf.Max(0.0001f, lossyScale.y),
            bounds.size.z / Mathf.Max(0.0001f, lossyScale.z));

        MarkNavStatic(instance);
        return instance;
    }

    // 지도에 라벨 붙은 주요 건물들 - 어제 그려주신 손그림(파란=소형 상가 1개, 노란=대형 아파트 1개, 빨강=대형 건물/공원)
    // 기준으로 배치. 정확한 축척/각도가 아니라 "이 자리에 이 정도 크기 건물이 있다"는 느낌만 옮긴 것
    private static readonly BuildingSpot[] Buildings = new BuildingSpot[]
    {
        // 소형 상가/편의점 (파란 네모)
        new BuildingSpot("GS25_top", new Vector2(95, 65), new Vector2(26, 22), 0f, ColorMint),
        new BuildingSpot("CU", new Vector2(325, 45), new Vector2(26, 22), 0f, ColorMint),
        new BuildingSpot("다이소", new Vector2(480, 40), new Vector2(26, 22), 0f, ColorYellow),
        new BuildingSpot("삼양슈퍼", new Vector2(575, 35), new Vector2(28, 22), 0f, ColorYellow),
        new BuildingSpot("석섬빌딩", new Vector2(220, 110), new Vector2(55, 40), 0f, ColorLavender),
        new BuildingSpot("우신빌딩", new Vector2(495, 205), new Vector2(50, 38), 0f, ColorSky),
        new BuildingSpot("초가집", new Vector2(620, 205), new Vector2(24, 22), 0f, ColorLavender),
        new BuildingSpot("중문복지재단", new Vector2(415, 385), new Vector2(26, 22), 0f, ColorSky),
        new BuildingSpot("신협", new Vector2(345, 450), new Vector2(24, 20), 0f, ColorSky),
        new BuildingSpot("대호빌라", new Vector2(575, 425), new Vector2(50, 40), 0f, ColorPink),
        new BuildingSpot("청우빌딩", new Vector2(650, 580), new Vector2(50, 40), 0f, ColorSky),
        new BuildingSpot("진강회관", new Vector2(810, 730), new Vector2(28, 24), 0f, ColorLavender),
        new BuildingSpot("GS25_bottom", new Vector2(35, 650), new Vector2(26, 22), 0f, ColorMint),

        // 대형 건물 / 병원 (빨간 네모)
        new BuildingSpot("GS칼텍스", new Vector2(340, 215), new Vector2(30, 28), 0f, ColorMint),
        new BuildingSpot("양녕요양병원", new Vector2(355, 320), new Vector2(80, 60), 0f, ColorHospitalPink),
        new BuildingSpot("갈마그랜드프라자", new Vector2(255, 545), new Vector2(55, 42), 0f, ColorLavender),

        // 대형 아파트 단지 (노란 네모)
        new BuildingSpot("105", new Vector2(130, 520), new Vector2(45, 35), 0f, ColorYellow),
        new BuildingSpot("103", new Vector2(130, 610), new Vector2(45, 35), 0f, ColorYellow),
        new BuildingSpot("갈마1단지아파트", new Vector2(50, 590), new Vector2(46, 38), 0f, ColorPink),
        // V5(x=800) 세로 도로가 이 구역 한가운데를 지나가므로, 전부 x=620~780 사이(도로 왼쪽)에 몰아서 배치
        new BuildingSpot("쌍용갈마아파트_1", new Vector2(650, 305), new Vector2(42, 32), 0f, ColorSky),
        new BuildingSpot("쌍용갈마아파트_2", new Vector2(730, 300), new Vector2(42, 32), 0f, ColorSky),
        new BuildingSpot("쌍용갈마아파트_3", new Vector2(700, 390), new Vector2(42, 32), 0f, ColorSky),
        new BuildingSpot("쌍용갈마아파트_4", new Vector2(650, 440), new Vector2(42, 32), 0f, ColorSky),
        new BuildingSpot("쌍용할인마트", new Vector2(730, 460), new Vector2(50, 38), 0f, ColorYellow),
        new BuildingSpot("광도푸른아파트", new Vector2(730, 650), new Vector2(85, 68), 0f, ColorLavender),

        // 남쪽 빌라 단지(201,301~307) - 갈마로 대각선 각도에 맞춰 살짝 회전시켜서 줄지어 있는 느낌을 줌
        new BuildingSpot("201", new Vector2(220, 740), new Vector2(34, 26), 15f, ColorYellow),
        new BuildingSpot("202", new Vector2(280, 675), new Vector2(32, 24), 15f, ColorYellow),
        new BuildingSpot("203", new Vector2(380, 745), new Vector2(34, 26), 15f, ColorYellow),
        new BuildingSpot("301", new Vector2(230, 600), new Vector2(45, 35), 15f, ColorYellow),
        new BuildingSpot("302", new Vector2(310, 570), new Vector2(45, 35), 15f, ColorYellow),
        new BuildingSpot("303", new Vector2(280, 650), new Vector2(45, 35), 15f, ColorYellow),
        new BuildingSpot("304", new Vector2(390, 580), new Vector2(45, 35), 15f, ColorYellow),
        new BuildingSpot("305", new Vector2(350, 660), new Vector2(45, 35), 15f, ColorYellow),
        new BuildingSpot("306", new Vector2(460, 600), new Vector2(45, 35), 15f, ColorYellow),
        new BuildingSpot("307", new Vector2(480, 670), new Vector2(34, 26), 15f, ColorYellow),
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

            // 3. 도로/인도/공원을 깐 다음, 라벨 붙은 주요 건물(빨강=공원 옆 대형 건물, 노랑=아파트)부터 배치
            foreach (BuildingSpot spot in Buildings)
            {
                PlaceBuilding(root.transform, spot.Name, spot.PixelCenter, spot.PixelSize, spot.RotationDeg, spot.Color, placed);
            }

            // 라벨 없는 파란 건물들(빌라 느낌의 작은 건물 여러 채)도 사진에서 뭉쳐 있는 자리마다 격자로 채움 -
            // 랜덤으로 아무데나 채우는 게 아니라, 도로 블록 안쪽 여백(로드 경계에서 확실히 떨어진 범위)만 지정해서 채움
            // V5(x=800) 세로 도로 왼쪽만 씀 - 오른쪽은 도로/곡선 사이 틈이 너무 좁아서 뺌
            PlaceBuildingGrid(root.transform, "TopClusterL", 615f, 20f, 780f, 145f, 2, 2, ColorSky, placed);
            PlaceBuildingGrid(root.transform, "Col1_RowB", 30f, 180f, 140f, 240f, 1, 2, ColorMint, placed);
            PlaceBuildingGrid(root.transform, "Col2_RowB", 180f, 180f, 280f, 240f, 1, 2, ColorLavender, placed);
            PlaceBuildingGrid(root.transform, "Col1_RowC", 68f, 305f, 98f, 475f, 1, 3, ColorPink, placed);

            // ResolvePlacement는 도로/인도 폭을 원/캡슐로 근사한 값이라 이론상으로는 안 겹쳐야 하지만,
            // 여러 도로가 한꺼번에 밀어내는 구석에서는 근사 계산이 어긋나 실제로 인도에 살짝 걸치는 경우가 남았음.
            // 그래서 마지막에 실제 RoadMesh/SidewalkMesh 콜라이더에 대고 진짜 겹치는지 레이캐스트로 확인하고,
            // 겹치면 그 자리 주변을 나선형으로 훑어서 안 겹치는 가장 가까운 자리로 옮기는 보정을 한 번 더 함
            EnsureBuildingsClearOfRoads(root.transform, roadMesh.GetComponent<MeshCollider>(), sidewalkMesh.GetComponent<MeshCollider>());

            // 공원 두 곳에 벤치/가로등/쓰레기통/그네/미끄럼틀/분수대 소품을 몇 개씩 흩어 놓음(장식용이라
            // 건물처럼 엄격하게 안 겹치게 밀어내진 않고, 공원 영역 안쪽 여백에서만 랜덤하게 고름)
            for (int parkIndex = 0; parkIndex < Parks.Length; parkIndex++)
            {
                PlaceParkProps(root.transform, Parks[parkIndex], parkIndex);
            }
        }

        // 배달 스팟까지 다 붙었으면 네비메시를 구움 - 도로/인도/공원(걸을 수 있는 바닥)은 NavigationStatic으로
        // 마크됐고 건물도 같이 마크돼서 구멍(장애물)으로 자동 반영됨
        NavMeshBuilder.BuildNavMesh();

        Debug.Log("ReferenceMapPreview 생성 완료 - Town 오브젝트 옆(x=600)에 놓여있음. 라벨 건물 " + Buildings.Length + "개 + 격자로 채운 파란 건물 묶음, 전부 도로/인도와 겹치지 않게 자동으로 거리를 뒀어요. 랜덤 채움 건물은 더 이상 안 만들어요.");
    }

    // 배치 다 끝난 뒤 실제 콜라이더 기준으로 도로/인도와 겹치는 건물이 있으면, 겹치지 않는 자리를 찾을 때까지
    // 원래 위치를 중심으로 점점 더 넓은 원을 그리며(16방향 x 최대 20단계, 1m 간격) 훑어서 옮김.
    // 근사 계산(ResolvePlacement)과 달리 진짜 메시 콜라이더에 레이를 쏴서 확인하기 때문에 결과가 확정적임
    // 색깔 큐브는 오브젝트 자신에게 Renderer가 있지만, 실제 3D 모델(FBX)은 렌더러가 자식 메시에 있는
    // 경우가 대부분이라 GetComponentInChildren으로 찾아야 함 - 못 찾으면(둘 다 없으면) null
    private static bool TryGetCombinedBounds(Transform t, out Bounds bounds)
    {
        Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int r = 1; r < renderers.Length; r++)
        {
            bounds.Encapsulate(renderers[r].bounds);
        }

        return true;
    }

    private static void EnsureBuildingsClearOfRoads(Transform root, MeshCollider roadCollider, MeshCollider sidewalkCollider)
    {
        List<Transform> buildings = new List<Transform>();
        foreach (Transform child in root)
        {
            if (child.name.StartsWith("Building_") && TryGetCombinedBounds(child, out _))
            {
                buildings.Add(child);
            }
        }

        foreach (Transform t in buildings)
        {
            if (!IsOverlappingRoadOrSidewalk(t, roadCollider, sidewalkCollider))
            {
                continue;
            }

            Vector3 originalPos = t.position;
            bool resolved = false;
            Vector3 bestRoadClearCandidate = originalPos;
            bool foundRoadClearCandidate = false;

            // 1차: 도로/인도뿐 아니라 다른 건물과도 안 겹치는 완전히 깨끗한 자리를 우선 찾음
            for (int step = 1; step <= 40 && !resolved; step++)
            {
                float radius = step * 1f;
                for (int a = 0; a < 32; a++)
                {
                    float angle = a * (360f / 32f) * Mathf.Deg2Rad;
                    Vector3 candidate = originalPos + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    t.position = candidate;

                    if (IsOverlappingRoadOrSidewalk(t, roadCollider, sidewalkCollider))
                    {
                        continue;
                    }

                    if (!foundRoadClearCandidate)
                    {
                        // 도로/인도와는 이미 안 겹치는 첫 후보 - 다른 건물과 겹쳐도 최소한 이 자리는 확보해둠
                        foundRoadClearCandidate = true;
                        bestRoadClearCandidate = candidate;
                    }

                    if (IsOverlappingOtherBuilding(t, buildings))
                    {
                        continue;
                    }

                    resolved = true;
                    break;
                }
            }

            if (!resolved)
            {
                // 2차: 완전히 깨끗한 자리는 못 찾았어도, 도로/인도와만 안 겹치는 자리는 있었다면 그걸로 확정함 -
                // 사용자가 명시적으로 요구한 건 "도로/인도와 안 겹치는 것"이고, 건물끼리 살짝 겹치는 건 그보다 덜 중요함
                if (foundRoadClearCandidate)
                {
                    t.position = bestRoadClearCandidate;
                    resolved = true;
                }
                else
                {
                    t.position = originalPos;
                    Debug.LogWarning($"[ReferenceMapPreview] {t.gameObject.name} 주변에서 도로/인도와 안 겹치는 자리를 못 찾았어요 - 수동으로 위치/크기 조정이 필요할 수 있어요.");
                }
            }
        }
    }

    private static bool IsOverlappingRoadOrSidewalk(Transform t, MeshCollider roadCollider, MeshCollider sidewalkCollider)
    {
        if (!TryGetCombinedBounds(t, out Bounds b))
        {
            return false;
        }

        const int N = 3;
        for (int ix = 0; ix < N; ix++)
        {
            for (int iz = 0; iz < N; iz++)
            {
                float fx = ix / (float)(N - 1);
                float fz = iz / (float)(N - 1);
                float x = Mathf.Lerp(b.min.x, b.max.x, fx);
                float z = Mathf.Lerp(b.min.z, b.max.z, fz);
                Ray ray = new Ray(new Vector3(x, 50f, z), Vector3.down);
                if (roadCollider.Raycast(ray, out _, 100f)) return true;
                if (sidewalkCollider.Raycast(ray, out _, 100f)) return true;
            }
        }

        return false;
    }

    private static bool IsOverlappingOtherBuilding(Transform self, List<Transform> allBuildings)
    {
        if (!TryGetCombinedBounds(self, out Bounds selfBounds))
        {
            return false;
        }

        foreach (Transform other in allBuildings)
        {
            if (other == self) continue;
            if (TryGetCombinedBounds(other, out Bounds otherBounds) && selfBounds.Intersects(otherBounds)) return true;
        }

        return false;
    }

    // 도로/교차점/공원/다른 건물과 벌어져 보이도록 밀어낼 때 추가로 두는 여유 간격(미터)
    private const float ClearanceMargin = 3.5f;

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

        Quaternion rotation = Quaternion.Euler(0f, rotationDeg, 0f);

        if (TryGetBuildingModel(name, out RealModelDef model))
        {
            SpawnRealModel(parent, "Building_" + name, model, center, rotation);
        }
        else
        {
            float height = Mathf.Lerp(3f, 12f, Mathf.InverseLerp(20f, 100f, Mathf.Max(pixelSize.x, pixelSize.y)));

            GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = "Building_" + name;
            building.transform.SetParent(parent, false);
            building.transform.localPosition = center + new Vector3(0f, height * 0.5f, 0f);
            building.transform.localRotation = rotation;
            building.transform.localScale = new Vector3(width, height, depth);
            ApplyColor(building, color);
            MarkNavStatic(building);
        }

        // 기존 TownGenerator처럼 건물마다 배달 스팟을 하나씩 붙임(건물 바로 앞쪽에, 회전된 건물이면 그 방향으로)
        Vector3 spotPos = center + rotation * new Vector3(0f, 0f, -(depth * 0.5f + 1.5f));
        spotPos.y = 0.3f;
        GameObject spot = new GameObject("Building_" + name + "_Spot");
        spot.transform.SetParent(parent, false);
        spot.transform.localPosition = spotPos;
        BoxCollider spotCollider = spot.AddComponent<BoxCollider>();
        spotCollider.isTrigger = true;
        spotCollider.size = new Vector3(3f, 2f, 3f);
        spot.AddComponent<DeliverySpot>().SetCategory(GetSpotCategory(name));

        placed.Add((center, buildingRadius));
        return true;
    }

    // 라벨 없는 파란 건물 뭉치(빌라 느낌) 배치용 - 지정한 사각 영역(도로/인도 경계에서 이미 떨어뜨려 놓은 안쪽 여백)을
    // cols x rows 격자로 나눠서 칸마다 건물을 하나씩 채움. 칸 크기의 55%만 채워서 밀어내기 여유를 넉넉히 둠
    private static void PlaceBuildingGrid(Transform parent, string prefix, float x0, float y0, float x1, float y1, int cols, int rows, Color color, List<(Vector3 pos, float radius)> placed)
    {
        float cellW = (x1 - x0) / cols;
        float cellH = (y1 - y0) / rows;
        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float cx = x0 + cellW * (c + 0.5f);
                float cy = y0 + cellH * (r + 0.5f);
                Vector2 size = new Vector2(cellW * 0.42f, cellH * 0.42f);
                PlaceBuilding(parent, prefix + "_" + idx, new Vector2(cx, cy), size, 0f, color, placed);
                idx++;
            }
        }
    }

    // 공원 영역 안쪽에 소품들을 고정된 상대 위치(공원 크기 비율)로 몇 개씩 배치 - 건물과 달리 장식용이라
    // ResolvePlacement 같은 엄격한 회피 로직 없이, 공원 중심에서 벗어난 자리에 나눠서만 놓음
    private static void PlaceParkProps(Transform parent, ParkArea park, int parkIndex)
    {
        Vector2 half = park.PixelSize * 0.5f;

        (RealModelDef model, Vector2 offsetFrac, float rotationDeg, string label)[] props = new[]
        {
            (PropFountain, new Vector2(0f, 0f), 0f, "Fountain"),
            (PropBench, new Vector2(-0.45f, 0.35f), 30f, "Bench_0"),
            (PropBench, new Vector2(0.45f, 0.35f), -30f, "Bench_1"),
            (PropStreetlamp, new Vector2(-0.55f, -0.5f), 0f, "Streetlamp_0"),
            (PropStreetlamp, new Vector2(0.55f, -0.5f), 0f, "Streetlamp_1"),
            (PropTrashCan, new Vector2(0.55f, 0.55f), 0f, "TrashCan"),
            (PropSwing, new Vector2(-0.55f, 0.5f), 0f, "Swing"),
            (PropSlide, new Vector2(-0.15f, -0.55f), 0f, "Slide"),
        };

        foreach (var p in props)
        {
            Vector2 pixelPos = park.PixelCenter + new Vector2(p.offsetFrac.x * half.x, p.offsetFrac.y * half.y);
            Vector3 groundPosition = PixelToWorld(pixelPos);
            Quaternion rotation = Quaternion.Euler(0f, p.rotationDeg, 0f);
            SpawnRealModel(parent, "Prop_" + park.Name + "_" + p.label + "_" + parkIndex, p.model, groundPosition, rotation);
        }
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

    // 마을을 다시 만들지 않고도(사용자가 손으로 옮긴 건물/공원 위치가 그대로 유지되게) 신호등 같은
    // 소품을 실제 교차로 자리에 놓을 수 있도록 주요 사거리(가로/세로 도로가 만나는 진짜 교차로만,
    // 지도 모서리나 곡선 이음매는 제외) 좌표를 로컬 좌표로 몇 개 공개해둠
    public static Vector3[] GetMainIntersectionLocalPositions()
    {
        Vector2[] pixelPoints =
        {
            J_H1_1, J_H1_2, J_H1_3, J_H1_4, J_H1_5,
            J_H2_1, J_H2_2, J_H2_3, J_H2_4,
            J_H3_1, J_H3_2, J_H3_3, J_H3_4,
            J_H4_1, J_H4_2, J_H4_3,
        };

        Vector3[] result = new Vector3[pixelPoints.Length];
        for (int i = 0; i < pixelPoints.Length; i++)
        {
            result[i] = PixelToWorld(pixelPoints[i]);
        }

        return result;
    }

    // 건물 좌표를 지도에서 눈대중으로 딴 거라 도로랑 겹치는 경우가 있었음(예: GS칼텍스, 갈마그랜드프라자, 빌라 306/307
    // 등이 도로 위에 놓였음) - 하나하나 손으로 좌표를 고치는 대신, 도로 중심선/교차점 채우기/공원/이미 놓인 다른 건물과
    // 너무 가까우면 자동으로 옆으로 밀어내는 방식으로 해결함. ClearanceMargin만큼은 항상 여유를 둬서 도로랑 아예 안 닿게 함
    private static Vector3 ResolvePlacement(Vector3 rawCenter, float buildingRadius, List<(Vector3 pos, float radius)> otherBuildings)
    {
        Vector3 pos = rawCenter;

        for (int pass = 0; pass < 20; pass++)
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
