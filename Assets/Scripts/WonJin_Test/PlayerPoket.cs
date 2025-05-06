using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerPoket : MonoBehaviour
{
    [Header("�÷��̾� �ڿ� �ؽ�Ʈ")]
    public Text SeedValueText;
    public Text DistanceValueText;
    public Text BoltValueText;
    [Header("�÷��̾� �ڿ�")]
    [SerializeField] private string Seed;
    [SerializeField] private float Distance;
    [SerializeField] private int Bolt;
    
    public static PlayerPoket Instance { get; private set; }

    private void Awake()
    {
        // �ν��Ͻ��� �̹� �����ϸ� �ߺ� ����
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // �� ��ȯ �ÿ��� �ı����� �ʰ� �����Ϸ��� �Ʒ� �ڵ� ���
        DontDestroyOnLoad(gameObject);
    }
    
    // ���� ���� �Ÿ��� ȣ��� �Լ� (�̿�)
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
            Debug.Log("���� ����!");
            return false;
        }
        else if (cost <= Bolt)
        {
            Debug.Log("���� ����!");
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
