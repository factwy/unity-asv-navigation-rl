using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class BoatAgent : Agent
{
    #region Constants
    private const int MAX_BOUNDARY_PARTS = 4;
    private const int MAX_OBSTACLES = 13;
    private const int GRID_CELL_SIZE = 5;

    // Spawn and target positions
    private const float SPAWN_POSITION_X = -68.0f;
    private const float SPAWN_POSITION_Y = 1.0f;
    private const float SPAWN_POSITION_Z = -68.0f;
    private const float TARGET_BASE_POSITION = 80.0f;
    private const float TARGET_RANDOM_RANGE = 10.0f;

    // Angular velocity thresholds
    private const float ANGULAR_VELOCITY_SPINNING_THRESHOLD = 0.5f;

    // Path efficiency multipliers
    private const float PATH_EFFICIENCY_HALF_REWARD = 0.5f;
    private const float PATH_EFFICIENCY_PENALTY_MULTIPLIER = 2f;
    #endregion

    #region Serialized Fields
    [Header("Boat Physics")]
    public float forceHeightOffset = -0.3f;
    public float enginePower = 10f;
    public float turnPower = 3f;

    [Header("References")]
    public Transform Target;
    public Transform[] boundary = new Transform[4];

    [Header("Reward Settings")]
    [Tooltip("에피소드 최대 시간 (초)")]
    public float maxEpisodeTime = 600f;

    [Tooltip("시간 보상 가중치")]
    public float timeRewardWeight = 50.0f;

    [Tooltip("경로 효율성 보상 가중치")]
    public float pathEfficiencyRewardWeight = 50.0f;

    [Tooltip("목표 도달 시 최종 보상")]
    public float targetReachedReward = 100.0f;

    [Tooltip("생존 시간에 대한 지속적인 보상")]
    public float survivalReward = 0.1f;

    [Header("Penalty Settings")]
    [Tooltip("경계 충돌 시 패널티")]
    public float boundaryPenalty = -100f;

    [Tooltip("장애물 충돌 시 패널티")]
    public float obstaclePenalty = -0.5f;

    [Tooltip("방문했던 지역 재방문 시 패널티")]
    public float visitedPenalty = -0.001f;

    [Tooltip("제자리 회전 시 패널티")]
    public float spinningPenalty = -0.01f;

    [Tooltip("저속 주행 지속 시 패널티")]
    public float lowSpeedPenalty = -0.005f;

    [Header("Speed Control")]
    [Tooltip("이 속도 이하로 지속되면 패널티를 받습니다.")]
    public float lowSpeedThreshold = 0.5f;

    [Tooltip("저속 상태를 몇 초나 지속하면 패널티를 받을지 결정합니다.")]
    public float lowSpeedDurationThreshold = 5f;

    [Tooltip("보트 전복 시 패널티")]
    public float flippedPenalty = -50f;

    [Header("Environment References")]
    public Transform boundaryParent;
    public Transform obstacleParent;
    #endregion

    #region Private Fields
    private Rigidbody _rigidbody;
    private List<Transform> _boundaryParts = new List<Transform>();
    private List<Transform> _obstacleParts = new List<Transform>();
    private float _episodeStartTime;
    private float _totalDistanceTraveled;
    private Vector3 _lastPosition;
    private HashSet<Vector2Int> _visitedGridCells;
    private float _lowSpeedTimer;
    private float _initialEuclideanDistance;
    private float _initialManhattanDistance;
    #endregion

    #region Unity Lifecycle
    protected override void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();

        if (_rigidbody == null)
        {
            Debug.LogError($"[{GetType().Name}] Rigidbody component is missing!");
        }

        // Boundary 자식 객체들의 Transform 정보 수집
        if (boundaryParent != null)
        {
            foreach (Transform part in boundaryParent)
            {
                if (_boundaryParts.Count >= MAX_BOUNDARY_PARTS) break;
                _boundaryParts.Add(part);
            }
        }

        // Obstacle 자식 객체들의 Transform 정보 수집
        if (obstacleParent != null)
        {
            foreach (Transform part in obstacleParent)
            {
                if (_obstacleParts.Count >= MAX_OBSTACLES) break;
                _obstacleParts.Add(part);
            }
        }
    }
    #endregion

    #region ML-Agents Methods
    public override void OnEpisodeBegin()
    {
        if (_rigidbody == null) return;

        // Reset physics
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.velocity = Vector3.zero;

        // Reset position and rotation
        transform.localPosition = new Vector3(SPAWN_POSITION_X, SPAWN_POSITION_Y, SPAWN_POSITION_Z);
        transform.localRotation = Quaternion.identity;

        // Randomize target position
        if (Target != null)
        {
            Target.localPosition = new Vector3(
                TARGET_BASE_POSITION + Random.value * TARGET_RANDOM_RANGE,
                SPAWN_POSITION_Y,
                TARGET_BASE_POSITION + Random.value * TARGET_RANDOM_RANGE
            );
        }

        // Reset tracking variables
        _episodeStartTime = Time.time;
        _totalDistanceTraveled = 0f;
        _lastPosition = transform.localPosition;
        _lowSpeedTimer = 0f;

        // Reset visit tracking
        _visitedGridCells = new HashSet<Vector2Int>();
        RecordVisitedPosition();

        // Calculate initial distances
        if (Target != null)
        {
            _initialEuclideanDistance = Vector3.Distance(transform.localPosition, Target.localPosition);
            _initialManhattanDistance = Mathf.Abs(transform.localPosition.x - Target.localPosition.x) +
                                       Mathf.Abs(transform.localPosition.z - Target.localPosition.z);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (_rigidbody == null || Target == null) return;

        // Basic observations (12 total)
        sensor.AddObservation(_rigidbody.velocity.magnitude);
        sensor.AddObservation(transform.rotation);
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(Target.localPosition);

        float distanceToTarget = Vector3.Distance(transform.localPosition, Target.localPosition);
        sensor.AddObservation(distanceToTarget);

        // Boundary observations
        for (int i = 0; i < MAX_BOUNDARY_PARTS; i++)
        {
            if (i < _boundaryParts.Count)
            {
                sensor.AddObservation(transform.InverseTransformPoint(_boundaryParts[i].position));
                sensor.AddObservation(_boundaryParts[i].localScale);
            }
            else
            {
                sensor.AddObservation(Vector3.zero);
                sensor.AddObservation(Vector3.zero);
            }
        }

        // Obstacle observations
        for (int i = 0; i < MAX_OBSTACLES; i++)
        {
            if (i < _obstacleParts.Count)
            {
                sensor.AddObservation(transform.InverseTransformPoint(_obstacleParts[i].position));
                sensor.AddObservation(_obstacleParts[i].localScale);
            }
            else
            {
                sensor.AddObservation(Vector3.zero);
                sensor.AddObservation(Vector3.zero);
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (_rigidbody == null) return;

        ExecuteActions(actionBuffers);
        ApplyContinuousRewards();
        CheckFailureConditions();
    }
    #endregion

    #region Private Methods
    private void ExecuteActions(ActionBuffers actionBuffers)
    {
        float throttle = actionBuffers.ContinuousActions[1];
        float steer = actionBuffers.ContinuousActions[0];

        var forcePosition = _rigidbody.worldCenterOfMass + transform.up * forceHeightOffset;
        _rigidbody.AddForceAtPosition(transform.forward * enginePower * throttle, forcePosition, ForceMode.Acceleration);
        _rigidbody.AddTorque(transform.up * turnPower * steer, ForceMode.Acceleration);
    }

    private void ApplyContinuousRewards()
    {
        // Survival reward
        AddReward(survivalReward * Time.fixedDeltaTime);

        // Track distance
        _totalDistanceTraveled += Vector3.Distance(transform.localPosition, _lastPosition);
        _lastPosition = transform.localPosition;

        // Visit penalty
        ApplyVisitedPenalty();

        // Spinning penalty
        if (Mathf.Abs(_rigidbody.angularVelocity.y) > ANGULAR_VELOCITY_SPINNING_THRESHOLD &&
            _rigidbody.velocity.magnitude < lowSpeedThreshold)
        {
            AddReward(spinningPenalty * Time.fixedDeltaTime);
        }

        // Low speed penalty
        if (_rigidbody.velocity.magnitude < lowSpeedThreshold)
        {
            _lowSpeedTimer += Time.fixedDeltaTime;
            if (_lowSpeedTimer > lowSpeedDurationThreshold)
            {
                AddReward(lowSpeedPenalty * Time.fixedDeltaTime);
            }
        }
        else
        {
            _lowSpeedTimer = 0f;
        }
    }

    private void CheckFailureConditions()
    {
        // Check if boat is flipped
        if (Vector3.Dot(transform.up, Vector3.up) < 0f)
        {
            AddReward(flippedPenalty);
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Obstacle 태그와 충돌하면 중량의 패널티 부여
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            AddReward(obstaclePenalty);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Target != null && other.transform == Target)
        {
            HandleTargetReached();
        }

        // Check boundary collision
        foreach (Transform b in boundary)
        {
            if (other.transform == b)
            {
                AddReward(boundaryPenalty);
                EndEpisode();
                break;
            }
        }
    }

    private void HandleTargetReached()
    {
        // Time reward
        float elapsedTime = Time.time - _episodeStartTime;
        float timeReward = (maxEpisodeTime - elapsedTime) / maxEpisodeTime * timeRewardWeight;
        AddReward(timeReward);

        // Path efficiency reward
        if (_totalDistanceTraveled >= _initialEuclideanDistance &&
            _totalDistanceTraveled < _initialManhattanDistance)
        {
            AddReward(pathEfficiencyRewardWeight);
        }
        else if (_totalDistanceTraveled >= _initialManhattanDistance &&
                 _totalDistanceTraveled < _initialManhattanDistance * PATH_EFFICIENCY_PENALTY_MULTIPLIER)
        {
            AddReward(pathEfficiencyRewardWeight * PATH_EFFICIENCY_HALF_REWARD);
        }
        else
        {
            AddReward(-pathEfficiencyRewardWeight);
        }

        // Final success reward
        AddReward(targetReachedReward);
        EndEpisode();
    }

    private void RecordVisitedPosition()
    {
        Vector2Int gridCell = WorldToGridCell(transform.localPosition);
        _visitedGridCells.Add(gridCell);
    }

    private void ApplyVisitedPenalty()
    {
        Vector2Int currentGridCell = WorldToGridCell(transform.localPosition);
        if (_visitedGridCells.Contains(currentGridCell))
        {
            AddReward(visitedPenalty);
        }
        else
        {
            _visitedGridCells.Add(currentGridCell);
        }
    }

    private Vector2Int WorldToGridCell(Vector3 position)
    {
        int x = Mathf.FloorToInt(position.x / GRID_CELL_SIZE);
        int z = Mathf.FloorToInt(position.z / GRID_CELL_SIZE);
        return new Vector2Int(x, z);
    }
    #endregion

    #region Heuristic
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = Input.GetAxis("Horizontal");
        continuousActionsOut[1] = Input.GetAxis("Vertical");
    }
    #endregion
}