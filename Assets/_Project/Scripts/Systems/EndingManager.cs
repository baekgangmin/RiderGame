using UnityEngine;

// 게임 마무리 - DayNightCycle이 정해둔 날짜(totalDays)를 다 돌면 OnGameEnd를 호출해주는데,
// 여기서 그걸 받아서 시간을 멈추고(Time.timeScale = 0) EndingUI에 최종 통계를 띄워달라고 함
public class EndingManager : MonoBehaviour
{
    private DayNightCycle dayNightCycle;

    void Awake()
    {
        dayNightCycle = FindObjectOfType<DayNightCycle>();
        if (dayNightCycle != null)
        {
            dayNightCycle.OnGameEnd += HandleGameEnd;
        }
    }

    void OnDestroy()
    {
        if (dayNightCycle != null)
        {
            dayNightCycle.OnGameEnd -= HandleGameEnd;
        }
    }

    private void HandleGameEnd()
    {
        if (EndingUI.Instance != null)
        {
            EndingUI.Instance.Show(dayNightCycle.totalDays, DeliveryManager.DeliveryCount, DeliveryManager.TotalEarned, DeliveryManager.Money);
        }

        // 플레이어 조작/물리/배달 타이머 등을 전부 한 번에 멈추는 가장 간단한 방법 - Time.time 기반인
        // DayNightCycle도 같이 멈춰서 엔딩 화면이 뜬 채로 더 진행되지 않음
        Time.timeScale = 0f;
    }
}
