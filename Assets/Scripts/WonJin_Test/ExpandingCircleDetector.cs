using System.Collections.Generic;
using UnityEngine;

public class ExpandingCircleDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    public float radius = 0.5f;
    public float maxRadius = 100f;
    public float speed = 1f;
    public LayerMask detectableLayers;

    [SerializeField] private bool IsJoinShop = false;
    [SerializeField] private bool IsExitShop = false;

    [Header("References")]
    public Material material;        // Shader가 적용된 머티리얼
    public Transform planeTransform; // Plane 오브젝트의 Transform

    HashSet<GameObject> previouslyDetected = new HashSet<GameObject>();

    public void JoinShop() => IsJoinShop = true;
    public void ExitShop() => IsExitShop = true;

    private void Start()
    {
        material.SetFloat("_Radius", 0);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.O)) JoinShop();
        if (Input.GetKeyDown(KeyCode.P)) ExitShop();

        // 현재 프레임에서 감지된 오브젝트들 저장용
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, detectableLayers);
        HashSet<GameObject> currentlyDetected = new HashSet<GameObject>();
        foreach (var hit in hits)
        {
            currentlyDetected.Add(hit.gameObject);
        }

        if (material.GetFloat("_Radius") >= 1 && IsJoinShop) IsJoinShop = false;
        if (material.GetFloat("_Radius") <= 0 && IsExitShop)
        {
            foreach (var obj in currentlyDetected)
            {
                if (obj.transform.childCount > 0)
                {
                    obj.transform.GetChild(0).gameObject.SetActive(true);
                }
                else
                {
                    obj.transform.gameObject.GetComponent<MeshRenderer>().enabled = true;
                }
            }
            IsExitShop = false;
        }
        

        if (IsJoinShop)
        {
            radius += speed * Time.deltaTime * 50f;
            Debug.Log("ㅇㅇ");

            foreach (var obj in currentlyDetected)
            {
                if (obj.transform.childCount > 0)
                {

                    obj.transform.GetChild(0).gameObject.SetActive(false);

                }
                else
                {
                    obj.transform.gameObject.GetComponent<MeshRenderer>().enabled = false;
                }
            }
        }
        else if (IsExitShop)
        {
            radius -= speed * Time.deltaTime * 50f;

            // 이전에는 감지됐는데 지금은 빠진 애들만 다시 SetActive
            foreach (var obj in previouslyDetected)
            {
                if (!currentlyDetected.Contains(obj))
                {
                    if (obj.transform.childCount > 0)
                    {
                        obj.transform.GetChild(0).gameObject.SetActive(true);
                    }
                    else
                    {
                        obj.transform.gameObject.GetComponent<MeshRenderer>().enabled = true;
                    }
                }
            }
        }

        // Shader 마스크 반지름 적용
        if (material != null && planeTransform != null)
        {
            float planeWorldSize = planeTransform.localScale.x * 10f;
            if (radius == 1)
                return;
            float radiusNormalized = Mathf.Clamp01(radius / planeWorldSize);
            material.SetFloat("_Radius", radiusNormalized);
        }

        // 현재 감지 상태 저장
        previouslyDetected = currentlyDetected;
    }

    // Scene 뷰에서 감지 영역 표시
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
