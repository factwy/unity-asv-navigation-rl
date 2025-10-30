using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 실험 관리 기능이 비활성화되었습니다.
/// ExperimentData 클래스가 삭제되어 이 스크립트는 현재 사용할 수 없습니다.
/// 필요한 경우 ExperimentData 클래스를 다시 생성하거나
/// 이 컴포넌트를 GameObject에서 제거하세요.
/// </summary>
public class ExperimentManager : MonoBehaviour
{
    public static ExperimentManager Instance { get; private set; }

    [Header("⚠️ 이 스크립트는 현재 비활성화 상태입니다")]
    [Tooltip("ExperimentData 클래스가 필요합니다")]
    public bool experimentSystemDisabled = true;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        
        Debug.LogWarning("[ExperimentManager] 실험 시스템이 비활성화되었습니다. ExperimentData 클래스가 삭제되었습니다.");
    }

    void Start()
    {
        Debug.LogWarning("[ExperimentManager] 실험을 실행하려면 ExperimentData 클래스를 다시 생성하거나 이 컴포넌트를 제거하세요.");
    }

    // 하위 호환성을 위한 빈 메서드들
    public void StartExperiments()
    {
        Debug.LogError("[ExperimentManager] 실험 시스템이 비활성화되어 있습니다.");
    }

    public void StartNextExperiment()
    {
        Debug.LogError("[ExperimentManager] 실험 시스템이 비활성화되어 있습니다.");
    }

    // public void OnExperimentComplete(ExperimentData data)
    // {
    //     // ExperimentData 타입을 제거했으므로 이 메서드는 주석 처리
    // }
}