using UnityEngine;

public class Item : MonoBehaviour
{
    [SerializeField] private ItemType itemType;
    [SerializeField] private bool withTwoHanded;
    [SerializeField] private bool isStackable;

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

    public bool IsStackable
    {
        get => isStackable;
        set => isStackable = value;
    }
}