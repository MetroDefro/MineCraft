using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Structure
{
    public static Queue<VoxelMode> MakeTree(Vector3Int position, int minTrunkHeight, int maxTrunkHeight)
    {
        Queue<VoxelMode> queue = new Queue<VoxelMode>();
        int height = (int)(maxTrunkHeight * Noise.Get2DPerlin(new Vector2(position.x, position.y), 250f, 3f));

        if(height < minTrunkHeight)
            height = minTrunkHeight;

        for (int i = 1; i < height; i++)
            queue.Enqueue(new VoxelMode(new Vector3Int(position.x, position.y + i, position.z), BLOCK_TYPE_ID.Wood));

        for (int x = -3; x < 4; x++)
        {
            for (int y = 0; y < 7; y++)
            {
                for (int z = -3; z < 4; z++)
                {
                    queue.Enqueue(new VoxelMode(new Vector3Int(position.x + x, position.y + height + y, position.z + z), BLOCK_TYPE_ID.Leaves));
                }
            }
        }

        return queue;
    }
}
