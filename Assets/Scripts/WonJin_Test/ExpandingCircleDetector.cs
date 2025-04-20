using System.Collections;
using System.Collections.Generic;
using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.UI;

public class ExpandingCircleDetector : MonoBehaviour
{
    public static ExpandingCircleDetector Instance { get; private set; }

    [Header("Detection Settings")]
    public float radius = 0.5f;
    public float maxRadius = 100f;
    public float speed = 1f;
    public LayerMask detectableLayers;

    [SerializeField] private bool IsJoinShop = false;
    [SerializeField] private bool IsExitShop = false;

    [Header("References")]
    public Material material;
    public Transform planeTransform;
    [Header("상점 게이지")]
    public GameObject ShopGuage;
    private Coroutine FillCoroutine;
    public bool IsHold = false;
    HashSet<GameObject> previouslyDetected = new HashSet<GameObject>();

    IEnumerator FillImageOverTime()
    {
        float elapsedTime = 0f; // 경과 시간

        while (elapsedTime < 2f)
        {
            if (IsHold)
                yield break;
            elapsedTime += Time.deltaTime; 
            ShopGuage.transform.GetChild(0).GetComponent<Image>().fillAmount = Mathf.Lerp(0f, 1f, elapsedTime / 2f); // 0에서 1까지 선형 보간
            yield return null; 
        }
        ShopGuage.transform.GetChild(0).GetComponent<Image>().fillAmount = 1f;
        yield return null;
        ShopGuage.transform.GetChild(0).GetComponent<Image>().fillAmount = 0f;
        ShopGuage.SetActive(false);
        FillCoroutine = null;
    }

    public void JoinShop() => IsJoinShop = true;
    public void ExitShop()
    {
        IsExitShop = true; 
        IsJoinShop = false;
    }
    public bool GetJoin() => IsJoinShop;
    public bool GetExit() => IsExitShop;
    public void SetGuageBar(Vector3 pos, bool Ison)
    {
        ShopGuage.SetActive(Ison);
        ShopGuage.GetComponent<RectTransform>().position = pos;
        if (Ison)
        {
            if (FillCoroutine == null)
                FillCoroutine = StartCoroutine(FillImageOverTime());
        }
        else
        {
            if (FillCoroutine != null)
            {
                StopCoroutine(FillCoroutine);
                FillCoroutine = null;
                ShopGuage.transform.GetChild(0).GetComponent<Image>().fillAmount = 0f;
            }
        }
    }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // 중복 방지
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        material.SetFloat("_Radius", 0);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.O)) JoinShop();
        if (Input.GetKeyDown(KeyCode.P)) ExitShop();

        Collider[] hits = Physics.OverlapSphere(transform.position, radius, detectableLayers);
        HashSet<GameObject> currentlyDetected = new HashSet<GameObject>();
        foreach (var hit in hits)
        {
            currentlyDetected.Add(hit.gameObject);
        }

        if (material.GetFloat("_Radius") >= 1 && IsJoinShop) material.SetFloat("_Radius", 1);
        if (material.GetFloat("_Radius") <= 0 && IsExitShop)
        {
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
            if (material.GetFloat("_Radius") == 1)
                return;
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

        if (material != null && planeTransform != null)
        {
            float planeWorldSize = planeTransform.localScale.x * 10f;
            float radiusNormalized = Mathf.Clamp01(radius / planeWorldSize);
            if (radius == 1)
                return;
            material.SetFloat("_Radius", radiusNormalized);
        }

        previouslyDetected = currentlyDetected;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
