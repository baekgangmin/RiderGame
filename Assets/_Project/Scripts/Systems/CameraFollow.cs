using UnityEngine;

// 3인칭 추적 카메라 - 플레이어를 자동으로 따라다님 (M1 보조)
public class CameraFollow : MonoBehaviour
{
    [Header("따라갈 대상")]
    public Transform target;

    [Header("카메라 위치 오프셋 (대상 기준)")]
    public Vector3 offset = new Vector3(0f, 4f, -6f);

    [Header("부드러움")]
    public float smoothSpeed = 8f;

    [Header("대상을 항상 바라볼지")]
    public bool lookAtTarget = true;

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        if (lookAtTarget)
        {
            transform.LookAt(target.position + Vector3.up * 1.5f);
        }
    }
}
