using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelState
{
    public BLOCK_TYPE_ID id;
    public float globalLightPercent;

    public VoxelState()
    {
        id = BLOCK_TYPE_ID.Air;
        globalLightPercent = 0f;
    }

    public VoxelState(BLOCK_TYPE_ID id)
    {
        this.id = id;
    }
}