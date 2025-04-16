using System;
using UnityEngine;

public class RailController : MonoBehaviour
{
    public GameObject RailRight;
    public GameObject RailLeftBottom;
    public GameObject RailLeftTop;
    public GameObject RailRightTop;
    public GameObject RailRightBottom;
    public GameObject RailUp;
    public GameObject RailDown;
    public GameObject RailLeft;
    
    public float raycastDistance = 1.0f;
    public LayerMask railLayer;
    
    private bool connectedFront = false;
    private bool connectedBack = false;
    private bool connectedLeft = false;
    private bool connectedRight = false;

    public GameObject prevRail;
    public GameObject nextRail;

    private void Awake()
    {
        RailRight.SetActive(true);
    }

    private void Update()
    {
        ScanningRail();
    }

    private void ResetRails()
    {
        RailRight.SetActive(false);
        RailLeftBottom.SetActive(false);
        RailLeftTop.SetActive(false);
        RailRightTop.SetActive(false);
        RailRightBottom.SetActive(false);
        RailUp.SetActive(false);
        RailDown.SetActive(false);
        RailLeft.SetActive(false);
    }

    private void ScanningRail()
    {
        // 이전 연결 상태 저장
        bool prevConnectedFront = connectedFront;
        bool prevConnectedBack = connectedBack;
        bool prevConnectedLeft = connectedLeft;
        bool prevConnectedRight = connectedRight;
        
        // 연결 상태 초기화
        connectedFront = false;
        connectedBack = false;
        connectedLeft = false;
        connectedRight = false;
        
        GameObject oldPrevRail = prevRail;
        GameObject oldNextRail = nextRail;
        
        RaycastHit hit;
        
        // 앞쪽 레일 감지
        if (Physics.Raycast(transform.position, transform.forward, out hit, raycastDistance, railLayer))
        {
            connectedFront = true;
            if (prevRail == null)
                prevRail = hit.collider.gameObject;
            else if (nextRail == null && prevRail != hit.collider.gameObject)
                nextRail = hit.collider.gameObject;
        }
        
        // 뒤쪽 레일 감지
        if (Physics.Raycast(transform.position, -transform.forward, out hit, raycastDistance, railLayer))
        {
            connectedBack = true;
            if (prevRail == null)
                prevRail = hit.collider.gameObject;
            else if (nextRail == null && prevRail != hit.collider.gameObject)
                nextRail = hit.collider.gameObject;
        }
        
        // 왼쪽 레일 감지
        if (Physics.Raycast(transform.position, -transform.right, out hit, raycastDistance, railLayer))
        {
            connectedLeft = true;
            if (prevRail == null)
                prevRail = hit.collider.gameObject;
            else if (nextRail == null && prevRail != hit.collider.gameObject)
                nextRail = hit.collider.gameObject;
        }
        
        // 오른쪽 레일 감지
        if (Physics.Raycast(transform.position, transform.right, out hit, raycastDistance, railLayer))
        {
            connectedRight = true;
            if (prevRail == null)
                prevRail = hit.collider.gameObject;
            else if (nextRail == null && prevRail != hit.collider.gameObject)
                nextRail = hit.collider.gameObject;
        }
        
        // 연결 상태가 변경되었거나 레일 참조가 변경되었을 때 모양 업데이트
        if (prevConnectedFront != connectedFront || 
            prevConnectedBack != connectedBack || 
            prevConnectedLeft != connectedLeft || 
            prevConnectedRight != connectedRight ||
            prevRail != oldPrevRail || 
            nextRail != oldNextRail)
        {
            UpdateRailAppearance();
        }
    }
    
    private void UpdateRailAppearance()
    {
        ResetRails();
        
        // Debug.Log("레일 상태: 앞=" + connectedFront + ", 뒤=" + connectedBack + ", 왼쪽=" + connectedLeft + ", 오른쪽=" + connectedRight);
        
        // 연결된 레일의 수 계산
        int connectionCount = 0;
        if (connectedFront) connectionCount++;
        if (connectedBack) connectionCount++;
        if (connectedLeft) connectionCount++;
        if (connectedRight) connectionCount++;
        
        // Debug.Log("연결된 레일 수: " + connectionCount);
        
        // 아무 레일도 감지되지 않았을 때 기본값으로 RailRight 활성화
        if (connectionCount == 0)
        {
            RailRight.SetActive(true);
            // Debug.Log("기본 오른쪽 레일 활성화");
            return;
        }
        
        // 한쪽만 연결된 경우 처리
        if (connectionCount == 1)
        {
            if (connectedLeft)
            {
                RailLeft.SetActive(true);
                // Debug.Log("왼쪽만 연결 - 왼쪽 레일 활성화");
            }
            else if (connectedRight)
            {
                RailRight.SetActive(true);
                // Debug.Log("오른쪽만 연결 - 오른쪽 레일 활성화");
            }
            else if (connectedFront)
            {
                RailUp.SetActive(true);
                // Debug.Log("앞쪽만 연결 - 위쪽 레일 활성화");
            }
            else if (connectedBack)
            {
                RailDown.SetActive(true);
                // Debug.Log("뒤쪽만 연결 - 아래쪽 레일 활성화");
            }
            return;
        }
        
        // 정확히 2개의 레일과 연결된 경우 처리
        if (connectionCount == 2)
        {
            // 직선 레일 (가로)
            if (connectedLeft && connectedRight)
            {
                RailLeft.SetActive(true);
                RailRight.SetActive(true);
                // Debug.Log("가로 직선 레일 활성화");
            }
            // 직선 레일 (세로)
            else if (connectedFront && connectedBack)
            {
                RailUp.SetActive(true);
                RailDown.SetActive(true);
                // Debug.Log("세로 직선 레일 활성화");
            }
            // 코너 레일 (왼쪽-위) - 수정된 부분
            else if (connectedLeft && connectedFront)
            {
                RailLeftBottom.SetActive(true);
                // Debug.Log("왼쪽-위 코너 레일 활성화");
            }
            // 코너 레일 (오른쪽-위)
            else if (connectedRight && connectedFront)
            {
                RailRightTop.SetActive(true);
                // Debug.Log("오른쪽-위 코너 레일 활성화");
            }
            // 코너 레일 (왼쪽-아래) - 수정된 부분
            else if (connectedLeft && connectedBack)
            {
                RailLeftTop.SetActive(true);
                // Debug.Log("왼쪽-아래 코너 레일 활성화");
            }
            // 코너 레일 (오른쪽-아래)
            else if (connectedRight && connectedBack)
            {
                RailRightBottom.SetActive(true);
                // Debug.Log("오른쪽-아래 코너 레일 활성화");
            }
        }
    }
}