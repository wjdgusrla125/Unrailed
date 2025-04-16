
using UnityEngine;

public class Train_CraftDesk: Train
{
    protected override TrainType Type => TrainType.CraftDesk;
    protected override void InitTrainCar()
    {
        backTrainCar = null; //마지막 열차이기 때문에 뒷열차는 없음.
        manager.PlaySpawnAnimation();
    }
}
