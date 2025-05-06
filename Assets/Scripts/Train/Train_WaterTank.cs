
using UnityEngine;

public class Train_WaterTank: Train
{
    protected override TrainType Type => TrainType.WaterTank;
    protected override void InitTrainCar()
    {
        GameObject go = Instantiate(chestBoxPrefab);
        Train train = go.GetComponent<Train>();
        if (train)
        {
            //앞뒤열차와 열차번호를 설정
            SetBackTrainCar(train);
            train.SetFrontTrainCar(this);
            train.SetDestinationRail(destinationRail);
            train.SetTrainIndex(index + 1);
            train.InitTrain();
        }else Debug.LogError($"{go.name}에 Train 컴포넌트가 없음!!!");
    }
}
