using UnityEngine;

// 캐릭터 모델(CharacterModel)의 Animator에 이동 상태를 전달 - 걷기/뛰기 애니메이션 전환만 담당
// 탈것에 탑승 중일 때는 다리 애니메이션이 겹쳐 보이지 않도록 제자리(Idle)로 고정해둠
public class CharacterVisualAnimator : MonoBehaviour
{
    // Varco3D에서 받은 "뛰기"/"뒤로 뛰기" 클립 자체가 Hips 뼈대를 처음부터 끝까지 y축으로 크게 돌려서
    // 구워놨음(걷기/뒤로 걷기/대기 클립은 정상) - 클립을 다시 구울 수 없으니 재생 후(LateUpdate) 매 프레임
    // Hips 회전에 반대 방향 보정을 곱해서 덮어씀. 상태 전환 진행률만큼 블렌딩해서 부드럽게 켜지고 꺼지게 함.
    private const string RunStateName = "Run";
    private static readonly Quaternion RunHipsCorrection = Quaternion.Euler(0f, -90f, 0f);
    private const string RunBackStateName = "RunBack";
    private static readonly Quaternion RunBackHipsCorrection = Quaternion.Euler(0f, 72f, 0f);

    private Animator animator;
    private PlayerMovement playerMovement;
    private Transform hips;
    private Transform leftArm;
    private Transform rightArm;
    private Transform leftForeArm;
    private Transform rightForeArm;

    void Awake()
    {
        animator = GetComponent<Animator>();
        playerMovement = GetComponentInParent<PlayerMovement>();

        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            if (t.name == "Hips") hips = t;
            else if (t.name == "LeftArm") leftArm = t;
            else if (t.name == "RightArm") rightArm = t;
            else if (t.name == "LeftForeArm") leftForeArm = t;
            else if (t.name == "RightForeArm") rightForeArm = t;
        }
    }

    void Update()
    {
        if (animator == null || playerMovement == null)
        {
            return;
        }

        bool moving = playerMovement.IsMovingForward && !playerMovement.IsRiding;
        bool running = moving && playerMovement.IsBoosting;
        bool backward = moving && playerMovement.IsBackward;

        animator.SetBool("IsMoving", moving);
        animator.SetBool("IsRunning", running);
        animator.SetBool("IsBackward", backward);
    }

    // 지정한 상태 이름에 있는 정도(현재 상태거나, 그 상태로/그 상태에서 전환 중인 비율)를 0~1로 계산
    float GetStateWeight(string stateName)
    {
        float weight = animator.GetCurrentAnimatorStateInfo(0).IsName(stateName) ? 1f : 0f;
        if (animator.IsInTransition(0))
        {
            float currentIs = animator.GetCurrentAnimatorStateInfo(0).IsName(stateName) ? 1f : 0f;
            float nextIs = animator.GetNextAnimatorStateInfo(0).IsName(stateName) ? 1f : 0f;
            weight = Mathf.Lerp(currentIs, nextIs, animator.GetAnimatorTransitionInfo(0).normalizedTime);
        }
        return weight;
    }

    void LateUpdate()
    {
        if (animator == null || hips == null)
        {
            return;
        }

        float runWeight = GetStateWeight(RunStateName);
        if (runWeight > 0f)
        {
            hips.localRotation = Quaternion.Slerp(hips.localRotation, RunHipsCorrection * hips.localRotation, runWeight);
        }

        float runBackWeight = GetStateWeight(RunBackStateName);
        if (runBackWeight > 0f)
        {
            hips.localRotation = Quaternion.Slerp(hips.localRotation, RunBackHipsCorrection * hips.localRotation, runBackWeight);
        }

        ApplyRidingArmPose();
    }

    // 탈것에 탑승 중일 때 팔을 핸들 쪽으로 뻗은 것처럼 보이게 함 - 전용 라이딩 애니메이션이 없어서
    // 팔(위팔) 뼈를 핸들 목표 지점을 바라보도록 직접 회전시키는 방식으로 흉내냄(IK 없이 단순 LookAt 방식)
    private void ApplyRidingArmPose()
    {
        if (leftArm == null || rightArm == null || leftForeArm == null || rightForeArm == null)
        {
            return;
        }
        if (playerMovement == null || !playerMovement.IsRiding)
        {
            return;
        }

        // Bicycle은 탑승 중일 때 CharacterModel과 같은 부모(Player) 밑에 자식으로 붙어있음
        Transform bicycle = transform.parent != null ? transform.parent.Find("Bicycle") : null;
        if (bicycle == null)
        {
            return;
        }

        if (!TryGetHandlebarTarget(bicycle, out Transform handlebarModel, out Vector3 handleLocalPoint))
        {
            return;
        }

        // handleLocalPoint는 handlebarModel 기준 "로컬" 좌표라 회전과 무관하게 항상 같은 상대위치를 가리킴 -
        // TransformPoint로 변환하면 handlebarModel의 현재 회전(조향 포함)을 그대로 따라가서 흔들리지 않음.
        // (주의: Renderer.bounds 같은 월드 AABB로 매 프레임 다시 계산하면 회전할 때마다 모양이 달라져서
        // 팔이 이상한 방향을 보는 문제가 있었음 - 그래서 로컬 고정값을 씀)
        Vector3 handleCenter = handlebarModel.TransformPoint(handleLocalPoint);
        Vector3 leftGrip = handleCenter + transform.right * -0.18f;
        Vector3 rightGrip = handleCenter + transform.right * 0.18f;

        // 아래팔은 곧게 펴서(로컬 회전 0) 위팔 방향 그대로 손이 목표점을 향하게 함
        leftForeArm.localRotation = Quaternion.identity;
        rightForeArm.localRotation = Quaternion.identity;

        Vector3 leftDir = (leftGrip - leftArm.position).normalized;
        Vector3 rightDir = (rightGrip - rightArm.position).normalized;
        leftArm.rotation = Quaternion.FromToRotation(leftArm.up, leftDir) * leftArm.rotation;
        rightArm.rotation = Quaternion.FromToRotation(rightArm.up, rightDir) * rightArm.rotation;
    }

    // 킥보드 모델 자체에 (-90,-90,0) 회전이 미리 박혀있어서 로컬 축이 "앞/위"와 직관적으로 안 맞음 -
    // 그래서 바운즈로 어림짐작하는 대신, 실제로 태워보면서 맞았던 월드 좌표를 킥보드 로컬 좌표로
    // 역변환해서 구한 고정값을 씀(회전과 무관하게 항상 같은 상대위치를 가리킴)
    private static readonly Vector3 KickboardHandleLocalPoint = new Vector3(0.5128f, 0.2351f, 0.4080f);

    // 탈것 종류별 손잡이(핸들바) 모델과, 그 모델 기준 손잡이 목표 지점(로컬 좌표)을 반환 -
    // 아직 킥보드만 자세를 맞춰서 다른 탈것은 false를 반환함(기본 Idle 팔 자세 유지)
    private bool TryGetHandlebarTarget(Transform bicycle, out Transform handlebarModel, out Vector3 handleLocalPoint)
    {
        Transform kickboardModel = bicycle.Find("KickboardModel");
        if (kickboardModel != null && kickboardModel.gameObject.activeSelf)
        {
            handlebarModel = kickboardModel;
            handleLocalPoint = KickboardHandleLocalPoint;
            return true;
        }

        handlebarModel = null;
        handleLocalPoint = Vector3.zero;
        return false;
    }
}
