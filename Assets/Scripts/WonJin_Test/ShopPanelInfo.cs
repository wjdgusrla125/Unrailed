using TMPro;
using UnityEngine;

public class ShopPanelInfo : MonoBehaviour
{
    [Header("0�� exit, �������� ����")]
    public int ShopCost;
    public TMP_Text CostText;
    public void BuyAterCostUp()
    {
        if (ShopCost == 0)
            return;
        ShopCost += 2;
        CostText.text = ShopCost.ToString();
    }
}
