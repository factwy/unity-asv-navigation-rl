using UnityEngine;
using Pathfinding;
using Crest;
using RRT;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// RRT* 경로를 정확하게 추적하는 컨트롤러 - 100번 반복 실행 및 데이터 수집 버전 (수정됨)
/// </summary>
[RequireComponent(typeof(BoatProbes_AStar), typeof(AIPath))]
public class BoatAIController_RRT : MonoBehaviour
{
    [Header("RRT 경로 찾기")]
    public RRT.RRT rrtController;
    public Transform target;

    [Header("A* Movement (RRT 경로 추적용)")]
    public AIPath aiPath;
    
    [Tooltip("다음 웨이포인트까지의 거리")]
    public float nextWaypointDistance = 8f;

    [Header("Movement Control")]
    [Tooltip("목표 지점 정지 거리")]
    public float stoppingDistance = 12f;
    
    [Tooltip("감속 시작 각도")]
    public float slowDownAngle = 45f;
    
    [Tooltip("급감속 시작 각도")]
    public float sharpTurnAngle = 75f;
    
    [Tooltip("최대 전진 입력")]
    public float maxSpeedInput = 1f;
    
    [Tooltip("급회전 시 최대 속도")]
    [UnityEngine.Range(0f, 1f)]
    public float maxSpeedDuringSharpTurn = 0.4f;
    
    [Tooltip("일반 회전 시 최대 속도")]
    [UnityEngine.Range(0f, 1f)]
    public float maxSpeedDuringTurn = 0.75f;

    [Header("회전 제어 (개선됨)")]
    [Tooltip("회전 입력 배율 - 클수록 빠르게 회전")]
    [UnityEngine.Range(1f, 3f)]
    public float turnInputMultiplier = 2f;
    
    [Tooltip("최소 회전 입력값 (작은 각도에서도 회전 보장)")]
    [UnityEngine.Range(0.1f, 0.5f)]
    public float minTurnInput = 0.2f;

    [Header("예측 제어")]
    [Tooltip("앞서 볼 웨이포인트 수")]
    [UnityEngine.Range(0, 3)]
    public int lookAheadWaypoints = 1;
    
    [Tooltip("예측 거리 (m)")]
    public float lookAheadDistance = 10f;
    
    [Tooltip("속도 변화 부드러움")]
    [UnityEngine.Range(0.05f, 0.2f)]
    public float speedSmoothTime = 0.08f;

    [Header("경로 추적 정확도")]
    [Tooltip("경로 이탈 허용 거리")]
    public float maxPathDeviation = 8f;
    
    [Tooltip("웨이포인트 근접 시 감속")]
    [UnityEngine.Range(1f, 2f)]
    public float waypointSlowdownMultiplier = 1.2f;



    [Header("데이터 수집 설정")]
    [Tooltip("총 실행 횟수")]
    public int totalRuns = 100;
    
    [Tooltip("최대 실행 시간 (초) - 4분")]
    public float maxRunTime = 240f;
    
    [Tooltip("RRT 경로 대기 최대 시간 (초) - 30초")]
    public float maxPathWaitTime = 30f;
    
    [Tooltip("전복 감지 각도 (도)")]
    public float capsizeAngle = 60f;
    
    [Tooltip("좌표 기록 간격 (초)")]
    public float positionRecordInterval = 1f;
    
    [Tooltip("CSV 파일 저장 경로 (비워두면 Assets 폴더)")]
    public string csvSavePath = "";

    [Header("디버그")]
    public bool showDebugInfo = true;
    public bool showGizmos = true;
    public Color rrtPathColor = Color.green;
    public Color currentWaypointColor = Color.red;
    public Color lookAheadColor = Color.yellow;

    // 단일 실행 데이터 구조체
    [System.Serializable]
    public class RunData
    {
        public int runNumber;
        public float travelDistance;
        public float targetDistance;
        public bool isSuccess;
        public string failureReason; // "Success", "Timeout", "Capsized", "Collision", "NoPath"
        public int collisionCount;
        public float elapsedTime;
        public List<Vector2> positionHistory;

        public RunData(int run)
        {
            runNumber = run;
            travelDistance = 0f;
            targetDistance = 0f;
            isSuccess = false;
            failureReason = "InProgress";
            collisionCount = 0;
            elapsedTime = 0f;
            positionHistory = new List<Vector2>();
        }
    }

    // 실행 관리 변수
    private int _currentRun = 0;
    private List<RunData> _allRunsData = new List<RunData>();
    private RunData _currentRunData;
    private bool _isCollectingData = false;
    private bool _isRunActive = false;  // 현재 실행 활성 상태

    // 단일 실행 변수
    private float _startTime;
    private float _pathWaitStartTime;
    private bool _isTimerRunning;
    private bool _isWaitingForPath;
    private int _collisionCount = 0;
    private Vector3 _lastPosition;
    private float _totalDistance = 0f;

    private List<Vector3> _rrtPath;
    private int _currentWaypoint = 0;
    private bool _hasValidPath = false;

    private BoatProbes_AStar _boatProbes;
    private Rigidbody _rb;
    
    private float _currentSpeedLimit = 1f;
    private float _speedVelocity = 0f;

    // 초기 위치 및 회전 저장
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Vector3 _initialVelocity;
    private Vector3 _initialAngularVelocity;

    void Awake()
    {
        _boatProbes = GetComponent<BoatProbes_AStar>();
        _rb = GetComponent<Rigidbody>();
        
        if (aiPath == null)
            aiPath = GetComponent<AIPath>();

        if (rrtController == null)
            rrtController = GetComponent<RRT.RRT>();

        // 초기 상태 저장
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        _initialVelocity = Vector3.zero;
        _initialAngularVelocity = Vector3.zero;
    }

    void Start()
    {
        if (aiPath != null)
        {
            aiPath.canMove = false;
            aiPath.canSearch = false;
        }

        if (rrtController != null)
        {
            rrtController.OnPathFound.AddListener(OnRRTPathFound);
            Debug.Log("BoatAIController_RRT: RRT 경로 찾기 이벤트 연결 완료");
        }
        else
        {
            Debug.LogError("RRT 컨트롤러를 찾을 수 없습니다!");
            return;
        }

        // CSV 저장 경로 설정
        if (string.IsNullOrEmpty(csvSavePath))
        {
            csvSavePath = Application.dataPath;
        }

        // 데이터 수집 시작
        if (target != null)
        {
            _isCollectingData = true;
            StartNewRun();
        }
    }

    void StartNewRun()
    {
        // 실행 횟수 체크 (중요!)
        if (_currentRun >= totalRuns)
        {
            Debug.LogWarning($"<color=yellow>이미 {totalRuns}번 실행 완료. 추가 실행 중단.</color>");
            return;
        }

        // 코루틴으로 시작하여 순서 보장
        StopAllCoroutines();
        StartCoroutine(StartNewRunCoroutine());
    }

    IEnumerator StartNewRunCoroutine()
    {
        _currentRun++;
        _isRunActive = false; // 준비 단계에서는 비활성
        
        Debug.Log($"<color=cyan>==================== 실행 {_currentRun}/{totalRuns} 준비 중 ====================</color>");

        // 1단계: 데이터 초기화
        _currentRunData = new RunData(_currentRun);
        _collisionCount = 0;
        _totalDistance = 0f;
        _isTimerRunning = false;
        _isWaitingForPath = false;
        _hasValidPath = false;
        _currentWaypoint = 0;
        _rrtPath = null;

        // 2단계: 보트 물리 및 위치 완전 초기화
        ResetBoat();
        
        // 3단계: 물리 안정화 대기 (3 프레임)
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        // 4단계: 위치 검증 및 재초기화
        float distanceFromStart = Vector3.Distance(transform.position, _initialPosition);
        if (distanceFromStart > 1f)
        {
            Debug.LogWarning($"<color=orange>초기 위치 이탈 감지 ({distanceFromStart:F2}m) - 재초기화</color>");
            ResetBoat();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        // 5단계: 목표 거리 확인 (너무 가까우면 문제)
        Vector3 targetPos2D = new Vector3(target.position.x, 0, target.position.z);
        Vector3 currentPos2D = new Vector3(transform.position.x, 0, transform.position.z);
        float distanceToTarget = Vector3.Distance(currentPos2D, targetPos2D);
        
        if (distanceToTarget < stoppingDistance * 2)
        {
            Debug.LogWarning($"<color=orange>시작 위치가 목표에 너무 가까움 ({distanceToTarget:F2}m) - 초기화 문제 가능성</color>");
        }

        Debug.Log($"<color=cyan>실행 {_currentRun} 초기화 완료 - 시작 위치: {transform.position}, 목표 거리: {distanceToTarget:F2}m</color>");

        // 6단계: RRT 경로 재탐색 트리거
        if (rrtController != null)
        {
            rrtController.RestartSearch();
            Debug.Log("RRT* 경로 재탐색 시작...");
        }

        // 7단계: 타이머 및 실행 시작
        _startTime = Time.time;
        _pathWaitStartTime = Time.time;
        _isTimerRunning = true;
        _isWaitingForPath = true;
        _isRunActive = true; // 이제 실행 활성화
        _lastPosition = transform.position; // 거리 계산용 위치 저장

        Debug.Log($"<color=cyan>==================== 실행 {_currentRun}/{totalRuns} 시작 ====================</color>");

        // 8단계: 좌표 기록 코루틴 시작
        StartCoroutine(RecordPositionCoroutine());
    }

    void ResetBoat()
    {
        // 입력 즉시 중지
        if (_boatProbes != null)
        {
            _boatProbes.AI_ForwardInput = 0;
            _boatProbes.AI_TurnInput = 0;
        }

        // 물리 완전 정지
        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.Sleep(); // 물리 일시 정지
        }

        // 위치 및 회전 강제 설정
        transform.position = _initialPosition;
        transform.rotation = _initialRotation;

        // 물리 재활성화
        if (_rb != null)
        {
            _rb.WakeUp();
            // 초기 속도 설정 (보통 0)
            _rb.velocity = _initialVelocity;
            _rb.angularVelocity = _initialAngularVelocity;
        }

        // 속도 제어 초기화
        _currentSpeedLimit = 1f;
        _speedVelocity = 0f;

        // 마지막 위치 저장
        _lastPosition = transform.position;

        Debug.Log($"<color=green>보트 초기화 완료 - 위치: {transform.position}, 회전: {transform.rotation.eulerAngles}</color>");
    }

    System.Collections.IEnumerator RecordPositionCoroutine()
    {
        while (_isTimerRunning && _isRunActive)
        {
            if (_currentRunData != null)
            {
                Vector2 currentPos = new Vector2(transform.position.x, transform.position.z);
                _currentRunData.positionHistory.Add(currentPos);
            }
            yield return new WaitForSeconds(positionRecordInterval);
        }
    }

    public void OnRRTPathFound()
    {
        // 실행이 활성화되지 않았으면 무시 (이전 실행의 경로 이벤트일 수 있음)
        if (!_isRunActive || !_isWaitingForPath)
        {
            Debug.LogWarning($"<color=orange>RRT 경로 발견 이벤트 무시 - 실행 비활성 상태 (Run: {_currentRun})</color>");
            return;
        }

        Debug.Log($"<color=yellow>실행 {_currentRun} - RRT* 경로 발견! 경로를 추출합니다...</color>");

        _rrtPath = ExtractPathFromRRT();

        if (_rrtPath == null || _rrtPath.Count == 0)
        {
            Debug.LogError($"실행 {_currentRun} - RRT 경로 추출 실패!");
            return;
        }

        _currentWaypoint = 0;
        _hasValidPath = true;
        _isWaitingForPath = false;

        Debug.Log($"<color=cyan>실행 {_currentRun} - RRT 경로 추출 완료! 총 {_rrtPath.Count}개의 웨이포인트</color>");
        Debug.Log($"시작: {_rrtPath[0]}, 목표: {_rrtPath[_rrtPath.Count - 1]}");
    }

    List<Vector3> ExtractPathFromRRT()
    {
        if (rrtController == null || rrtController._tree == null)
            return null;

        if (!rrtController._tree.HasFoundPath)
            return null;

        List<Vector3> path = new List<Vector3>();
        Node currentNode = rrtController._tree.TargetNode;

        if (currentNode == null)
            return null;

        while (currentNode != null)
        {
            path.Add(currentNode.Position);
            currentNode = currentNode.Parent;
        }

        path.Reverse();
        return path;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_isRunActive) return;
        
        if (collision.gameObject.CompareTag("Obstacle") || collision.gameObject.CompareTag("Boundary"))
        {
            _collisionCount++;
            if (_currentRunData != null)
            {
                _currentRunData.collisionCount = _collisionCount;
            }
            Debug.Log($"<color=red>장애물과 충돌! (현재 {_collisionCount}회)</color>");
        }
    }

    void FixedUpdate()
    {
        // 데이터 수집 중이 아니거나 실행이 비활성이면 중단
        if (!_isCollectingData || !_isRunActive)
            return;

        // 타임아웃 체크
        if (_isTimerRunning && (Time.time - _startTime) > maxRunTime)
        {
            Debug.LogWarning($"<color=orange>실행 {_currentRun} 타임아웃! (4분 초과)</color>");
            EndCurrentRun(false, "Timeout");
            return;
        }

        // 경로 대기 타임아웃 체크 (중요!)
        if (_isWaitingForPath && (Time.time - _pathWaitStartTime) > maxPathWaitTime)
        {
            Debug.LogWarning($"<color=orange>실행 {_currentRun} 경로 탐색 타임아웃! ({maxPathWaitTime}초 초과)</color>");
            EndCurrentRun(false, "NoPath");
            return;
        }

        // 전복 체크
        float rollAngle = Mathf.Abs(transform.eulerAngles.z);
        if (rollAngle > 180f) rollAngle = 360f - rollAngle;
        
        float pitchAngle = Mathf.Abs(transform.eulerAngles.x);
        if (pitchAngle > 180f) pitchAngle = 360f - pitchAngle;

        if (rollAngle > capsizeAngle || pitchAngle > capsizeAngle)
        {
            Debug.LogWarning($"<color=orange>실행 {_currentRun} 전복! (Roll: {rollAngle:F1}°, Pitch: {pitchAngle:F1}°)</color>");
            EndCurrentRun(false, "Capsized");
            return;
        }

        // 이동 거리 계산
        if (_isTimerRunning && _currentRunData != null)
        {
            float distanceMoved = Vector3.Distance(transform.position, _lastPosition);
            _totalDistance += distanceMoved;
            _lastPosition = transform.position;
            _currentRunData.travelDistance = _totalDistance;
        }

        // 경로 대기 중
        if (!_hasValidPath || _rrtPath == null || _rrtPath.Count == 0)
        {
            if (_boatProbes != null)
            {
                _boatProbes.AI_ForwardInput = 0;
                _boatProbes.AI_TurnInput = 0;
            }
            
            if (showDebugInfo && Time.frameCount % 120 == 0)
            {
                Debug.Log($"RRT 경로 대기 중... ({Time.time - _pathWaitStartTime:F1}초 경과)");
            }
            return;
        }

        if (_currentWaypoint >= _rrtPath.Count)
        {
            if (_boatProbes != null)
            {
                _boatProbes.AI_ForwardInput = 0;
                _boatProbes.AI_TurnInput = 0;
            }
            return;
        }

        Vector3 currentPos2D = new Vector3(_rb.position.x, 0, _rb.position.z);
        Vector3 waypointPos2D = new Vector3(_rrtPath[_currentWaypoint].x, 0, _rrtPath[_currentWaypoint].z);
        Vector3 targetPos2D = new Vector3(target.position.x, 0, target.position.z);

        float distanceToWaypoint = Vector3.Distance(currentPos2D, waypointPos2D);
        float distanceToTarget = Vector3.Distance(currentPos2D, targetPos2D);
        
        // 목표까지의 거리 업데이트
        if (_currentRunData != null)
        {
            _currentRunData.targetDistance = distanceToTarget;
        }

        // 웨이포인트 도달 체크
        if (distanceToWaypoint < nextWaypointDistance)
        {
            _currentWaypoint++;
            Debug.Log($"<color=green>웨이포인트 {_currentWaypoint - 1} 통과! (남은: {_rrtPath.Count - _currentWaypoint})</color>");
            
            if (_currentWaypoint >= _rrtPath.Count)
            {
                Debug.Log("<color=cyan>모든 웨이포인트 통과!</color>");
                return;
            }
            
            waypointPos2D = new Vector3(_rrtPath[_currentWaypoint].x, 0, _rrtPath[_currentWaypoint].z);
            distanceToWaypoint = Vector3.Distance(currentPos2D, waypointPos2D);
        }

        // 목표 도착 확인
        if (distanceToTarget < stoppingDistance && _isTimerRunning)
        {
            Debug.Log($"<color=cyan>실행 {_currentRun} 성공! 목표 지점 도착!</color>");
            EndCurrentRun(true, "Success");
            return;
        }

        // ==========================================
        // 핵심: 개선된 제어 로직
        // ==========================================
        
        // 1. 목표 지점 계산 (예측 제어)
        Vector3 lookAheadTarget = GetLookAheadPoint();
        Vector3 desiredDirection = (lookAheadTarget - currentPos2D).normalized;

        // 2. 회전 제어 (개선됨)
        float angleToTarget = Vector3.SignedAngle(transform.forward, desiredDirection, Vector3.up);
        float absAngle = Mathf.Abs(angleToTarget);
        
        // 회전 입력 계산 - 더 강력하게
        float baseTurnInput = Mathf.Clamp(angleToTarget / 30f, -1f, 1f); // 30도 기준으로 정규화
        float turnInput = baseTurnInput * turnInputMultiplier;
        
        // 최소 회전 입력 보장 (작은 각도에서도 회전)
        if (absAngle > 5f)
        {
            float sign = Mathf.Sign(angleToTarget);
            turnInput = Mathf.Max(Mathf.Abs(turnInput), minTurnInput) * sign;
        }
        
        turnInput = Mathf.Clamp(turnInput, -1f, 1f);
        if (_boatProbes != null)
        {
            _boatProbes.AI_TurnInput = turnInput;
        }

        // 3. 전진 제어 (개선됨)
        float targetSpeed = CalculateTargetSpeed(absAngle, distanceToTarget, distanceToWaypoint);
        
        // 부드러운 속도 변화
        _currentSpeedLimit = Mathf.SmoothDamp(
            _currentSpeedLimit, 
            targetSpeed, 
            ref _speedVelocity, 
            speedSmoothTime
        );
        
        if (_boatProbes != null)
        {
            _boatProbes.AI_ForwardInput = _currentSpeedLimit;
        }

        // 4. 경로 이탈 모니터링
        CheckPathDeviation(currentPos2D);

        // 디버그 정보
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            float currentSpeed = _rb.velocity.magnitude;
            Debug.Log($"Run[{_currentRun}] WP[{_currentWaypoint}/{_rrtPath.Count}] | " +
                      $"거리: {distanceToWaypoint:F1}m | " +
                      $"각도: {angleToTarget:F1}° | " +
                      $"속도: {currentSpeed:F1}m/s ({_currentSpeedLimit:F2}) | " +
                      $"회전: {turnInput:F2}");
        }
    }

    void EndCurrentRun(bool success, string reason)
    {
        if (!_isRunActive)
        {
            Debug.LogWarning($"<color=yellow>실행이 이미 종료되었습니다. (Run {_currentRun})</color>");
            return;
        }

        _isRunActive = false;
        _isTimerRunning = false;
        _isWaitingForPath = false;
        
        // 입력 중지
        if (_boatProbes != null)
        {
            _boatProbes.AI_ForwardInput = 0;
            _boatProbes.AI_TurnInput = 0;
        }

        // 코루틴 중지
        StopAllCoroutines();

        // 데이터 기록
        if (_currentRunData != null)
        {
            _currentRunData.elapsedTime = Time.time - _startTime;
            _currentRunData.isSuccess = success;
            _currentRunData.failureReason = reason;
            
            // 완료된 실행만 저장 (InProgress 제거!)
            _allRunsData.Add(_currentRunData);
            
            Debug.Log($"<color=yellow>실행 {_currentRun} 종료 - {reason} | " +
                      $"시간: {_currentRunData.elapsedTime:F2}s | " +
                      $"이동: {_currentRunData.travelDistance:F2}m | " +
                      $"목표거리: {_currentRunData.targetDistance:F2}m | " +
                      $"충돌: {_currentRunData.collisionCount}회</color>");
        }

        // 다음 실행 또는 종료
        if (_currentRun < totalRuns)
        {
            // 다음 실행 시작 (1.5초 후 - 물리 및 RRT 안정화 시간 확보)
            Debug.Log($"<color=cyan>1.5초 후 실행 {_currentRun + 1} 시작 준비...</color>");
            StartCoroutine(WaitAndStartNextRun());
        }
        else
        {
            // 모든 실행 완료 - 데이터 저장
            SaveDataToCSV();
            
            Debug.Log($"<color=cyan>╔═══════════════════════════════════════╗\n" +
                      $"║   모든 {totalRuns}회 실행 완료!   ║\n" +
                      $"╚═══════════════════════════════════════╝</color>");

            _isCollectingData = false;

            #if UNITY_EDITOR
            EditorApplication.isPlaying = false;
            #endif
        }
    }

    IEnumerator WaitAndStartNextRun()
    {
        // 1.5초 대기 (물리 안정화 + RRT 초기화 시간)
        yield return new WaitForSeconds(1.5f);
        
        // 다음 실행 시작
        StartNewRun();
    }

    void SaveDataToCSV()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"BoatAI_RunData_{timestamp}.csv";
        string filePath = System.IO.Path.Combine(csvSavePath, fileName);

        try
        {
            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // 헤더 작성
                writer.WriteLine("Run,TravelDistance(m),TargetDistance(m),Success,FailureReason,CollisionCount,ElapsedTime(s),PositionHistory");

                // 완료된 실행 데이터만 작성 (InProgress 필터링)
                int savedCount = 0;
                foreach (var runData in _allRunsData)
                {
                    // InProgress 상태는 저장하지 않음!
                    if (runData.failureReason == "InProgress")
                    {
                        Debug.LogWarning($"<color=yellow>Run {runData.runNumber}: InProgress 상태로 저장 건너뜀</color>");
                        continue;
                    }

                    StringBuilder positionHistory = new StringBuilder();
                    foreach (var pos in runData.positionHistory)
                    {
                        positionHistory.Append($"({pos.x:F2},{pos.y:F2});");
                    }

                    writer.WriteLine($"{runData.runNumber}," +
                                   $"{runData.travelDistance:F2}," +
                                   $"{runData.targetDistance:F2}," +
                                   $"{runData.isSuccess}," +
                                   $"{runData.failureReason}," +
                                   $"{runData.collisionCount}," +
                                   $"{runData.elapsedTime:F2}," +
                                   $"\"{positionHistory.ToString().TrimEnd(';')}\"");
                    
                    savedCount++;
                }

                Debug.Log($"<color=green>데이터 저장 완료: {filePath}</color>");
                Debug.Log($"<color=green>총 {savedCount}개의 유효한 실행 데이터 저장됨</color>");
            }

            // 통계 출력
            PrintStatistics();
        }
        catch (Exception e)
        {
            Debug.LogError($"CSV 저장 실패: {e.Message}");
        }
    }

    void PrintStatistics()
    {
        int successCount = 0;
        int timeoutCount = 0;
        int capsizedCount = 0;
        int collisionCount = 0;
        int noPathCount = 0;
        float avgTime = 0f;
        float avgDistance = 0f;
        float avgCollisions = 0f;
        int validRuns = 0;

        foreach (var data in _allRunsData)
        {
            // InProgress는 통계에서 제외
            if (data.failureReason == "InProgress")
                continue;

            validRuns++;
            if (data.isSuccess) successCount++;
            if (data.failureReason == "Timeout") timeoutCount++;
            if (data.failureReason == "Capsized") capsizedCount++;
            if (data.failureReason == "Collision") collisionCount++;
            if (data.failureReason == "NoPath") noPathCount++;
            avgTime += data.elapsedTime;
            avgDistance += data.travelDistance;
            avgCollisions += data.collisionCount;
        }

        if (validRuns > 0)
        {
            avgTime /= validRuns;
            avgDistance /= validRuns;
            avgCollisions /= validRuns;
        }

        Debug.Log($"<color=cyan>========== 통계 ==========\n" +
                  $"유효한 실행: {validRuns}/{totalRuns}\n" +
                  $"성공: {successCount}/{validRuns} ({(validRuns > 0 ? successCount * 100f / validRuns : 0):F1}%)\n" +
                  $"타임아웃: {timeoutCount}회\n" +
                  $"전복: {capsizedCount}회\n" +
                  $"충돌 실패: {collisionCount}회\n" +
                  $"경로 없음: {noPathCount}회\n" +
                  $"평균 시간: {avgTime:F2}초\n" +
                  $"평균 이동거리: {avgDistance:F2}m\n" +
                  $"평균 충돌: {avgCollisions:F2}회\n" +
                  $"==========================</color>");
    }

    Vector3 GetLookAheadPoint()
    {
        if (lookAheadWaypoints == 0 && lookAheadDistance == 0)
        {
            // 예측 없이 현재 웨이포인트만 사용
            return new Vector3(_rrtPath[_currentWaypoint].x, 0, _rrtPath[_currentWaypoint].z);
        }
        
        Vector3 currentPos2D = new Vector3(_rb.position.x, 0, _rb.position.z);
        float accumulatedDistance = 0f;
        
        for (int i = _currentWaypoint; i < _rrtPath.Count - 1; i++)
        {
            Vector3 wpPos = new Vector3(_rrtPath[i].x, 0, _rrtPath[i].z);
            Vector3 nextWpPos = new Vector3(_rrtPath[i + 1].x, 0, _rrtPath[i + 1].z);
            
            float segmentLength = Vector3.Distance(wpPos, nextWpPos);
            accumulatedDistance += segmentLength;
            
            if (accumulatedDistance >= lookAheadDistance || i >= _currentWaypoint + lookAheadWaypoints)
            {
                return nextWpPos;
            }
        }
        
        return new Vector3(_rrtPath[_rrtPath.Count - 1].x, 0, _rrtPath[_rrtPath.Count - 1].z);
    }

    float CalculateTargetSpeed(float absAngle, float distanceToTarget, float distanceToWaypoint)
    {
        float targetSpeed = maxSpeedInput;
        
        // 1. 각도에 따른 속도 제한 (완화됨)
        if (absAngle > sharpTurnAngle)
        {
            targetSpeed = maxSpeedDuringSharpTurn;
        }
        else if (absAngle > slowDownAngle)
        {
            float t = (absAngle - slowDownAngle) / (sharpTurnAngle - slowDownAngle);
            targetSpeed = Mathf.Lerp(maxSpeedDuringTurn, maxSpeedDuringSharpTurn, t);
        }
        else
        {
            // 작은 각도에서는 거의 최대 속도
            targetSpeed = maxSpeedInput;
        }
        
        // 2. 목표 지점 근처에서 감속
        if (distanceToTarget < stoppingDistance * 2.5f)
        {
            float distanceFactor = Mathf.Clamp01(distanceToTarget / (stoppingDistance * 2.5f));
            targetSpeed *= distanceFactor;
        }
        
        // 3. 웨이포인트 근접 시 약간 감속
        float slowdownDistance = nextWaypointDistance * waypointSlowdownMultiplier;
        if (distanceToWaypoint < slowdownDistance)
        {
            float waypointFactor = Mathf.Clamp01(distanceToWaypoint / slowdownDistance);
            targetSpeed *= Mathf.Max(waypointFactor, 0.6f); // 최소 60% 속도 유지
        }
        
        // 4. 최소 속도 보장
        return Mathf.Max(targetSpeed, 0.3f);
    }

    void CheckPathDeviation(Vector3 currentPos)
    {
        if (_currentWaypoint >= _rrtPath.Count - 1) return;
        
        Vector3 wpPos = new Vector3(_rrtPath[_currentWaypoint].x, 0, _rrtPath[_currentWaypoint].z);
        Vector3 nextWpPos = new Vector3(_rrtPath[_currentWaypoint + 1].x, 0, _rrtPath[_currentWaypoint + 1].z);
        
        Vector3 pathDirection = (nextWpPos - wpPos).normalized;
        Vector3 toPosition = currentPos - wpPos;
        float projection = Vector3.Dot(toPosition, pathDirection);
        Vector3 closestPoint = wpPos + pathDirection * Mathf.Clamp(projection, 0, Vector3.Distance(wpPos, nextWpPos));
        float deviation = Vector3.Distance(currentPos, closestPoint);
        
        if (deviation > maxPathDeviation && showDebugInfo && Time.frameCount % 120 == 0)
        {
            Debug.LogWarning($"<color=orange>⚠ 경로 이탈 경고! 편차: {deviation:F2}m (허용: {maxPathDeviation}m)</color>");
        }
    }

    Pathfinding.Path ConvertRRTPathToAstarPath()
    {
        if (_rrtPath == null || _rrtPath.Count < 2) return null;

        var path = ABPath.Construct(_rrtPath[0], _rrtPath[_rrtPath.Count - 1], null);
        path.vectorPath = new List<Vector3>(_rrtPath);
        return path;
    }

    float CalculatePathLength()
    {
        if (_rrtPath == null || _rrtPath.Count < 2) return 0f;

        float length = 0f;
        for (int i = 0; i < _rrtPath.Count - 1; i++)
            length += Vector3.Distance(_rrtPath[i], _rrtPath[i + 1]);
        
        return length;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos || _rrtPath == null || _rrtPath.Count == 0)
            return;

        Gizmos.color = rrtPathColor;
        for (int i = 0; i < _rrtPath.Count - 1; i++)
        {
            Gizmos.DrawLine(_rrtPath[i], _rrtPath[i + 1]);
        }
        
        for (int i = 0; i < _rrtPath.Count; i++)
        {
            Gizmos.color = Color.Lerp(Color.green, Color.blue, i / (float)_rrtPath.Count);
            Gizmos.DrawWireSphere(_rrtPath[i], 0.3f);
        }

        if (_hasValidPath && _currentWaypoint < _rrtPath.Count)
        {
            Gizmos.color = currentWaypointColor;
            Gizmos.DrawSphere(_rrtPath[_currentWaypoint], 0.7f);
            Gizmos.DrawLine(transform.position, _rrtPath[_currentWaypoint]);
        }

        if (_hasValidPath && Application.isPlaying)
        {
            Vector3 lookAhead = GetLookAheadPoint();
            Gizmos.color = lookAheadColor;
            Gizmos.DrawWireSphere(lookAhead, 0.9f);
            Gizmos.DrawLine(transform.position, lookAhead);
            
            Vector3 direction = (lookAhead - transform.position).normalized;
            Gizmos.DrawRay(transform.position + Vector3.up, direction * 5f);
        }

        if (_rrtPath.Count > 0)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_rrtPath[0], 0.6f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_rrtPath[_rrtPath.Count - 1], 1f);
        }
    }

    void OnDestroy()
    {
        if (rrtController != null)
            rrtController.OnPathFound.RemoveListener(OnRRTPathFound);
        
        StopAllCoroutines();
        
        // 종료 시 데이터 저장 (플레이 중단 시)
        if (_isCollectingData && _allRunsData.Count > 0)
        {
            Debug.Log("<color=yellow>게임 종료 - 현재까지의 데이터 저장 중...</color>");
            SaveDataToCSV();
        }
    }

    // 에디터에서 플레이 모드 종료 시 호출
    void OnApplicationQuit()
    {
        if (_isCollectingData && _allRunsData.Count > 0)
        {
            Debug.Log("<color=yellow>애플리케이션 종료 - 데이터 저장 중...</color>");
            SaveDataToCSV();
        }
    }
}