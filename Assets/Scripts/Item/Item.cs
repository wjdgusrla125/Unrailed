using UnityEngine;

public class Item : MonoBehaviour
{
    [SerializeField] private ItemType itemType;
    [SerializeField] private bool withTwoHanded;

    public ItemType ItemType
    {
        get => itemType;
        set => itemType = value;
    }

    public bool WithTwoHanded
    {
        get => withTwoHanded;
        set => withTwoHanded = value;
    }
}