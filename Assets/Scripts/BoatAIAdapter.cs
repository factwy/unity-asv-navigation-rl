using UnityEngine;
using Pathfinding;
using Crest;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 필요한 컴포넌트들을 명시
[RequireComponent(typeof(AIPath), typeof(BoatProbes_AStar))]
public class BoatAIAdapter : MonoBehaviour
{
    
    private AIPath _aiPath;
    private BoatProbes_AStar _boatProbes;

    // --- 타이머 및 충돌 카운터 변수 ---
    private float _startTime;
    private bool _isTimerRunning;
    private int _collisionCount = 0;
    // ---------------------------------


    void Awake()
    {
        // 시작할 때 필요한 컴포넌트들을 미리 찾아 저장합니다.
        _aiPath = GetComponent<AIPath>();
        _boatProbes = GetComponent<BoatProbes_AStar>();
    }

    void Start()
    {
        // AIPath의 목표(destination)가 설정되면 타이머를 시작합니다.
        // AIDestinationSetter가 Target을 설정해 줄 것입니다.
        if (_aiPath.hasPath || _aiPath.destination != null)
        {
            StartTimer();
        }
    }

    void StartTimer()
    {
        _startTime = Time.time;
        _isTimerRunning = true;
        // Debug.Log("AIPath 이동 시작! 시간 측정을 시작합니다.");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            _collisionCount++;
            // Debug.Log($"장애물과 충돌! (현재 {_collisionCount}회)");
        }
    }

    void Update()
    {
        // --- AIPath의 상태를 기반으로 BoatProbes 제어 ---

        // AIPath가 계산한 '희망 속도' 벡터를 가져옵니다.
        Vector3 desiredVelocity = _aiPath.desiredVelocity;

        // 1. 전진 입력 계산
        // 희망 속도의 크기를 기반으로 전진/후진을 결정합니다.
        float forwardInput = desiredVelocity.magnitude / _aiPath.maxSpeed;
        _boatProbes.AI_ForwardInput = Mathf.Clamp01(forwardInput);

        // Debug.Log($"희망 속도(Magnitude): {_aiPath.desiredVelocity.magnitude}, 최종 전진 입력: {forwardInput}");

        // 2. 회전 입력 계산
        if (desiredVelocity.magnitude > 0.1f) // 아주 느릴 때는 회전하지 않음
        {
            float angle = Vector3.SignedAngle(transform.forward, desiredVelocity, Vector3.up);
            _boatProbes.AI_TurnInput = Mathf.Clamp(angle / 45f, -1f, 1f);
        }
        else
        {
            _boatProbes.AI_TurnInput = 0;
        }

        // --- 목표 지점 도착 확인 및 결과 출력 ---
        // AIPath.reachedDestination은 목표에 도달했는지 알려주는 편리한 속성입니다.
        if (_isTimerRunning && _aiPath.reachedDestination)
        {
            _isTimerRunning = false;
            float elapsedTime = Time.time - _startTime;
            
            // Debug.Log($"<color=cyan>목표 지점 도착!\n총 소요 시간: {elapsedTime.ToString("F2")}초\n장애물 충돌 횟수: {_collisionCount}회</color>");

            #if UNITY_EDITOR
                EditorApplication.isPlaying = false;
            #endif
        }
    }
}