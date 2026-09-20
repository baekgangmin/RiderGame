using UnityEngine;
using UnityEngine.AI;

// 배달 경로 안내선 (M7) - 수락한 배달의 픽업/배달지까지 실제로 걸어갈 수 있는 길을 바닥에 선으로 그려줌
// NavMesh로 건물을 피해가는 경로를 계산해서 LineRenderer로 표시함
// 월드 공간에 그려지기 때문에 3인칭 화면뿐 아니라 핸드폰 미니맵(위에서 찍는 카메라)에도 자동으로 같이 찍힘
public class RouteGuide : MonoBehaviour
{
    public float recomputeInterval = 0.5f;
    public float lineHeight = 0.15f;
    public float lineWidth = 0.8f;
    public Color pickupColor = new Color(1f, 0.85f, 0.1f);
    public Color deliveryColor = new Color(0.2f, 0.6f, 1f);

    private LineRenderer line;
    private Transform player;
    private float timer = 999f; // 시작하자마자 바로 첫 경로를 계산하도록 크게 잡아둠
    private NavMeshPath path;

    void Start()
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            player = playerGO.transform;
        }

        GameObject lineGO = new GameObject("RouteLine");
        line = lineGO.AddComponent<LineRenderer>();
        line.positionCount = 0;
        line.widthMultiplier = lineWidth;
        line.numCapVertices = 4;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            line.material = new Material(shader);
        }

        path = new NavMeshPath();
    }

    void Update()
    {
        DeliveryJobManager manager = DeliveryJobManager.Instance;
        DeliverySpot target = null;
        Color lineColor = pickupColor;

        if (manager != null)
        {
            if (manager.State == DeliveryJobManager.JobState.GoingToPickup)
            {
                target = manager.CurrentPickupSpot;
                lineColor = pickupColor;
            }
            else if (manager.State == DeliveryJobManager.JobState.GoingToDelivery)
            {
                target = manager.CurrentDeliverySpot;
                lineColor = deliveryColor;
            }
        }

        if (target == null || player == null || line == null)
        {
            if (line != null && line.positionCount != 0)
            {
                line.positionCount = 0;
            }
            return;
        }

        ApplyLineColor(lineColor);

        timer += Time.deltaTime;
        if (timer >= recomputeInterval)
        {
            timer = 0f;
            RecomputePath(target.transform.position);
        }
    }

    void RecomputePath(Vector3 targetPos)
    {
        bool ok = false;

        NavMeshHit fromHit;
        NavMeshHit toHit;
        if (NavMesh.SamplePosition(player.position, out fromHit, 5f, NavMesh.AllAreas) &&
            NavMesh.SamplePosition(targetPos, out toHit, 5f, NavMesh.AllAreas))
        {
            if (NavMesh.CalculatePath(fromHit.position, toHit.position, NavMesh.AllAreas, path) && path.corners.Length > 1)
            {
                line.positionCount = path.corners.Length;
                for (int i = 0; i < path.corners.Length; i++)
                {
                    Vector3 c = path.corners[i];
                    c.y = lineHeight;
                    line.SetPosition(i, c);
                }
                ok = true;
            }
        }

        if (!ok)
        {
            // NavMesh 경로를 못 찾으면(베이크 전이거나 경로 단절) 직선으로라도 방향은 보여줌
            Vector3 from = player.position;
            from.y = lineHeight;
            Vector3 to = targetPos;
            to.y = lineHeight;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }
    }

    void ApplyLineColor(Color c)
    {
        if (line.material == null)
        {
            return;
        }

        if (line.material.HasProperty("_BaseColor"))
        {
            line.material.SetColor("_BaseColor", c);
        }
        else
        {
            line.material.color = c;
        }
    }
}
