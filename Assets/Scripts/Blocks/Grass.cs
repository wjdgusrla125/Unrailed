
using UnityEngine;

public class Grass: Blocks
{
    public override TileType BlockTileType => TileType.Grass;

    protected override void AdditionalCreateBlock()
    {
    }
}
