using UnityEngine;

namespace Crest
{
    /// <summary>
    /// 테스트 관리 기능이 비활성화되었습니다.
    /// EpisodeData 클래스가 삭제되어 이 스크립트는 현재 사용할 수 없습니다.
    /// 필요한 경우 EpisodeData 클래스를 다시 생성하거나
    /// 이 컴포넌트를 GameObject에서 제거하세요.
    /// </summary>
    public class TestManager : MonoBehaviour
    {
        [Header("⚠️ 이 스크립트는 현재 비활성화 상태입니다")]
        [Tooltip("EpisodeData 클래스가 필요합니다")]
        public bool testSystemDisabled = true;
        
        [Header("Test Configuration")]
        [SerializeField] private BoatAgent_RL agent;
        [SerializeField] private int totalEpisodes = 100;
        [SerializeField] private float testTimeScale = 5.0f;
        
        private void Start()
        {
            Debug.LogWarning("[TestManager] 테스트 시스템이 비활성화되었습니다. EpisodeData 클래스가 삭제되었습니다.");
            Debug.LogWarning("[TestManager] 테스트를 실행하려면 EpisodeData 클래스를 다시 생성하거나 이 컴포넌트를 제거하세요.");
        }

        /// <summary>
        /// 테스트 시작 (비활성화됨)
        /// </summary>
        public void StartTest()
        {
            Debug.LogError("[TestManager] 테스트 시스템이 비활성화되어 있습니다. EpisodeData 클래스가 필요합니다.");
        }

        // 하위 호환성을 위한 빈 메서드들
        public void OnGoalReached()
        {
            Debug.LogWarning("[TestManager] 테스트 시스템이 비활성화되어 있습니다.");
        }

        public void OnCapsized()
        {
            Debug.LogWarning("[TestManager] 테스트 시스템이 비활성화되어 있습니다.");
        }

        public void OnCollision()
        {
            Debug.LogWarning("[TestManager] 테스트 시스템이 비활성화되어 있습니다.");
        }

        public void UpdateTravelDistance(float distance)
        {
            // 비활성화됨
        }

        public void UpdateDistanceToTarget(float distance)
        {
            // 비활성화됨
        }
    }
}