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
            // 남아 있는 감지된 오브젝트 전부 복구
            foreach (var obj in currentlyDetected)
            {
                if (obj.transform.childCount > 0)
                {
                    if (obj.gameObject.layer == LayerMask.NameToLayer("ShopTile"))
                        obj.transform.GetChild(0).gameObject.SetActive(false);
                    else
                        obj.transform.GetChild(0).gameObject.SetActive(true);
                }
                else if (obj.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    renderer.enabled = true;
                }
            }

            // previouslyDetected에만 있던 애들도 복구
            foreach (var obj in previouslyDetected)
            {
                if (!currentlyDetected.Contains(obj))
                {
                    if (obj.transform.childCount > 0)
                    {
                        if (obj.gameObject.layer == LayerMask.NameToLayer("ShopTile"))
                            obj.transform.GetChild(0).gameObject.SetActive(false);
                        else
                            obj.transform.GetChild(0).gameObject.SetActive(true);
                    }
                    else if (obj.TryGetComponent<MeshRenderer>(out var renderer))
                    {
                        renderer.enabled = true;
                    }
                }
            }

            IsExitShop = false;
        }

        if (IsJoinShop)
        {
            radius += speed * Time.deltaTime * 50f;

            foreach (var obj in currentlyDetected)
            {
                if (obj.transform.childCount > 0)
                {
                    if (obj.gameObject.layer == LayerMask.NameToLayer("ShopTile"))
                        obj.transform.GetChild(0).gameObject.SetActive(true);
                    else
                        obj.transform.GetChild(0).gameObject.SetActive(false);
                }
                else if (obj.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    renderer.enabled = false;
                }
            }
        }
        else if (IsExitShop)
        {
            radius -= speed * Time.deltaTime * 50f;

            foreach (var obj in previouslyDetected)
            {
                if (!currentlyDetected.Contains(obj))
                {
                    if (obj.transform.childCount > 0)
                    {
                        if (obj.gameObject.layer == LayerMask.NameToLayer("ShopTile"))
                            obj.transform.GetChild(0).gameObject.SetActive(false);
                        else
                            obj.transform.GetChild(0).gameObject.SetActive(true);
                    }
                    else if (obj.TryGetComponent<MeshRenderer>(out var renderer))
                    {
                        renderer.enabled = true;
                    }
                }
            }
        }

        // Shader 마스크 반지름 적용
        if (material != null && planeTransform != null)
        {
            float planeWorldSize = planeTransform.localScale.x * 10f;
            float radiusNormalized = Mathf.Clamp01(radius / planeWorldSize);
            if(radius == 1)
                return;
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
