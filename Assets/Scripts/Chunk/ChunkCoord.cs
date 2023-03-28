using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkCoord
{
    public int x;
    public int z;

    public ChunkCoord()
    {
        x = 0;
        z = 0;
    }

    public ChunkCoord(int x, int z)
    {
        this.x = x;
        this.z = z;
    }

    public ChunkCoord(Vector3 pos)
    {
        x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
    }

    public ChunkCoord(Vector3Int pos)
    {
        x = pos.x / VoxelData.ChunkWidth;
        z = pos.z / VoxelData.ChunkWidth;
    }

    public bool Equals(ChunkCoord other)
    {
        if (other == null)
            return false;
        else if (other.x == x && other.z == z)
            return true;
        else
            return false;
    }
}