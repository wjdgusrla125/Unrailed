using System.Collections;
using UnityEngine;

public class WaterTank : MonoBehaviour
{
    private Material body_0;
    private Material body_1;
    [SerializeField] private Renderer body_0_R;
    [SerializeField] private Renderer body_1_R;
    private float duration = 10f;     // 색이 바뀌는 데 걸리는 시간

    private IEnumerator ChangeColorToRed()
    {
        Color startColor = body_0.GetColor("_BaseColor"); // URP/Lit 쉐이더의 컬러 프로퍼티
        Color targetColor = Color.red;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            body_0.SetColor("_BaseColor", Color.Lerp(startColor, targetColor, time / duration));
            body_1.SetColor("_BaseColor", Color.Lerp(startColor, targetColor, time / duration));
            yield return null;
        }

        body_0.SetColor("_BaseColor", targetColor); // 마지막 색 확실하게
        body_1.SetColor("_BaseColor", targetColor); // 마지막 색 확실하게
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
