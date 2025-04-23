
using System;
using System.Collections;
using UnityEngine;

public class CameraController: MonoBehaviour
{
    private TrainManager _cameraTarget;
    private float _offsetX;
    private bool _isFollowing = false;
    
    public Vector3 originInitPosition; //최초의 InitPosition
    private Vector3 _initPosition;
    
    private float _initialTargetX;
    private const float FollowThreshold = 3f; // 따라가기 시작할 거리

    private void Awake()
    {
        // 카메라 원래 위치 저장
        originInitPosition = transform.position;
        _initPosition = transform.position;
        
        // 카메라를 오른쪽으로 이동
        Vector3 pos = transform.position;
        pos.x = _initPosition.x + FollowThreshold;
        transform.position = pos;
    }

    //카메라를 초기상태로 전환
    public void ResetCamera()
    {
        _isFollowing = false;
        _cameraTarget = null;
        _initPosition = originInitPosition;
        Vector3 pos = transform.position;
        pos.x = _initPosition.x + FollowThreshold;
        transform.position = pos;
        
    }

    //기차가 생성될 때 호출
    public void InitCamera(TrainManager target)
    {
        _cameraTarget = target;
        _offsetX = _initPosition.x - _cameraTarget.transform.position.x;

        // 기차의 시작 X 저장
        _initialTargetX = _cameraTarget.transform.position.x;
    }

    //맵이 확장될 때 호출
    public void SetInitPosition()
    {
        _initPosition = transform.position;
    }

    // public void StartCamera()
    // {
    //     _isFollowing = true;
    // }
    
    private void LateUpdate()
    {
        if (!_cameraTarget) return;

        // 기차가 _initialTargetX만큼 이동했는지 체크
        if (!_isFollowing)
        {
            float moved = _cameraTarget.transform.position.x - _initialTargetX;
            if (moved >= FollowThreshold)
                _isFollowing = true;
        }

        if (_isFollowing)
        {
            Vector3 pos = transform.position;
            pos.x = _cameraTarget.transform.position.x + _offsetX;
            transform.position = pos;
        }
    }

    public void GameOverCameraMoving()
    {
        _isFollowing = false;
        StartCoroutine(GameOverCameraCoroutine());
    }
    
    private IEnumerator GameOverCameraCoroutine()
    {
        yield return new WaitForSeconds(3f);
        Vector3 startPos = transform.position;
        Vector3 targetPos = _initPosition;                     
        targetPos.x -= 14.0f;

        const float duration = 13f;
        float elapsed = 0f;

        float totalDistance = Vector3.Distance(startPos, targetPos);
        float speed = totalDistance / duration;

        bool spawnCrow = false; //까마귀 생성용

        while (elapsed < duration)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

            if (!spawnCrow && elapsed >= 2f)
            {
                spawnCrow = true;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        MapGenerator.Instance.AllTileDespawn();
        Debug.Log("GameOverCameraMoving 동작 종료");
    }
}
