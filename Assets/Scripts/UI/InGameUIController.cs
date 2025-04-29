
using UnityEngine;
using UnityEngine.UI;

public class InGameUIController: MonoBehaviour
{
    [SerializeField] private Text seed;
    [SerializeField] private Text distance;
    [SerializeField] private Text bolt;
    [SerializeField] private Text speed;
    
    private float _cachedSpeed;
    private string _lastSpeedText;
    public float Speed
    {
        get
        {
            if (speed.text == _lastSpeedText) return _cachedSpeed;
            _lastSpeedText = speed.text;
            _cachedSpeed = float.TryParse(_lastSpeedText, out var v) ? v : 0;
            return _cachedSpeed;
        }
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
