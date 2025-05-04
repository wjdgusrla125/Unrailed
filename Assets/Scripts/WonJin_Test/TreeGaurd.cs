using UnityEngine;
using System.Collections;

public class TreeGaurd : MonoBehaviour
{
    public BoxCollider GaurdCol;
    public void Update()
    {
        if(gameObject.transform.childCount == 1)
        {
            GaurdCol.center = new Vector3(0, 0, 0);
            GaurdCol.size = new Vector3(1, 1, 1);
        }
        else
        {
            GaurdCol.center = new Vector3(0, 0.5f, 0);
            GaurdCol.size = new Vector3(1f, 2, 1f);
        }
    }
}
