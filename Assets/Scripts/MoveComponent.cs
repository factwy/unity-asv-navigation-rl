using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveComponent : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("보트의 직진 속도입니다.")]
    public float moveSpeed = 12.0f;

    [Tooltip("회전할 때의 속도입니다. 직진 속도보다 낮게 설정하세요.")]
    public float turnMoveSpeed = 3.0f;

    [Tooltip("회전 속도입니다 (도/초). 낮을수록 천천히 회전합니다.")]
    public float turnSpeed = 30.0f;

    [Tooltip("보트가 한 방향으로 전진할 거리입니다.")]
    public float patrolDistance = 50.0f;

    [Tooltip("보트의 이동 방향 (1.0 = 앞으로, -1.0 = 뒤로)")]
    public float movementDirection = 1.0f;

    // 비공개 변수 (내부 로직용)
    private bool isTurning = false;
    private float distanceTraveled = 0f;
    private float turnAngleRemaining = 0f;

    void Update()
    {
        // 현재 상태에 따른 속도 결정
        float currentSpeed = isTurning ? turnMoveSpeed : moveSpeed;

        // 1. 상태별 회전 처리 (먼저 처리)
        if (isTurning)
        {
            // 이번 프레임에 회전할 각도 계산
            float angleThisFrame = turnSpeed * Time.deltaTime;
            
            // 남은 회전 각도보다 크면 남은 각도만큼만 회전
            if (angleThisFrame >= turnAngleRemaining)
            {
                angleThisFrame = turnAngleRemaining;
                isTurning = false;
                distanceTraveled = 0f;
            }
            
            // Y축 기준으로 회전
            transform.Rotate(0, angleThisFrame, 0);
            turnAngleRemaining -= angleThisFrame;
        }
        else // 직진 상태
        {
            distanceTraveled += currentSpeed * Time.deltaTime;
            
            if (distanceTraveled >= patrolDistance)
            {
                isTurning = true;
                turnAngleRemaining = 180f; // 180도 U턴
            }
        }
        
        // 2. 전진: 회전 후 현재 방향으로 이동 (나중에 처리)
        transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime * movementDirection, Space.Self);
    }
}