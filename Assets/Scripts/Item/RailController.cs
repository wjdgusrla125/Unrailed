using System.Collections;
using Unity.Netcode;
using UnityEngine;

public enum RailShape
{
    Right,
    Left,
    Up,
    Down,
    StraightHorizontal,
    StraightVertical,
    CornerLeftTop,
    CornerLeftBottom,
    CornerRightTop,
    CornerRightBottom,
    None
}

public class RailController : NetworkBehaviour
{
    public bool isStartFirstRail = false;
    public bool isStartHeadRail = false;

    public bool isEndFirstRail = false;
    public bool isEndHeadRail = false;

    public GameObject RailRight;
    public GameObject RailLeftBottom;
    public GameObject RailLeftTop;
    public GameObject RailRightTop;
    public GameObject RailRightBottom;
    public GameObject RailUp;
    public GameObject RailDown;
    public GameObject RailLeft;

    public GameObject prevRail;
    public GameObject nextRail;

    private Vector2Int _gridPos;
    public Vector2Int GridPos => _gridPos;

    private void Awake()
    {
        RailRight.SetActive(true);
    }

    public void SetRail()
    {
        _gridPos = new Vector2Int(
            Mathf.RoundToInt(transform.position.x),
            Mathf.RoundToInt(transform.position.z)
        );

        RailManager.Instance.RegisterRail(this, _gridPos);

        if (IsServer)
        {
            RailManager.Instance.UpdateHeadRail();
        }
    }

    private void ResetRails()
    {
        RailRight.SetActive(false);
        RailLeftBottom.SetActive(false);
        RailLeftTop.SetActive(false);
        RailRightTop.SetActive(false);
        RailRightBottom.SetActive(false);
        RailUp.SetActive(false);
        RailDown.SetActive(false);
        RailLeft.SetActive(false);
    }

    public void UpdateRailAppearanceServer()
    {
        ResetRails();

        bool left = (prevRail && Mathf.RoundToInt(prevRail.transform.position.x) < _gridPos.x)
                    || (nextRail && Mathf.RoundToInt(nextRail.transform.position.x) < _gridPos.x);
        bool right = (prevRail && Mathf.RoundToInt(prevRail.transform.position.x) > _gridPos.x)
                     || (nextRail && Mathf.RoundToInt(nextRail.transform.position.x) > _gridPos.x);
        bool up = (prevRail && Mathf.RoundToInt(prevRail.transform.position.z) > _gridPos.y)
                  || (nextRail && Mathf.RoundToInt(nextRail.transform.position.z) > _gridPos.y);
        bool down = (prevRail && Mathf.RoundToInt(prevRail.transform.position.z) < _gridPos.y)
                    || (nextRail && Mathf.RoundToInt(nextRail.transform.position.z) < _gridPos.y);

        RailShape shape = RailShape.None;
        int count = (left ? 1 : 0) + (right ? 1 : 0) + (up ? 1 : 0) + (down ? 1 : 0);

        if (count <= 1)
        {
            if (left) shape = RailShape.Left;
            else if (right) shape = RailShape.Right;
            else if (up) shape = RailShape.Up;
            else if (down) shape = RailShape.Down;
        }
        else if (count == 2)
        {
            if (left && right) shape = RailShape.StraightHorizontal;
            else if (up && down) shape = RailShape.StraightVertical;
            else if (left && up) shape = RailShape.CornerLeftBottom;
            else if (right && up) shape = RailShape.CornerRightTop;
            else if (left && down) shape = RailShape.CornerLeftTop;
            else if (right && down) shape = RailShape.CornerRightBottom;
        }

        SetRailAppearanceClientRpc(shape);
    }

    [ClientRpc]
    public void SetRailAppearanceClientRpc(RailShape shape)
    {
        ResetRails();

        switch (shape)
        {
            case RailShape.Right:
                RailRight.SetActive(true); break;
            case RailShape.Left:
                RailLeft.SetActive(true); break;
            case RailShape.Up:
                RailUp.SetActive(true); break;
            case RailShape.Down:
                RailDown.SetActive(true); break;
            case RailShape.StraightHorizontal:
                RailLeft.SetActive(true);
                RailRight.SetActive(true);
                break;
            case RailShape.StraightVertical:
                RailUp.SetActive(true);
                RailDown.SetActive(true);
                break;
            case RailShape.CornerLeftTop:
                RailLeftTop.SetActive(true); break;
            case RailShape.CornerLeftBottom:
                RailLeftBottom.SetActive(true); break;
            case RailShape.CornerRightTop:
                RailRightTop.SetActive(true); break;
            case RailShape.CornerRightBottom:
                RailRightBottom.SetActive(true); break;
            default:
                break;
        }
    }

    public void PlaySpawnAnimation(float spawnOffset)
    {
        StartCoroutine(SpawnCoroutine(spawnOffset));
    }

    private IEnumerator SpawnCoroutine(float spawnOffset)
    {
        Vector3 finalPos = transform.position + Vector3.down * spawnOffset;

        float moveDuration = 2.5f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            float easedT = EaseOutQuart(t);
            transform.position = Vector3.Lerp(startPos, finalPos, easedT);
            yield return null;
        }

        transform.position = finalPos;
    }

    private float EaseOutQuart(float t)
    {
        return 1f - Mathf.Pow(1f - t, 4f);
    }
}
