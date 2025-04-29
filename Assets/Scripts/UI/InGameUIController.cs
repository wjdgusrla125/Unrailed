
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class InGameUIController: MonoBehaviour
{
    [SerializeField] private Text seed; //현재 시드값
    [SerializeField] private Text distance; //기차가 오른쪽으로 얼마나 이동했는지
    [SerializeField] private Text bolt; //현재 획득한 볼트의 수
    [SerializeField] private Text speed; //현재 기차의 속도
    
    private float _startX;
    private int _maxDistance = 0;
    
    public void InitDistance(float startPositionX)
    {
        _startX = startPositionX;
        _maxDistance = 0;
    }
    
    public void UpdateDistance(float currentPositionX)
    {
        int displacement = Mathf.RoundToInt(currentPositionX - _startX);
        if (displacement > _maxDistance)
        {
            _maxDistance = displacement;
            distance.text = _maxDistance + "m";
        }
    }

    public void UpdateBolt(int value)
    {
        bolt.text = value.ToString();
    }

    public void UpdateSpeed(float value)
    {
        speed.text = value.ToString("#.000") + "m/s";
    }

    public void SetReaderBoardText(string seedText = null, string distanceText = null, string boltText = null, string speedText = null)
    {
        if (seedText != null) seed.text = seedText;
        if (distanceText != null) distance.text = distanceText + "m";
        if (boltText != null) bolt.text = boltText;
        if (speedText != null)
            speed.text = (float.TryParse(speedText, out var spd) ? spd.ToString("#.000") : speedText) + "m/s";
    }
}
