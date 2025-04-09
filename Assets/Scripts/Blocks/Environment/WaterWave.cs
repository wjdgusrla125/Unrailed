
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class WaterWave: MonoBehaviour
{
    public WaterFall waterFall;
    private Coroutine _waveRoutine;
    [SerializeField] private GameObject brightObject;
    [SerializeField] private GameObject normalObject;
    private readonly Vector3 _brightUnderOffset = new (0, 0f, -5f);
    private readonly Vector3 _brightUpperOffset = new (0, 0f, 5f);

    // public int direction;
    

    public void WaveObjectInit(int dir)
    {
        // direction = dir;
        switch (dir)
        {
            case 1:
            case 3:
                brightObject.transform.localPosition += _brightUpperOffset;
                normalObject.transform.localPosition += _brightUpperOffset;
                break;
            case 7:
                brightObject.transform.localPosition += _brightUnderOffset;
                normalObject.transform.localPosition += _brightUnderOffset;
                break;
        }
    }

    public void StartWave()
    {
        if (_waveRoutine == null)
        {
            _waveRoutine = StartCoroutine(WaveCoroutine());
        }
    }

    public void StopWave()
    {
        if (_waveRoutine != null)
        {
            StopCoroutine(_waveRoutine);
            _waveRoutine = null;
        }
    }

    protected abstract IEnumerator WaveCoroutine();
}
