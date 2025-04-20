using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerPoket : MonoBehaviour
{
    [Header("플레이어 자원 텍스트")]
    public TMP_Text SeedValueText;
    public TMP_Text DistanceValueText;
    public TMP_Text BoltValueText;
    [Header("플레이어 자원")]
    [SerializeField] private string Seed;
    [SerializeField] private float Distance;
    [SerializeField] private int Bolt;
    public static PlayerPoket Instance { get; private set; }

    private void Awake()
    {
        // 인스턴스가 이미 존재하면 중복 제거
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 씬 전환 시에도 파괴되지 않게 유지하려면 아래 코드 사용
        DontDestroyOnLoad(gameObject);
    }
    
    // 열차 주행 거리에 호출될 함수 (미완)
    public void SetDistance(float Speed)
    {
        Distance += Time.deltaTime * Speed;
        DistanceValueText.text = Distance.ToString() + "m";
    }

    public void AddBolt()
    {
        Bolt++;
        BoltValueText.text = Bolt.ToString();
    }

    public bool BuyItem(int cost)
    {
        if (cost > Bolt)
        {
            Debug.Log("구매 실패!");
            return false;
        }
        else if (cost <= Bolt)
        {
            Debug.Log("구매 성공!");
            Bolt -= cost;
            BoltValueText.text = Bolt.ToString();
            return true;
        }
        return false;
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.B)) 
        {
            AddBolt();
        }
    }
}
