using UnityEngine;

public abstract class Blocks: MonoBehaviour
{
    
    [SerializeField, Header("블럭 위에 소환될 오브젝트")] private GameObject[] envPrefab;
    
    
}
