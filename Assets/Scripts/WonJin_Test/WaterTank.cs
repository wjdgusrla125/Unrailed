using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class WaterTank : MonoBehaviour
{
    private Material body_0;
    private Material body_1;
    [SerializeField] private Renderer body_0_R;
    [SerializeField] private Renderer body_1_R;
    [SerializeField] private List<GameObject> TrainOBJ;
    Color startColor;
    private float duration = 10f;     // 색이 바뀌는 데 걸리는 시간
    private Coroutine TankCoroutine;
    private Coroutine BurnCoroutine;
    private IEnumerator ChangeColorToRed()
    {
        startColor = body_0.GetColor("_BaseColor"); // URP/Lit 쉐이더의 컬러 프로퍼티
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
        Debug.Log(gameObject.GetComponent<BurnTrainObject>().Isburn);
    }

    private IEnumerator RandomBurn()
    {
        while (gameObject.GetComponent<BurnTrainObject>().Isburn)
        {
            if(TrainOBJ[0].GetComponent<BurnTrainObject>().Isburn && TrainOBJ[1].GetComponent<BurnTrainObject>().Isburn && TrainOBJ[2].GetComponent<BurnTrainObject>().Isburn)
            {
                StopCoroutine(BurnCoroutine);
                BurnCoroutine = null;
                yield break;
            }
            yield return new WaitForSeconds(2.5f);
            BurnTrains();
        }
        StopCoroutine(BurnCoroutine);
        BurnCoroutine = null;
    }

    private void Start()
    {
        body_0 = body_0_R.material; 
        body_1 = body_1_R.material;
        TrainOBJ.Add(FindObjectOfType<Train_Head>().gameObject);
        TrainOBJ.Add(FindObjectOfType<CraftingTable>().gameObject);
        TrainOBJ.Add(FindObjectOfType<DeskInfo>().gameObject);
    }

    public void CoolingTank()
    {
        body_0.SetColor("_BaseColor", startColor); // 마지막 색 확실하게
        body_1.SetColor("_BaseColor", startColor); // 마지막 색 확실하게
        StopCoroutine(TankCoroutine);
        TankCoroutine = null;
        TankCoroutine = StartCoroutine(ChangeColorToRed());
    }

    private void BurnTrains()
    {
        // Isburn == false인 오브젝트만 필터링
        List<GameObject> unburned = TrainOBJ.FindAll(obj => !obj.GetComponent<BurnTrainObject>().Isburn);

        if (unburned.Count == 0)
            return; // 다 불타고 있으면 종료

        int randomIndex = Random.Range(0, unburned.Count);
        unburned[randomIndex].GetComponent<BurnTrainObject>().Isburn = true;
    }

    private void Update()
    {
        if (TankCoroutine == null && gameObject.GetComponent<BurnTrainObject>().Isburn == false)
        {
            TankCoroutine = StartCoroutine(ChangeColorToRed());
        }

        if (gameObject.GetComponent<BurnTrainObject>().Isburn)
        {
            if (TrainOBJ[0].GetComponent<BurnTrainObject>().Isburn && TrainOBJ[1].GetComponent<BurnTrainObject>().Isburn && TrainOBJ[2].GetComponent<BurnTrainObject>().Isburn)
                return;
            if (BurnCoroutine == null)
                BurnCoroutine = StartCoroutine(RandomBurn());
        }
    }
}
