﻿using System.Collections;
using UnityEngine;

public class Water: Blocks
{
    [SerializeField] private WaterFall waterFall1; //키패드 기준 1시 방향으로 떨어지는 폭포
    [SerializeField] private WaterFall waterFall3; //키패드 기준 3시 방향으로 떨어지는 폭포
    [SerializeField] private WaterFall waterFall7; //키패드 기준 7시 방향에서 떨어지는 폭포

    public override TileType BlockTileType => TileType.River;

    protected override void BlockInit()
    {
        waterFall1.gameObject.SetActive(false);
        waterFall3.gameObject.SetActive(false);
        waterFall7.gameObject.SetActive(false);
    }

    public void ActivateWaterFall(int dir)
    {
        switch (dir)
        {
            case 1:
                waterFall1.gameObject.SetActive(true);
                waterFall1.SetWaterFall(dir);
                break;
            case 3:
                waterFall3.gameObject.SetActive(true);
                waterFall3.SetWaterFall(dir);
                break;
            case 7:
                waterFall7.gameObject.SetActive(true);
                waterFall7.SetWaterFall(dir);
                break;
        }
    }
}
