using UnityEngine;
using Unity.MLAgents; // ML-Agents를 사용하기 위해 필요

/// <summary>
/// 플레이 시작부터 세션이 끝날 때까지의 시간을 측정하고 기록합니다.
/// 동일한 게임 오브젝트에 Agent 컴포넌트가 있으면 각 에피소드의 길이를 측정하고,
/// 없으면 플레이 모드가 중지될 때까지의 전체 플레이 시간을 측정합니다.
/// </summary>
public class PlaytimeLogger : MonoBehaviour
{
    private float sessionStartTime;
    private bool isTiming = false;

    // ML-Agents 관련 변수
    private Agent agent;
    private int initialEpisodeCount;

    // 도착 확인 관련 변수
    [SerializeField] private LayerMask targetLayer;
    private int _arrivalCount = 0;
    private int _trialCount = 1;

    private void Awake()
    {
        // 이 컴포넌트가 부착된 게임 오브젝트에서 Agent 컴포넌트를 찾아봅니다.
        agent = GetComponent<Agent>();
    }

    private void Start()
    {
        // 플레이가 시작되면 타이머를 시작합니다.
        StartTimer();

        if (agent != null)
        {
            // Agent 컴포넌트가 있는 경우, 현재 완료된 에피소드 수를 기록합니다.
            initialEpisodeCount = agent.CompletedEpisodes;
            Debug.Log("[ML-Agents Mode] 각 에피소드의 시간 측정을 시작합니다.");
        }
        else
        {
            Debug.Log("[Standard Mode] 전체 플레이 시간 측정을 시작합니다.");
        }
    }

    private void Update()
    {
        // Agent가 있고, 타이머가 실행 중이며, 완료된 에피소드 수가 증가했을 때
        if (agent != null && isTiming && agent.CompletedEpisodes > initialEpisodeCount)
        {
            LogElapsedTime("에피소드");
            
            // 다음 에피소드 측정을 위해 타이머와 카운터를 리셋합니다.
            StartTimer();
            initialEpisodeCount = agent.CompletedEpisodes;
        }
    }

    private void OnDestroy()
    {
        // Agent가 없는 경우에만 이 로직을 실행합니다 (플레이 중지 시 호출됨).
        if (agent == null && isTiming)
        {
            LogElapsedTime("전체 플레이");
        }
    }

    /// <summary>
    /// 타이머를 현재 시간으로 초기화하고 측정을 시작합니다.
    /// </summary>
    private void StartTimer()
    {
        sessionStartTime = Time.time;
        isTiming = true;
    }

    /// <summary>
    /// 경과 시간을 계산하고 Debug.Log로 출력합니다.
    /// </summary>
    /// <param name="sessionName">로그에 표시될 세션의 이름 (예: "에피소드", "전체 플레이")</param>
    private void LogElapsedTime(string sessionName)
    {
        if (!isTiming) return;

        float elapsedTime = Time.time - sessionStartTime;
        Debug.Log($"[PlaytimeLogger] {sessionName} 시간: {elapsedTime:F2}초");
        Debug.Log($"총 시도횟수 : {_trialCount++}, 성공 횟수 : {_arrivalCount}");
        isTiming = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & targetLayer) != 0)
        {
            _arrivalCount++;
        }
    }
}