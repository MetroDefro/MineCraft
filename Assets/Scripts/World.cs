using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
    public Material material;
    public BlockType[] blockTypes;

    private void Start()
    {
        Chunk newChunk = new Chunk(new ChunkCoord(0, 0), this);
        Chunk newChunk2 = new Chunk(new ChunkCoord(1, 0), this);
    }

    private void GenerateWorld()
    {
        for (int x = 0; x < VoxelData.WorldSizeInChunks; x++)
        {
            for(int z = 0; z < VoxelData.WorldSizeInChunks; z++)
            {
                Chunk newChunk = new Chunk(new ChunkCoord(x, z), this);
            }
        }
    }
}

[System.Serializable]
public class BlockType
{
    public string blockName;
    public bool isSolid;

    [Header("Texture Values")]
    public int backFaceTexture;
    public int frontFaceTexture;
    public int topFaceTexture;
    public int bottomFaceTexture;
    public int leftFaceTexture;
    public int rightFaceTexture;

    private enum face
    {
        back = 0, 
        front = 1, 
        top = 2, 
        bottom = 3, 
        left = 4, 
        right = 5,
    }

    public int GetTextureID (int faceIndex)
    {
        switch(faceIndex)
        {
            case (int)face.back:
                return backFaceTexture;
            case (int)face.front:
                return frontFaceTexture;
            case (int)face.top:
                return topFaceTexture;
            case (int)face.bottom:
                return bottomFaceTexture;
            case (int)face.left:
                return leftFaceTexture;
            case (int)face.right:
                return rightFaceTexture;
            default:
                Debug.Log("Error in GetTextureID; invalid face index");
                return 0;

        }
    }
}