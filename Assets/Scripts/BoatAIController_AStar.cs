using UnityEngine;
using Pathfinding;
using Crest;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// A* pathfinding controller for boat navigation.
/// Follows waypoints along the calculated path to reach the target.
/// </summary>
[RequireComponent(typeof(BoatProbes_AStar), typeof(Seeker))]
public class BoatAIController_AStar : MonoBehaviour
{
    #region Constants
    private const float DEFAULT_TURN_ANGLE_DIVISOR = 45f;
    private const float DEFAULT_CAPSIZED_ANGLE = 60f;
    private const float ANGLE_NORMALIZATION_THRESHOLD = 180f;
    private const float ANGLE_NORMALIZATION_VALUE = 360f;
    #endregion

    #region Serialized Fields
    [Header("Path Target")]
    public Transform target;

    [Header("Pathfinding")]
    public float nextWaypointDistance = 5f;
    public float pathUpdateInterval = 0.5f;

    [Header("Movement")]
    public float stoppingDistance = 10f;
    public float slowDownAngle = 45f;
    public float maxSpeedInput = 1f;

    [Header("Experiment Settings")]
    public float maxExperimentTime = 240f;
    public float capsizeAngle = DEFAULT_CAPSIZED_ANGLE;
    #endregion

    #region Private Fields
    private float _startTime;
    private bool _isRunning = false;
    private int _collisionCount = 0;
    private float _totalDistance = 0f;
    private Vector3 _lastPosition;
    private Path _path;
    private int _currentWaypoint = 0;
    private Seeker _seeker;
    private BoatProbes_AStar _boatProbes;
    private Rigidbody _rigidbody;

    // Reusable Vector3 to avoid allocations
    private Vector3 _currentPosition2D;
    private Vector3 _waypointPosition2D;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        _seeker = GetComponent<Seeker>();
        _boatProbes = GetComponent<BoatProbes_AStar>();
        _rigidbody = GetComponent<Rigidbody>();

        if (_seeker == null)
        {
            Debug.LogError($"[{GetType().Name}] Seeker component is missing!");
        }

        if (_boatProbes == null)
        {
            Debug.LogError($"[{GetType().Name}] BoatProbes_AStar component is missing!");
        }

        if (_rigidbody == null)
        {
            Debug.LogError($"[{GetType().Name}] Rigidbody component is missing!");
        }
    }

    private void Start()
    {
        InvokeRepeating(nameof(UpdatePath), 0f, pathUpdateInterval);
        StartNavigation();
    }

    private void FixedUpdate()
    {
        if (!_isRunning)
        {
            StopBoat();
            return;
        }

        UpdateTravelDistance();

        if (CheckCapsized())
        {
            EndNavigation(false, "전복");
            return;
        }

        if (CheckTimeout())
        {
            EndNavigation(false, "시간초과");
            return;
        }

        FollowPath();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_isRunning) return;

        if (collision.gameObject.CompareTag("Obstacle"))
        {
            _collisionCount++;
            Debug.Log($"장애물 충돌! (현재 {_collisionCount}회)");
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Starts the AI navigation and begins tracking statistics.
    /// </summary>
    public void StartNavigation()
    {
        _isRunning = true;
        _startTime = Time.time;
        _lastPosition = _rigidbody.position;
        _totalDistance = 0f;
        _collisionCount = 0;

        Debug.Log("AI 네비게이션 시작");
    }
    #endregion

    #region Private Methods - Pathfinding
    private void UpdatePath()
    {
        if (target != null && _seeker != null && _seeker.IsDone())
        {
            _seeker.StartPath(_rigidbody.position, target.position, OnPathComplete);
        }
    }

    private void OnPathComplete(Path p)
    {
        if (!p.error)
        {
            _path = p;
            _currentWaypoint = 0;
        }
    }

    private void FollowPath()
    {
        if (_path == null || _currentWaypoint >= _path.vectorPath.Count)
        {
            StopBoat();
            return;
        }

        UpdatePosition2D();
        UpdateWaypointPosition2D();

        float distanceToWaypoint = Vector3.Distance(_currentPosition2D, _waypointPosition2D);

        if (distanceToWaypoint < nextWaypointDistance)
        {
            _currentWaypoint++;
            if (_currentWaypoint >= _path.vectorPath.Count) return;
            UpdateWaypointPosition2D();
        }

        Vector3 waypointDirection = (_waypointPosition2D - _currentPosition2D).normalized;
        UpdateTargetPosition2D();
        float distanceToTarget = Vector3.Distance(_currentPosition2D, _waypointPosition2D);

        if (distanceToTarget < stoppingDistance)
        {
            EndNavigation(true, "목표 도달");
            return;
        }

        ApplyMovement(waypointDirection, distanceToTarget);
    }

    private void UpdatePosition2D()
    {
        _currentPosition2D.x = _rigidbody.position.x;
        _currentPosition2D.y = 0;
        _currentPosition2D.z = _rigidbody.position.z;
    }

    private void UpdateWaypointPosition2D()
    {
        if (_path != null && _currentWaypoint < _path.vectorPath.Count)
        {
            _waypointPosition2D.x = _path.vectorPath[_currentWaypoint].x;
            _waypointPosition2D.y = 0;
            _waypointPosition2D.z = _path.vectorPath[_currentWaypoint].z;
        }
    }

    private void UpdateTargetPosition2D()
    {
        if (target != null)
        {
            _waypointPosition2D.x = target.position.x;
            _waypointPosition2D.y = 0;
            _waypointPosition2D.z = target.position.z;
        }
    }
    #endregion

    #region Private Methods - Movement Control
    private void ApplyMovement(Vector3 direction, float distanceToTarget)
    {
        float angleToWaypoint = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
        _boatProbes.AI_TurnInput = Mathf.Clamp(angleToWaypoint / DEFAULT_TURN_ANGLE_DIVISOR, -1f, 1f);

        float forwardInput = maxSpeedInput;

        if (distanceToTarget < stoppingDistance)
        {
            forwardInput = Mathf.Clamp01(distanceToTarget / stoppingDistance);
        }
        else if (Mathf.Abs(angleToWaypoint) > slowDownAngle)
        {
            float angleExcess = Mathf.Abs(angleToWaypoint) - slowDownAngle;
            float slowdownFactor = 1f - (angleExcess / 90f);
            forwardInput *= Mathf.Clamp01(slowdownFactor);
        }

        _boatProbes.AI_ForwardInput = forwardInput;
    }

    private void StopBoat()
    {
        if (_boatProbes != null)
        {
            _boatProbes.AI_ForwardInput = 0;
            _boatProbes.AI_TurnInput = 0;
        }
    }
    #endregion

    #region Private Methods - State Checking
    private void UpdateTravelDistance()
    {
        float distanceMoved = Vector3.Distance(_rigidbody.position, _lastPosition);
        _totalDistance += distanceMoved;
        _lastPosition = _rigidbody.position;
    }

    private bool CheckCapsized()
    {
        float rollAngle = NormalizeAngle(transform.eulerAngles.x);
        float pitchAngle = NormalizeAngle(transform.eulerAngles.z);

        return Mathf.Abs(rollAngle) > capsizeAngle || Mathf.Abs(pitchAngle) > capsizeAngle;
    }

    private bool CheckTimeout()
    {
        float elapsedTime = Time.time - _startTime;
        return elapsedTime >= maxExperimentTime;
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > ANGLE_NORMALIZATION_THRESHOLD)
        {
            angle -= ANGLE_NORMALIZATION_VALUE;
        }
        return angle;
    }

    private void EndNavigation(bool success, string reason)
    {
        if (!_isRunning) return;

        _isRunning = false;
        float elapsedTime = Time.time - _startTime;

        StopBoat();

        Debug.Log($"<color=cyan>네비게이션 종료</color>");
        Debug.Log($"성공 여부: {success}");
        Debug.Log($"종료 사유: {reason}");
        Debug.Log($"소요 시간: {elapsedTime:F2}초");
        Debug.Log($"총 이동 거리: {_totalDistance:F2}m");
        Debug.Log($"장애물 충돌 횟수: {_collisionCount}회");

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }
    #endregion
}
