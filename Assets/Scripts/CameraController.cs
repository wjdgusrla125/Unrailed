
using System.Collections;
using UnityEngine;

public class CameraController: MonoBehaviour
{
    private TrainManager _cameraTarget;
    private float _offsetX;
    private bool _isFollowing = false;
    private Vector3 _initPosition;

    public void InitCamera(TrainManager target)
    {
        _initPosition = transform.position; //초기 위치 저장
        _cameraTarget = target;
        _offsetX = transform.position.x - _cameraTarget.transform.position.x;
        
        Vector3 pos = transform.position;
        pos.x = transform.position.x + 5f;
        transform.position = pos;
    }

    public void StartCamera()
    {
        // _isFollowing = true;
    }
    
    private void LateUpdate()
    {
        if (_isFollowing && _cameraTarget)
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

        const float duration = 10f;                           
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
