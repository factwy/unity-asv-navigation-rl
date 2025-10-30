using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Crest
{
    /// <summary>
    /// ML-Agents 기반 USV 강화학습 Agent (360도 센서 + 커리큘럼 학습)
    /// State: 25개 관측값 (9개 센서 + 이전 9개 센서)
    /// </summary>
    public class BoatAgent_RL : Agent
    {
        [Header("Agent References")]
        [SerializeField] private BoatProbes_RL boatController;
        [SerializeField] private Transform goalTransform;
        
        [Header("Curriculum - Obstacle Lists")]
        [SerializeField] private List<GameObject> staticObstacles = new List<GameObject>();
        [SerializeField] private List<GameObject> movingObstacles = new List<GameObject>();
        
        [Header("Sensor Configuration - 360° Coverage")]
        [SerializeField] private float sensorMaxDistance = 200f;
        [SerializeField] private LayerMask obstacleLayer;
        // 360도를 40도씩 9개로 분할: 0°, 40°, 80°, 120°, 160°, 200°, 240°, 280°, 320°
        private readonly float[] sensorAngles = { 0f, 40f, 80f, 120f, 160f, 200f, 240f, 280f, 320f };
        private float[] currentSensorDistances = new float[9];
        private float[] previousSensorDistances = new float[9];
        
        [Header("Normalization Parameters")]
        [SerializeField] private float maxLinearVelocity = 10f;
        [SerializeField] private float maxAngularVelocity = 2f;
        [SerializeField] private float maxRollAngle = 45f;
        [SerializeField] private float maxPitchAngle = 30f;
        
        [Header("Reward Weights")]
        [SerializeField] private float goalReward = 1000f;
        [SerializeField] private float collisionPenalty = -1000f;
        [SerializeField] private float capsizedPenalty = -500f;
        [SerializeField] private float w1_distance = 1.0f;
        [SerializeField] private float w2_safety = 0.5f;
        [SerializeField] private float w3_velocity = 0.3f;
        [SerializeField] private float w4_stability = 0.2f;
        [SerializeField] private float w5_risk = 0.8f;
        [SerializeField] private float w6_roll = 0.4f;
        
        [Header("Episode Settings")]
        [SerializeField] private float goalReachThreshold = 5f;
        [SerializeField] private float maxEpisodeSteps = 5000f;
        [SerializeField] private float capsizeRollThreshold = 60f;

        [Header("Test Settings")]
        public bool isTestMode = false;
        public int testStaticObstacles = 5;
        public int testMovingObstacles = 3;
        public float testTimeScale = 1.0f;
        
        // State variables
        private Vector3 startPosition;
        private Quaternion startRotation;
        private float previousGoalDistance;
        private float currentGoalDistance;
        private float minObstacleDistance;
        private float previousMinObstacleDistance;
        private Rigidbody rb;
        
        // 자세 각도 변수
        private float currentRollAngle;
        private float currentPitchAngle;
        private float previousRollAngle;
        private float previousPitchAngle;
        
        // Episode tracking
        private int stepCount;
        
        // Curriculum variables
        private int currentNumStaticObstacles = -1;
        private int currentNumMovingObstacles = -1;
        private List<GameObject> activeStaticObstacles = new List<GameObject>();
        private List<GameObject> activeMovingObstacles = new List<GameObject>();

        // Test mode variables
        private TestManager testManager;
        private Vector3 previousPosition;
        private float totalTravelDistance;

        private void Awake()
        {
            if (boatController == null)
                boatController = GetComponent<BoatProbes_RL>();
            
            rb = GetComponent<Rigidbody>();
            
            if (rb == null)
            {
                return;
            }
            
            startPosition = transform.position;
            startRotation = transform.rotation;
            
            // 배열 초기화 (9개 센서)
            currentSensorDistances = new float[9];
            previousSensorDistances = new float[9];
            
            // Decision Requester 확인
            if (GetComponent<Unity.MLAgents.DecisionRequester>() == null)
            {
            }
            
            // 장애물 리스트 검증
            ValidateObstacleLists();
            
            // 시작 시 모든 장애물 비활성화
            foreach (var obstacle in staticObstacles)
            {
                if (obstacle != null)
                    obstacle.SetActive(false);
            }
            foreach (var obstacle in movingObstacles)
            {
                if (obstacle != null)
                    obstacle.SetActive(false);
            }
            
            // 추론 과정에서의 시간 배율 조정
            if (isTestMode)
                Time.timeScale = testTimeScale;
            
            // TestManager 찾기
            testManager = FindObjectOfType<TestManager>();
        }

        public override void Initialize()
        {
            base.Initialize();
            MaxStep = (int)maxEpisodeSteps;
        }

        public override void OnEpisodeBegin()
        {
            // Null 체크
            if (goalTransform == null || rb == null)
            {
                return;
            }
            
            // 에피소드 시작 시 초기화
            stepCount = 0;
            
            // 위치 및 회전 초기화
            transform.position = startPosition;
            transform.rotation = startRotation;
            
            // Rigidbody 초기화
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            // 커리큘럼 파라미터 읽기 및 장애물 설정
            UpdateCurriculumObstacles();
            
            // 센서 초기화
            UpdateSensors();
            previousSensorDistances = (float[])currentSensorDistances.Clone();
            
            // 거리 초기화
            previousGoalDistance = Vector3.Distance(transform.position, goalTransform.position);
            currentGoalDistance = previousGoalDistance;
            previousMinObstacleDistance = minObstacleDistance;
            
            // 자세 각도 초기화
            UpdateAttitude();
            previousRollAngle = currentRollAngle;
            previousPitchAngle = currentPitchAngle;
            
            // Test mode 초기화
            if (isTestMode && testManager != null)
            {
                previousPosition = transform.position;
                totalTravelDistance = 0f;
            }
        }

        /// <summary>
        /// 커리큘럼 파라미터를 읽어서 장애물 활성화/비활성화
        /// </summary>
        private void UpdateCurriculumObstacles()
        {
            if (isTestMode) {
                currentNumStaticObstacles = testStaticObstacles;
                currentNumMovingObstacles = testMovingObstacles;
                ActivateObstacles();
                return;
            } else {
                // Environment Parameters에서 값 읽기
                int targetStaticCount = Mathf.RoundToInt(
                    Academy.Instance.EnvironmentParameters.GetWithDefault("num_static_obstacles", 0f)
                );
                int targetMovingCount = Mathf.RoundToInt(
                    Academy.Instance.EnvironmentParameters.GetWithDefault("num_moving_obstacles", 0f)
                );
                
                // 이전 설정과 다르면 장애물 재배치
                if (targetStaticCount != currentNumStaticObstacles || 
                    targetMovingCount != currentNumMovingObstacles)
                {
                    currentNumStaticObstacles = targetStaticCount;
                    currentNumMovingObstacles = targetMovingCount;
                    
                    // 장애물 활성화/비활성화
                    ActivateObstacles();
                }
            }
        }

        /// <summary>
        /// 장애물을 랜덤하게 선택하여 활성화
        /// </summary>
        private void ActivateObstacles()
        {
            // 1. 모든 장애물 비활성화
            foreach (var obstacle in staticObstacles)
            {
                if (obstacle != null)
                    obstacle.SetActive(false);
            }
            foreach (var obstacle in movingObstacles)
            {
                if (obstacle != null)
                    obstacle.SetActive(false);
            }
            
            // 2. 정적 장애물 랜덤 선택 및 활성화
            activeStaticObstacles.Clear();
            if (currentNumStaticObstacles > 0 && staticObstacles.Count > 0)
            {
                int count = Mathf.Min(currentNumStaticObstacles, staticObstacles.Count);
                var shuffled = staticObstacles.OrderBy(x => Random.value).ToList();
                
                for (int i = 0; i < count; i++)
                {
                    if (shuffled[i] != null)
                    {
                        shuffled[i].SetActive(true);
                        activeStaticObstacles.Add(shuffled[i]);
                    }
                }
            }
            
            // 3. 동적 장애물 랜덤 선택 및 활성화
            activeMovingObstacles.Clear();
            if (currentNumMovingObstacles > 0 && movingObstacles.Count > 0)
            {
                int count = Mathf.Min(currentNumMovingObstacles, movingObstacles.Count);
                var shuffled = movingObstacles.OrderBy(x => Random.value).ToList();
                
                for (int i = 0; i < count; i++)
                {
                    if (shuffled[i] != null)
                    {
                        shuffled[i].SetActive(true);
                        activeMovingObstacles.Add(shuffled[i]);
                    }
                }
            }
        }

        /// <summary>
        /// 장애물 리스트 검증
        /// </summary>
        private void ValidateObstacleLists()
        {
            if (staticObstacles.Count != 8)
            {
            }
            if (movingObstacles.Count != 4)
            {
            }
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            // Null 체크
            if (goalTransform == null || rb == null)
            {
                // 기본값으로 채우기 (25개)
                for (int i = 0; i < 25; i++)
                {
                    sensor.AddObservation(0f);
                }
                return;
            }
            
            UpdateSensors();
            UpdateAttitude();
            
            // ===== 현재 타임스텝 관측 (S_t) =====
            
            // 1. 센서 거리 (9개) - 정규화
            for (int i = 0; i < currentSensorDistances.Length; i++)
            {
                sensor.AddObservation(currentSensorDistances[i] / sensorMaxDistance);
            }
            
            // 2. 목표점과의 거리 - 정규화
            currentGoalDistance = Vector3.Distance(transform.position, goalTransform.position);
            sensor.AddObservation(currentGoalDistance / sensorMaxDistance);
            
            // 3. 목표점과의 각도 - 정규화 (-1 ~ 1)
            float goalAngle = GetGoalAngle();
            sensor.AddObservation(goalAngle / Mathf.PI);
            
            // 4. 현재 선형 속도 - 정규화
            float linearVelocity = Vector3.Dot(rb.velocity, transform.forward);
            sensor.AddObservation(linearVelocity / maxLinearVelocity);
            
            // 5. 현재 각속도 - 정규화
            float angularVelocity = rb.angularVelocity.y;
            sensor.AddObservation(angularVelocity / maxAngularVelocity);
            
            // 6. 현재 롤 각도 - 정규화
            sensor.AddObservation(currentRollAngle / maxRollAngle);
            
            // 7. 현재 피치 각도 - 정규화
            sensor.AddObservation(currentPitchAngle / maxPitchAngle);
            
            // ===== 이전 타임스텝 관측 (S_{t-1}) =====
            
            // 8. 이전 센서 거리 (9개) - 정규화
            for (int i = 0; i < previousSensorDistances.Length; i++)
            {
                sensor.AddObservation(previousSensorDistances[i] / sensorMaxDistance);
            }
            
            // 9. 이전 목표점 거리 - 정규화
            sensor.AddObservation(previousGoalDistance / sensorMaxDistance);
            
            // Total observations: 9 + 1 + 1 + 1 + 1 + 1 + 1 + 9 + 1 = 25
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            // 시간 패널티 추가
            AddReward(-2f / maxEpisodeSteps);
            
            stepCount++;
            
            if (boatController == null)
            {
                return;
            }
            
            // Action: [v_t, omega_t]
            float forwardAction = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
            float turnAction = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
            
            // BoatProbes_RL에 액션 적용
            boatController._playerControlled = false;
            boatController.AI_ForwardInput = forwardAction;
            boatController.AI_TurnInput = turnAction;
            
            // 보상 계산
            float reward = CalculateReward(forwardAction, turnAction);
            AddReward(reward);
            
            // Test mode: 이동 거리 계산
            if (isTestMode && testManager != null)
            {
                float distanceMoved = Vector3.Distance(transform.position, previousPosition);
                totalTravelDistance += distanceMoved;
                previousPosition = transform.position;
                
                testManager.UpdateTravelDistance(totalTravelDistance);
                testManager.UpdateDistanceToTarget(currentGoalDistance);
            }
            
            // 에피소드 종료 조건 체크
            CheckEpisodeEnd();
            
            // 다음 스텝을 위한 이전 값 저장
            previousSensorDistances = (float[])currentSensorDistances.Clone();
            previousGoalDistance = currentGoalDistance;
            previousMinObstacleDistance = minObstacleDistance;
            previousRollAngle = currentRollAngle;
            previousPitchAngle = currentPitchAngle;
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuousActions = actionsOut.ContinuousActions;
            continuousActions[0] = Input.GetAxis("Vertical");
            continuousActions[1] = Input.GetAxis("Horizontal");
        }

        /// <summary>
        /// 360도 센서를 사용하여 장애물 거리 측정 (9개 광선, 40도 간격)
        /// </summary>
        private void UpdateSensors()
        {
            minObstacleDistance = sensorMaxDistance;
            
            for (int i = 0; i < sensorAngles.Length; i++)
            {
                float angle = sensorAngles[i];
                Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
                
                RaycastHit hit;
                if (Physics.Raycast(transform.position, direction, out hit, sensorMaxDistance, obstacleLayer))
                {
                    currentSensorDistances[i] = hit.distance;
                    minObstacleDistance = Mathf.Min(minObstacleDistance, hit.distance);
                }
                else
                {
                    currentSensorDistances[i] = sensorMaxDistance;
                }
            }
        }

        private void UpdateAttitude()
        {
            Vector3 eulerAngles = transform.eulerAngles;
            
            currentRollAngle = eulerAngles.z;
            if (currentRollAngle > 180f)
                currentRollAngle -= 360f;
            
            currentPitchAngle = eulerAngles.x;
            if (currentPitchAngle > 180f)
                currentPitchAngle -= 360f;
        }

        private float GetGoalAngle()
        {
            if (goalTransform == null)
            {
                return 0f;
            }
            
            Vector3 toGoal = (goalTransform.position - transform.position).normalized;
            toGoal.y = 0;
            
            Vector3 forward = transform.forward;
            forward.y = 0;
            forward.Normalize();
            
            float angle = Vector3.SignedAngle(forward, toGoal, Vector3.up) * Mathf.Deg2Rad;
            return angle;
        }

        private float CalculateReward(float linearVel, float angularVel)
        {
            float totalReward = 0f;
            float shapingReward = CalculateShapingReward(linearVel, angularVel);
            totalReward += shapingReward;
            return totalReward;
        }

        private float CalculateShapingReward(float linearVel, float angularVel)
        {
            if (rb == null)
            {
                return 0f;
            }
            
            // 1. r_dist
            float r_dist = 1/currentGoalDistance - (1/previousGoalDistance);
            
            // 2. r_safe
            float r_safe = minObstacleDistance > 0 ? -1f / minObstacleDistance : -10f;
            
            // 3. r_vel
            float goalAngle = GetGoalAngle();
            float actualLinearVel = Vector3.Dot(rb.velocity, transform.forward);
            float r_vel = actualLinearVel * Mathf.Cos(goalAngle);
            
            // 4. r_stable
            float actualAngularVel = rb.angularVelocity.y;
            float r_stable = -Mathf.Abs(actualAngularVel);
            
            // 5. r_risk
            float r_risk = 0f;
            if (minObstacleDistance > 0 && previousMinObstacleDistance > 0)
            {
                float currentRisk = 1f / minObstacleDistance;
                float previousRisk = 1f / previousMinObstacleDistance;
                r_risk = Mathf.Max(0, currentRisk - previousRisk);
            }
            
            // 6. r_roll
            float r_roll = -Mathf.Abs(currentRollAngle) / maxRollAngle;
            
            float shapingReward = 
                w1_distance * r_dist +
                w2_safety * r_safe +
                w3_velocity * r_vel +
                w4_stability * r_stable +
                w5_risk * (r_risk) +
                w6_roll * r_roll;
            
            return shapingReward;
        }

        private void CheckEpisodeEnd()
        {
            if (goalTransform == null)
            {
                return;
            }
            
            // 1. 목표 도달
            if (currentGoalDistance < goalReachThreshold)
            {
                AddReward(goalReward);
                
                // Test mode: TestManager에 알림
                if (isTestMode && testManager != null)
                {
                    testManager.OnGoalReached();
                }
                
                EndEpisode();
                return;
            }
            
            // 2. 전복 체크
            if (Mathf.Abs(currentRollAngle) > capsizeRollThreshold)
            {
                AddReward(capsizedPenalty);
                
                // Test mode: TestManager에 알림
                if (isTestMode && testManager != null)
                {
                    testManager.OnCapsized();
                }
                
                EndEpisode();
                return;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            string collidedObjectName = collision.gameObject.name;

            if (((1 << collision.gameObject.layer) & obstacleLayer) != 0)
            {
                AddReward(collisionPenalty);
                
                // Test mode: TestManager에 충돌 알림
                if (isTestMode && testManager != null)
                {
                    testManager.OnCollision();
                }
                
                // EndEpisode();
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            
            // 360도 센서 시각화
            for (int i = 0; i < sensorAngles.Length; i++)
            {
                float angle = sensorAngles[i];
                Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
                
                Color rayColor = currentSensorDistances[i] < sensorMaxDistance ? Color.red : Color.green;
                Gizmos.color = rayColor;
                Gizmos.DrawRay(transform.position, direction * currentSensorDistances[i]);
            }
            
            // 목표 지점까지의 선
            if (goalTransform != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, goalTransform.position);
                
                Gizmos.color = new Color(0, 1, 1, 0.3f);
                Gizmos.DrawWireSphere(goalTransform.position, goalReachThreshold);
            }
            
            // 롤 각도 위험 시각화
            if (Mathf.Abs(currentRollAngle) > capsizeRollThreshold * 0.7f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 1f);
            }
        }
    }
}