using UnityEngine;
using Crest;

/// <summary>
/// BoatProbes의 전진력을 강화하고 움직임을 개선하는 도구 (개선 버전)
/// </summary>
[RequireComponent(typeof(BoatProbes_AStar))]
public class BoatMovementEnhancer : MonoBehaviour
{
    [Header("전진력 증폭")]
    [Tooltip("엔진 파워 배율")]
    [UnityEngine.Range(1f, 10f)]
    public float enginePowerMultiplier = 4f;
    
    [Tooltip("AI 입력값 부스트")]
    [UnityEngine.Range(1f, 3f)]
    public float inputBoost = 2f;

    [Header("저항 감소")]
    [Tooltip("물 저항 감소 배율")]
    [UnityEngine.Range(0.1f, 1f)]
    public float dragReduction = 0.5f;

    [Header("회전 개선")]
    [Tooltip("회전 중 최소 속도 유지")]
    [UnityEngine.Range(0f, 1f)]
    public float speedDuringTurn = 0.9f;
    
    [Tooltip("회전만 할 때 최소 전진 입력")]
    [UnityEngine.Range(0f, 0.5f)]
    public float minimumForwardInput = 0.3f;

    [Header("가속 지원")]
    [Tooltip("저속 시 추가 힘 적용 임계값")]
    [UnityEngine.Range(0f, 5f)]
    public float lowSpeedThreshold = 3f;
    
    [Tooltip("추가 가속력")]
    [UnityEngine.Range(0f, 50f)]
    public float additionalForce = 15f;
    
    [Tooltip("추가 힘을 적용할 최소 전진 입력")]
    [UnityEngine.Range(0f, 1f)]
    public float minForwardInputForBoost = 0.3f;

    [Header("디버그")]
    public bool showDebugInfo = true;

    private BoatProbes_AStar _boatProbes;
    private Rigidbody _rb;
    
    // 원본 값 저장
    private float _originalEnginePower;
    private float _originalDragUp;
    private float _originalDragRight;
    private float _originalDragForward;

    void Awake()
    {
        _boatProbes = GetComponent<BoatProbes_AStar>();
        _rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        // 원본 값 저장
        _originalEnginePower = _boatProbes._enginePower;
        _originalDragUp = _boatProbes._dragInWaterUp;
        _originalDragRight = _boatProbes._dragInWaterRight;
        _originalDragForward = _boatProbes._dragInWaterForward;

        ApplyEnhancements();
    }

    void ApplyEnhancements()
    {
        // 엔진 파워 증가
        _boatProbes._enginePower = _originalEnginePower * enginePowerMultiplier;

        // 저항 감소
        _boatProbes._dragInWaterUp = _originalDragUp * dragReduction;
        _boatProbes._dragInWaterRight = _originalDragRight * dragReduction;
        _boatProbes._dragInWaterForward = _originalDragForward * dragReduction;

        if (showDebugInfo)
        {
            Debug.Log("=== 보트 움직임 강화 적용 ===");
            Debug.Log($"엔진 파워: {_originalEnginePower} → {_boatProbes._enginePower}");
            Debug.Log($"전방 저항: {_originalDragForward} → {_boatProbes._dragInWaterForward}");
            Debug.Log($"측면 저항: {_originalDragRight} → {_boatProbes._dragInWaterRight}");
        }
    }

    void Update()
    {
        EnhanceMovement();
    }

    void EnhanceMovement()
    {
        // 현재 AI 입력값 가져오기
        float currentForward = _boatProbes.AI_ForwardInput;
        float currentTurn = _boatProbes.AI_TurnInput;

        // 1. 전진 입력 부스트
        if (currentForward > 0.01f)
        {
            float boostedForward = currentForward * inputBoost;
            boostedForward = Mathf.Clamp01(boostedForward);
            
            // 회전 중에도 최소한의 속도 유지
            if (Mathf.Abs(currentTurn) > 0.5f)
            {
                boostedForward = Mathf.Max(boostedForward, speedDuringTurn);
            }
            
            _boatProbes.AI_ForwardInput = boostedForward;
        }
        else if (Mathf.Abs(currentTurn) > 0.1f)
        {
            // 회전만 하고 있으면 최소 전진 적용
            _boatProbes.AI_ForwardInput = minimumForwardInput;
        }

        // 2. 저속 시 추가 가속 지원 (개선)
        float currentSpeed = _rb.velocity.magnitude;
        bool needsBoost = currentSpeed < lowSpeedThreshold && currentForward > minForwardInputForBoost;
        
        if (needsBoost)
        {
            // 보트의 전방 방향으로 힘 추가
            Vector3 forceDirection = transform.forward;
            
            // 회전 중이면 힘의 방향을 약간 조정
            if (Mathf.Abs(currentTurn) > 0.3f)
            {
                // 회전 방향을 고려하여 힘의 방향 조정
                Vector3 turnDirection = transform.right * currentTurn;
                forceDirection = (forceDirection + turnDirection * 0.3f).normalized;
            }
            
            _rb.AddForce(forceDirection * additionalForce, ForceMode.Acceleration);
            
            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.Log($"추가 힘 적용! 현재 속도: {currentSpeed:F2} | 방향: {forceDirection}");
            }
        }

        if (showDebugInfo && Time.frameCount % 120 == 0)
        {
            Debug.Log($"전진: {_boatProbes.AI_ForwardInput:F2}, " +
                     $"회전: {currentTurn:F2}, " +
                     $"속도: {currentSpeed:F2} m/s");
        }
    }

    void OnValidate()
    {
        // Inspector에서 값 변경 시 실시간 적용
        if (Application.isPlaying && _boatProbes != null)
        {
            ApplyEnhancements();
        }
    }

    void OnDestroy()
    {
        // 원본 값 복원
        if (_boatProbes != null)
        {
            _boatProbes._enginePower = _originalEnginePower;
            _boatProbes._dragInWaterUp = _originalDragUp;
            _boatProbes._dragInWaterRight = _originalDragRight;
            _boatProbes._dragInWaterForward = _originalDragForward;
        }
    }
}