
using System;
using UnityEngine;

public class Train_Head: Train
{
    protected override TrainType Type => TrainType.Head;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        StopSmoke();
    }

    private void Start()
    {
        InitTrain();
    }

    protected override void InitTrainCar()
    {
        index = 0; //0번 열차로 설정
        frontTrainCar = null; //Head이기 때문에 앞열차는 없음.
        
        GameObject go = Instantiate(waterTankPrefab);
        Train train = go.GetComponent<Train>();
        if (train)
        {
            //앞뒤열차와 열차번호를 설정
            SetBackTrainCar(train);
            train.SetFrontTrainCar(this);
            train.SetDestinationRail(destinationRail); //첫 생성 시 뒷차에게 목적지를 공유
            train.SetTrainIndex(index + 1); //열차의 다음 번호로 할당
            train.InitTrain();
        }else Debug.LogError($"{go.name}에 Train 컴포넌트가 없음!!!");
    }
}
