using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class WaterTank : NetworkBehaviour
{
    private Material body_0;
    private Material body_1;
    [SerializeField] private Renderer body_0_R;
    [SerializeField] private Renderer body_1_R;
    [SerializeField] private List<GameObject> TrainOBJ;
    private Color startColor;
    private float duration = 60f;
    private Coroutine TankCoroutine;
    private Coroutine BurnCoroutine;

    private void Start()
    {
        body_0 = body_0_R.material;
        body_1 = body_1_R.material;
        TrainOBJ.Add(FindObjectOfType<Train_Head>().gameObject);
        TrainOBJ.Add(FindObjectOfType<CraftingTable>().gameObject);
        TrainOBJ.Add(FindObjectOfType<DeskInfo>().gameObject);
    }

    private void Update()
    {
        var burnObj = GetComponent<BurnTrainObject>();
        
        if (IsServer && TankCoroutine == null && !burnObj.Isburn.Value)
        {
            TankCoroutine = StartCoroutine(ChangeColorToRed());
        }

        if (burnObj.Isburn.Value)
        {
            if (TrainOBJ.TrueForAll(obj => obj.GetComponent<BurnTrainObject>().Isburn.Value)) return;
            if (BurnCoroutine == null)
                BurnCoroutine = StartCoroutine(RandomBurn());
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CoolingTankServerRpc()
    {
        GetComponent<BurnTrainObject>().Isburn.Value = false;
        UpdateTankColorClientRpc(startColor);
        RestartTankCoroutine();
    }

    [ClientRpc]
    private void UpdateTankColorClientRpc(Color color)
    {
        body_0.SetColor("_BaseColor", color);
        body_1.SetColor("_BaseColor", color);
    }

    private void RestartTankCoroutine()
    {
        if (TankCoroutine != null)
        {
            StopCoroutine(TankCoroutine);
        }
        
        TankCoroutine = StartCoroutine(ChangeColorToRed());
    }

    private IEnumerator ChangeColorToRed()
    {
        startColor = body_0.GetColor("_BaseColor");
        Color targetColor = Color.red;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            Color lerpedColor = Color.Lerp(startColor, targetColor, time / duration);

            body_0.SetColor("_BaseColor", lerpedColor);
            body_1.SetColor("_BaseColor", lerpedColor);

            // ✅ 클라이언트에게도 현재 색 전달
            UpdateTankColorClientRpc(lerpedColor);

            yield return null;
        }

        body_0.SetColor("_BaseColor", targetColor);
        body_1.SetColor("_BaseColor", targetColor);
        UpdateTankColorClientRpc(targetColor);

        GetComponent<BurnTrainObject>().Isburn.Value = true;
    }
    
    private IEnumerator RandomBurn()
    {
        while (GetComponent<BurnTrainObject>().Isburn.Value)
        {
            if (TrainOBJ.TrueForAll(obj => obj.GetComponent<BurnTrainObject>().Isburn.Value)) break;

            yield return new WaitForSeconds(5f);
            
            List<GameObject> unburned = TrainOBJ.FindAll(obj => !obj.GetComponent<BurnTrainObject>().Isburn.Value);
            
            if (unburned.Count > 0)
            {
                int randomIndex = Random.Range(0, unburned.Count);
                unburned[randomIndex].GetComponent<BurnTrainObject>().SetIsBurnServerRpc(false);
                ;
            }
        }
        BurnCoroutine = null;
    }
}
