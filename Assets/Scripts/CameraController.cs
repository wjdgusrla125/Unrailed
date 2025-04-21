
using UnityEngine;

public class CameraController: MonoBehaviour
{
    private TrainManager _cameraTarget;
    private float _offsetX;
    private bool _isFollowing = false;

    ///InitCamera에서 target와 카메라의 현재 거리를 offset으로 저장한 후 
    ///StartCamera에서 camera가 offset만큼의 거리를 유지하며 target을 따라간다.
    ///(주의) target은 worldPosition을 기준으로 x, z축으로 이동하지만 camera는 x축만을 따라간다.(카메라는 좌우이동만 한다는 뜻)
    public void InitCamera(TrainManager target)
    {
        _cameraTarget = target;
        _offsetX = transform.position.x - _cameraTarget.transform.position.x;
    }

    public void StartCamera()
    {
        _isFollowing = true;
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
}
