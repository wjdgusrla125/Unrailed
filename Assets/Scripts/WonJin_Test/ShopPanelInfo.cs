using TMPro;
using UnityEngine;

public class ShopPanelInfo : MonoBehaviour
{
    [Header("0은 exit, 나머지는 가격")]
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
