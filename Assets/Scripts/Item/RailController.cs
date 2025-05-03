using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

public class RailController : NetworkBehaviour
{
    public bool isStartFirstRail = false;//시작점의 최초 레일
    public bool isStartHeadRail = false; //true인 레일이 마지막 레일
    
    public bool isEndFirstRail = false; //끝점의 최초 레일
    public bool isEndHeadRail = false; //끝점의 마지막(가장 오른쪽) 레일
    
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

    public GameObject prevRail;
    public GameObject nextRail;
    
    private Vector2Int _gridPos;
    public Vector2Int GridPos => _gridPos;
    
    private void Awake()
    {
        RailRight.SetActive(true);
    }
    
    // public override void OnNetworkSpawn()
    // {
    //     SetRail();
    // }

    public void SetRail()
    {
        _gridPos = new Vector2Int(
            Mathf.RoundToInt(transform.position.x),
            Mathf.RoundToInt(transform.position.z)
        );

        RailManager.Instance.RegisterRail(this, _gridPos);
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
    
    public void UpdateRailAppearance()
    {
        ResetRails();

        bool left  = (prevRail && Mathf.RoundToInt(prevRail.transform.position.x) < _gridPos.x)
                     || (nextRail && Mathf.RoundToInt(nextRail.transform.position.x) < _gridPos.x);
        bool right = (prevRail && Mathf.RoundToInt(prevRail.transform.position.x) > _gridPos.x)
                     || (nextRail && Mathf.RoundToInt(nextRail.transform.position.x) > _gridPos.x);
        bool up    = (prevRail && Mathf.RoundToInt(prevRail.transform.position.z) > _gridPos.y)
                     || (nextRail && Mathf.RoundToInt(nextRail.transform.position.z) > _gridPos.y);
        bool down  = (prevRail && Mathf.RoundToInt(prevRail.transform.position.z) < _gridPos.y)
                     || (nextRail && Mathf.RoundToInt(nextRail.transform.position.z) < _gridPos.y);

        int count = (left ? 1 : 0) + (right ? 1 : 0) + (up ? 1 : 0) + (down ? 1 : 0);

        if (count <= 1)
        {
            if (left) RailLeft.SetActive(true);
            else if (right) RailRight.SetActive(true);
            else if (up) RailUp.SetActive(true);
            else if (down) RailDown.SetActive(true);
        }
        else if (count == 2)
        {
            if (left && right)
            {
                RailLeft.SetActive(true);
                RailRight.SetActive(true);
            }
            else if (up && down)
            {
                RailUp.SetActive(true);
                RailDown.SetActive(true);
            }
            else if (left && up) RailLeftBottom.SetActive(true);
            else if (right && up) RailRightTop.SetActive(true);
            else if (left && down) RailLeftTop.SetActive(true);
            else if (right && down) RailRightBottom.SetActive(true);
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