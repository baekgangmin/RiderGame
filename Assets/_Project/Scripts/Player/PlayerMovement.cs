using UnityEngine;

// 배달 라이더 - 조향식 이동 (M1) + 스태미나 연동 달리기 (M5) + 자전거 탑승 (M5)
// 방향키(화살표)로만 조작. WASD는 사용하지 않음.
// 좌/우 = 제자리 회전(조향), 상/하 = 캐릭터가 보는 방향으로 전진/후진 (차량 조작과 비슷한 방식)
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

    [Header("회전(조향) - 좌우 방향키를 누르면 이 속도(도/초)로 제자리에서 회전함")]
    public float turnSpeed = 200f;

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

        // 방향키(화살표)만 사용 - 좌우는 제자리 회전(조향), 상하는 캐릭터가 보고 있는 방향으로 전진/후진.
        // (예전엔 카메라 기준으로 좌우 입력을 "옆으로 이동"시켰는데, 캐릭터가 회전하면 카메라도
        //  같이 따라 돌면서 "옆" 방향 자체가 계속 바뀌어버려 가만히 좌우 키만 눌러도 원을 그리며
        //  빙글빙글 도는 문제가 있었음. 좌우 키는 이동이 아니라 순수 회전으로만 처리해서 없앰)
        float horizontal = 0f;
        if (Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
        if (Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;

        float vertical = 0f;
        if (Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
        if (Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;

        if (Mathf.Abs(horizontal) >= 0.1f)
        {
            transform.Rotate(Vector3.up, horizontal * turnSpeed * Time.deltaTime);
        }

        bool isMovingForward = Mathf.Abs(vertical) >= 0.1f;
        Vector3 moveDir = isMovingForward ? transform.forward * vertical : Vector3.zero;

        bool wantsToRun = Input.GetKey(KeyCode.LeftShift) && isMovingForward;
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

        if (isMovingForward)
        {
            controller.Move(moveDir * currentSpeed * Time.deltaTime);
        }

        // 경사로/바닥 밀착용 중력 (점프는 없음)
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
