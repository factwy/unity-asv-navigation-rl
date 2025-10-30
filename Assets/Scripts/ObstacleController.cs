using UnityEngine;
using Pathfinding;

// 이 컴포넌트는 GraphUpdateScene이 반드시 필요합니다.
[RequireComponent(typeof(GraphUpdateScene))]
public class ObstacleController : MonoBehaviour
{
    private GraphUpdateScene _gus;
    private Bounds _lastBounds; // 이전에 업데이트했던 영역을 기억하기 위한 변수
    private bool _isFirstUpdate = true;

    void Start()
    {
        // 시작할 때 GraphUpdateScene 컴포넌트를 찾아 저장합니다.
        _gus = GetComponent<GraphUpdateScene>();
        if (_gus == null)
        {
            Debug.LogError("ObstacleController: GraphUpdateScene 컴포넌트가 없습니다!");
            this.enabled = false; // 컴포넌트가 없으면 스크립트를 비활성화합니다.
        }
    }

    // 물리 및 그래프 업데이트는 FixedUpdate에서 처리하는 것이 더 안정적입니다.
    void FixedUpdate()
    {
        if (_gus == null) return;

        // --- 그래프 업데이트 로직 (마커와 지우개) ---
        
        // 1. 현재 장애물의 영역(Bounds)을 가져옵니다.
        // 이 오브젝트가 다른 스크립트에 의해 움직이면 이 영역도 따라 움직입니다.
        var currentBounds = _gus.GetBounds();

        // 2. '지우개': 이전에 막았던 길을 다시 열어줍니다.
        // 첫 프레임에는 이전에 막았던 길이 없으므로 실행하지 않습니다.
        if (!_isFirstUpdate)
        {
            var guo_unblock = new GraphUpdateObject(_lastBounds);
            guo_unblock.setWalkability = true; // 지나갈 수 있도록 설정
            AstarPath.active.UpdateGraphs(guo_unblock);
        }

        // 3. '마커': 현재 위치의 길을 막습니다.
        var guo_block = new GraphUpdateObject(currentBounds);
        guo_block.setWalkability = false; // 지나갈 수 없도록 설정
        AstarPath.active.UpdateGraphs(guo_block);

        // 4. 현재 위치를 '이전 위치'로 기억하여 다음 프레임에서 지울 수 있도록 합니다.
        _lastBounds = currentBounds;
        _isFirstUpdate = false;
    }
}