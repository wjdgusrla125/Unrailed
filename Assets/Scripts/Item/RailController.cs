using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class RailController : NetworkBehaviour
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
        // Save previous connection states
        bool prevConnectedFront = connectedFront;
        bool prevConnectedBack = connectedBack;
        bool prevConnectedLeft = connectedLeft;
        bool prevConnectedRight = connectedRight;
        
        // Reset connection states
        connectedFront = false;
        connectedBack = false;
        connectedLeft = false;
        connectedRight = false;
        
        GameObject oldPrevRail = prevRail;
        GameObject oldNextRail = nextRail;
        
        RaycastHit hit;
        
        // Front rail detection
        if (Physics.Raycast(transform.position, transform.forward, out hit, raycastDistance, railLayer))
        {
            GameObject detectedRail = hit.collider.gameObject;
            
            // Get RailController of detected rail
            RailController railController = detectedRail.GetComponent<RailController>();
            
            // Only consider the rail if it has connection slots available
            if (railController != null && (railController.prevRail == null || railController.nextRail == null || 
                                          railController.prevRail == gameObject || railController.nextRail == gameObject))
            {
                connectedFront = true;
                
                // Only update our connections if the detected rail has a slot for us
                if (railController.prevRail == null || railController.nextRail == null)
                {
                    if (prevRail == null)
                        prevRail = detectedRail;
                    else if (nextRail == null && prevRail != detectedRail)
                        nextRail = detectedRail;
                    else if (prevRail != null && nextRail != null && prevRail != detectedRail && nextRail != detectedRail)
                    {
                        // Both connections are already full and neither is to this rail
                        connectedFront = false;
                    }
                }
                else if (railController.prevRail != gameObject && railController.nextRail != gameObject)
                {
                    // The detected rail has both connections full and neither is to us
                    connectedFront = false;
                }
            }
        }
        
        // Back rail detection
        if (Physics.Raycast(transform.position, -transform.forward, out hit, raycastDistance, railLayer))
        {
            GameObject detectedRail = hit.collider.gameObject;
            
            // Get RailController of detected rail
            RailController railController = detectedRail.GetComponent<RailController>();
            
            // Only consider the rail if it has connection slots available
            if (railController != null && (railController.prevRail == null || railController.nextRail == null || 
                                          railController.prevRail == gameObject || railController.nextRail == gameObject))
            {
                connectedBack = true;
                
                // Only update our connections if the detected rail has a slot for us
                if (railController.prevRail == null || railController.nextRail == null)
                {
                    if (prevRail == null)
                        prevRail = detectedRail;
                    else if (nextRail == null && prevRail != detectedRail)
                        nextRail = detectedRail;
                    else if (prevRail != null && nextRail != null && prevRail != detectedRail && nextRail != detectedRail)
                    {
                        // Both connections are already full and neither is to this rail
                        connectedBack = false;
                    }
                }
                else if (railController.prevRail != gameObject && railController.nextRail != gameObject)
                {
                    // The detected rail has both connections full and neither is to us
                    connectedBack = false;
                }
            }
        }
        
        // Left rail detection
        if (Physics.Raycast(transform.position, -transform.right, out hit, raycastDistance, railLayer))
        {
            GameObject detectedRail = hit.collider.gameObject;
            
            // Get RailController of detected rail
            RailController railController = detectedRail.GetComponent<RailController>();
            
            // Only consider the rail if it has connection slots available
            if (railController != null && (railController.prevRail == null || railController.nextRail == null || 
                                          railController.prevRail == gameObject || railController.nextRail == gameObject))
            {
                connectedLeft = true;
                
                // Only update our connections if the detected rail has a slot for us
                if (railController.prevRail == null || railController.nextRail == null)
                {
                    if (prevRail == null)
                        prevRail = detectedRail;
                    else if (nextRail == null && prevRail != detectedRail)
                        nextRail = detectedRail;
                    else if (prevRail != null && nextRail != null && prevRail != detectedRail && nextRail != detectedRail)
                    {
                        // Both connections are already full and neither is to this rail
                        connectedLeft = false;
                    }
                }
                else if (railController.prevRail != gameObject && railController.nextRail != gameObject)
                {
                    // The detected rail has both connections full and neither is to us
                    connectedLeft = false;
                }
            }
        }
        
        // Right rail detection
        if (Physics.Raycast(transform.position, transform.right, out hit, raycastDistance, railLayer))
        {
            GameObject detectedRail = hit.collider.gameObject;
            
            // Get RailController of detected rail
            RailController railController = detectedRail.GetComponent<RailController>();
            
            // Only consider the rail if it has connection slots available
            if (railController != null && (railController.prevRail == null || railController.nextRail == null || 
                                          railController.prevRail == gameObject || railController.nextRail == gameObject))
            {
                connectedRight = true;
                
                // Only update our connections if the detected rail has a slot for us
                if (railController.prevRail == null || railController.nextRail == null)
                {
                    if (prevRail == null)
                        prevRail = detectedRail;
                    else if (nextRail == null && prevRail != detectedRail)
                        nextRail = detectedRail;
                    else if (prevRail != null && nextRail != null && prevRail != detectedRail && nextRail != detectedRail)
                    {
                        // Both connections are already full and neither is to this rail
                        connectedRight = false;
                    }
                }
                else if (railController.prevRail != gameObject && railController.nextRail != gameObject)
                {
                    // The detected rail has both connections full and neither is to us
                    connectedRight = false;
                }
            }
        }
        
        // If no rails detected, reset prevRail and nextRail
        if (!connectedFront && !connectedBack && !connectedLeft && !connectedRight)
        {
            prevRail = null;
            nextRail = null;
        }
        
        // Update appearance if connection state changed or rail references changed
        if (prevConnectedFront != connectedFront || 
            prevConnectedBack != connectedBack || 
            prevConnectedLeft != connectedLeft || 
            prevConnectedRight != connectedRight ||
            prevRail != oldPrevRail || nextRail != oldNextRail)
        {
            UpdateRailAppearance();
        }
    }
    
    private void UpdateRailAppearance()
    {
        int connectionCount = 0;
        if (connectedFront) connectionCount++;
        if (connectedBack) connectionCount++;
        if (connectedLeft) connectionCount++;
        if (connectedRight) connectionCount++;

        if (connectionCount < 3)
        {
            ResetRails();
        }
        
        if (connectionCount == 0)
        {
            RailRight.SetActive(true);
            return;
        }
        
        if (connectionCount == 1)
        {
            if (connectedLeft)
            {
                RailLeft.SetActive(true);
            }
            else if (connectedRight)
            {
                RailRight.SetActive(true);
            }
            else if (connectedFront)
            {
                RailUp.SetActive(true);
            }
            else if (connectedBack)
            {
                RailDown.SetActive(true);
            }
            return;
        }
        
        if (connectionCount == 2)
        {
            // 직선 레일 (가로)
            if (connectedLeft && connectedRight)
            {
                RailLeft.SetActive(true);
                RailRight.SetActive(true);
            }
            // 직선 레일 (세로)
            else if (connectedFront && connectedBack)
            {
                RailUp.SetActive(true);
                RailDown.SetActive(true);
            }
            // 코너 레일 (왼쪽-위) - 수정된 부분
            else if (connectedLeft && connectedFront)
            {
                RailLeftBottom.SetActive(true);
            }
            // 코너 레일 (오른쪽-위)
            else if (connectedRight && connectedFront)
            {
                RailRightTop.SetActive(true);
            }
            // 코너 레일 (왼쪽-아래) - 수정된 부분
            else if (connectedLeft && connectedBack)
            {
                RailLeftTop.SetActive(true);
            }
            // 코너 레일 (오른쪽-아래)
            else if (connectedRight && connectedBack)
            {
                RailRightBottom.SetActive(true);
            }
        }
    }
    
    public void PlaySpawnAnimation(float spawnOffset)
    {
        StartCoroutine(SpawnCoroutine(spawnOffset));
    }
    
    //스폰 애니메이션
    private IEnumerator SpawnCoroutine(float spawnOffset)
    {
        Vector3 finalPos = transform.position + Vector3.down * spawnOffset;
        
        float moveDuration = 2.5f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            float easedT = EaseOutQuart(t);
            transform.position = Vector3.Lerp(startPos, finalPos, easedT);
            yield return null;
        }
        
        transform.position = finalPos;
    }
    
    private float EaseOutQuart(float t)
    {
        return 1f - Mathf.Pow(1f - t, 4f);
    }
}