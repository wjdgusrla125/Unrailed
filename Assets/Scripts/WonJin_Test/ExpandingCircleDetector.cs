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
    [Header("게이지 관련")]
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

    public void JoinShop()
    {
        IsJoinShop = true;
        gameObject.transform.position = new Vector3(MapGenerator.Instance.GetPosB().x, 0.5f, MapGenerator.Instance.GetPosB().y);
    }
    public void ExitShop()
    {
        IsExitShop = true; 
        IsJoinShop = false;
        gameObject.transform.position = new Vector3(MapGenerator.Instance.GetPosB().x, 0.5f, MapGenerator.Instance.GetPosB().y);
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
            Destroy(gameObject); // 중복 제거
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
                ProcessObjectVisibility(obj, false);
            }

            foreach (var obj in previouslyDetected)
            {
                if (!currentlyDetected.Contains(obj))
                {
                    ProcessObjectVisibility(obj, false);
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
                ProcessObjectVisibility(obj, true);
            }
        }
        else if (IsExitShop)
        {
            radius -= speed * Time.deltaTime * 50f;

            foreach (var obj in previouslyDetected)
            {
                if (!currentlyDetected.Contains(obj))
                {
                    ProcessObjectVisibility(obj, false);
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

    // 게임 오브젝트의 가시성을 처리하는 새로운 메소드
    private void ProcessObjectVisibility(GameObject obj, bool isJoiningShop)
    {
        if (obj == null) return;

        if (obj.transform.childCount > 0)
        {
            if (obj.gameObject.layer == LayerMask.NameToLayer("ShopTile"))
            {
                obj.transform.GetChild(0).gameObject.SetActive(isJoiningShop);
            }
            else
            {
                if (obj.transform.childCount == 2 && obj.gameObject.layer == LayerMask.NameToLayer("Tile"))
                {
                    obj.transform.GetChild(0).gameObject.SetActive(!isJoiningShop);
                    obj.transform.GetChild(1).gameObject.SetActive(!isJoiningShop);
                    
                    MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = !isJoiningShop;
                    }
                }
                else if (obj.transform.childCount == 1 && obj.gameObject.layer == LayerMask.NameToLayer("Tile"))
                {
                    MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = !isJoiningShop;
                    }
                }
                else if (obj.gameObject.layer == LayerMask.NameToLayer("Item"))
                {
                    obj.transform.GetChild(0).gameObject.SetActive(!isJoiningShop);
                }
                else if (obj.gameObject.layer == LayerMask.NameToLayer("Water"))
                {
                    foreach (Transform child in obj.transform)
                    {
                        if (child.gameObject.activeSelf)
                        {
                            if (child.childCount == 0)
                            {
                                MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                                if (renderer != null)
                                {
                                    renderer.enabled = !isJoiningShop;
                                }
                            }
                            else
                            {
                                child.GetChild(0).gameObject.SetActive(!isJoiningShop);
                            }
                        }
                    }
                }
            }
        }
        else
        {
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = !isJoiningShop;
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}