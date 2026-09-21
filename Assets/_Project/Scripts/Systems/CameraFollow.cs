using UnityEngine;

// 3인칭 추적 카메라 - 플레이어가 도는 방향에 맞춰 카메라도 같이 돌아감
public class CameraFollow : MonoBehaviour
{
    [Header("따라갈 대상")]
    public Transform target;

    [Header("카메라 위치 오프셋 (대상의 로컬 기준: 뒤로 -Z, 위로 +Y)")]
    public Vector3 offset = new Vector3(0f, 4f, -6f);

    [Header("위치 부드러움 (높을수록 빠르게 따라붙음)")]
    public float positionSmoothSpeed = 10f;

    [Header("회전 부드러움 (높을수록 빠르게 따라 돎) - 너무 높으면 플레이어가 방향 바꿀 때 카메라가 홱 돌아서 어지러움")]
    public float rotationSmoothSpeed = 6f;

    [Header("대상을 항상 바라볼지")]
    public bool lookAtTarget = true;

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        // 오프셋을 대상(플레이어)의 회전 기준으로 변환 -> 플레이어가 돌면 카메라도 같이 돎
        Vector3 desiredPosition = target.position + target.rotation * offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-positionSmoothSpeed * Time.deltaTime));

        if (lookAtTarget)
        {
            Vector3 lookPoint = target.position + Vector3.up * 1.5f;
            Quaternion targetRotation = Quaternion.LookRotation(lookPoint - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime));
        }
    }
}
