using UnityEngine;

// 배달 라이더 - 카메라 기준 이동 (M1) + 스태미나 연동 달리기 (M5) + 자전거 탑승 (M5)
// 방향키(화살표)로만 조작. WASD는 사용하지 않음.
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("카메라 참조 (비워두면 Camera.main 자동 사용)")]
    public Transform cameraTransform;

    [Header("도보 이동 속도")]
    public float walkSpeed = 3.2f;
    public float runSpeed = 5.5f;

    [Header("자전거 탑승 시 이동 속도 (탈것 탑승 시 VehicleCatalog 값으로 덮어써짐)")]
    public float bikeWalkSpeed = 6.5f;
    public float bikeRunSpeed = 9.5f;

    [Header("중력")]
    public float gravity = -9.81f;

    [Header("회전")]
    public float rotationSpeed = 10f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private bool isRiding;
    private bool isFuelVehicle;

    // 지금 쉬프트로 가속 중인지 (오토바이/자동차 연료 소모량 계산에 사용됨)
    public bool IsBoosting { get; private set; }

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    // VehicleMount 같은 탈것 스크립트에서 호출해서 탑승 상태를 켜고 끔
    // rideWalkSpeed/rideRunSpeed를 주면(0보다 크면) 그 탈것 종류에 맞는 속도로 덮어씀 (자전거/킥보드/오토바이/자동차마다 속도가 다름)
    // usesFuel이 true면(오토바이/자동차) 쉬프트로 가속할 때 체력이 아니라 연료를 씀 - VehicleMount에서 처리
    public void SetRiding(bool riding, float rideWalkSpeed = 0f, float rideRunSpeed = 0f, bool usesFuel = false)
    {
        isRiding = riding;
        isFuelVehicle = riding && usesFuel;

        if (riding)
        {
            if (rideWalkSpeed > 0f)
            {
                bikeWalkSpeed = rideWalkSpeed;
            }
            if (rideRunSpeed > 0f)
            {
                bikeRunSpeed = rideRunSpeed;
            }
        }
    }

    void Update()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        // 방향키(화살표)만 사용
        float horizontal = 0f;
        if (Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
        if (Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;

        float vertical = 0f;
        if (Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
        if (Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;

        Vector3 rawInput = new Vector3(horizontal, 0f, vertical);
        if (rawInput.magnitude > 1f)
        {
            rawInput.Normalize();
        }

        // 카메라가 보는 방향(수평만) 기준으로 입력을 변환
        Vector3 moveDir = Vector3.zero;
        if (rawInput.magnitude >= 0.1f && cameraTransform != null)
        {
            Vector3 camForward = cameraTransform.forward;
            camForward.y = 0f;
            camForward.Normalize();

            Vector3 camRight = cameraTransform.right;
            camRight.y = 0f;
            camRight.Normalize();

            moveDir = camForward * vertical + camRight * horizontal;
            if (moveDir.magnitude > 1f)
            {
                moveDir.Normalize();
            }
        }
        else if (rawInput.magnitude >= 0.1f)
        {
            // 카메라를 못 찾은 경우 대비용 (안전장치)
            moveDir = rawInput;
        }

        bool wantsToRun = Input.GetKey(KeyCode.LeftShift) && moveDir.magnitude >= 0.1f;
        float baseSpeed = isRiding ? bikeWalkSpeed : walkSpeed;
        float boostedSpeed = isRiding ? bikeRunSpeed : runSpeed;
        float currentSpeed = baseSpeed;
        bool boostingNow = false;

        if (wantsToRun)
        {
            if (isFuelVehicle)
            {
                // 오토바이/자동차는 엔진 힘으로 가속 - 체력이 아니라 연료가 빨리 닳음 (VehicleMount.HandleFuelDrain에서 처리)
                currentSpeed = boostedSpeed;
                boostingNow = true;
            }
            else
            {
                // 도보/자전거/킥보드는 사람 힘이라 스태미나(페달링 힘)가 소모됨 - 기획서 기준
                bool canRun = StaminaSystem.Instance == null || StaminaSystem.Instance.TryUseStamina(Time.deltaTime);
                if (canRun)
                {
                    currentSpeed = boostedSpeed;
                    boostingNow = true;
                }
            }
        }

        IsBoosting = boostingNow;

        if (moveDir.magnitude >= 0.1f)
        {
            controller.Move(moveDir * currentSpeed * Time.deltaTime);

            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // 경사로/바닥 밀착용 중력 (점프는 없음)
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
