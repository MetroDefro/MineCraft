using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelMode
{
    public Vector3Int position;
    public BLOCK_TYPE_ID id;

    public VoxelMode()
    {
        position = new Vector3Int();
        id = 0;
    }

    public VoxelMode(Vector3Int position, BLOCK_TYPE_ID id)
    {
        this.position = position;
        this.id = id;
    }
}