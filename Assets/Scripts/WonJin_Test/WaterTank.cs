using System.Collections;
using UnityEngine;

public class WaterTank : MonoBehaviour
{
    private Material body_0;
    private Material body_1;
    [SerializeField] private Renderer body_0_R;
    [SerializeField] private Renderer body_1_R;
    private float duration = 10f;     // ���� �ٲ�� �� �ɸ��� �ð�

    private IEnumerator ChangeColorToRed()
    {
        Color startColor = body_0.GetColor("_BaseColor"); // URP/Lit ���̴��� �÷� ������Ƽ
        Color targetColor = Color.red;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            body_0.SetColor("_BaseColor", Color.Lerp(startColor, targetColor, time / duration));
            body_1.SetColor("_BaseColor", Color.Lerp(startColor, targetColor, time / duration));
            yield return null;
        }

        body_0.SetColor("_BaseColor", targetColor); // ������ �� Ȯ���ϰ�
        body_1.SetColor("_BaseColor", targetColor); // ������ �� Ȯ���ϰ�
        gameObject.GetComponent<BurnTrainObject>().Isburn = true;
    }
    private void Start()
    {
        body_0 = body_0_R.material; 
        body_1 = body_1_R.material;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            StartCoroutine(ChangeColorToRed());
        }
    }
}
