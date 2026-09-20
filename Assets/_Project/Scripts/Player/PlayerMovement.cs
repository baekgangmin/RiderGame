using UnityEngine;

// 배달 라이더 - 기본 도보 이동 스크립트 (M1)
// CharacterController를 사용한 단순 이동 + 달리기(Shift). 점프 없음.
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("이동 속도")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;

    [Header("중력")]
    public float gravity = -9.81f;

    [Header("회전")]
    public float rotationSpeed = 10f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(horizontal, 0f, vertical);
        if (inputDir.magnitude > 1f)
        {
            inputDir.Normalize();
        }

        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

        if (inputDir.magnitude >= 0.1f)
        {
            controller.Move(inputDir * currentSpeed * Time.deltaTime);

            Quaternion targetRotation = Quaternion.LookRotation(inputDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // 경사로/바닥 밀착용 중력 (점프는 없음)
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
