using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
    public int Seed;

    public Transform player;
    public Vector3 spawnPosition;

    public Material material;
    public BlockType[] blockTypes;

    Chunk[,] chunks =new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];

    List<ChunkCoord> currentActiveChunks = new List<ChunkCoord> ();
    
    ChunkCoord playerChunkCoord;
    ChunkCoord playerLastChunkCoord;

    private void Start()
    {
        Random.InitState(Seed);

        spawnPosition = new Vector3((VoxelData.WorldSizeInChunks * VoxelData.ChunkWidth) / 2f, VoxelData.ChunkHeight + 2f, (VoxelData.WorldSizeInChunks * VoxelData.ChunkWidth) / 2f);
        GenerateWorld();
        playerLastChunkCoord = GetChunkCoordFromVector3(player.position);

    }

    private void Update()
    {
        playerChunkCoord = GetChunkCoordFromVector3 (player.position);

        if (!playerChunkCoord.Equals(playerLastChunkCoord))
            CheckViewDistance();
    }

    private void GenerateWorld()
    {
        for (int x = (VoxelData.WorldSizeInChunks / 2) - VoxelData.ViewDistanceInChunks; x < (VoxelData.WorldSizeInChunks / 2) + VoxelData.ViewDistanceInChunks; x++)
        {
            for(int z = (VoxelData.WorldSizeInChunks / 2) - VoxelData.ViewDistanceInChunks; z < (VoxelData.WorldSizeInChunks / 2) + VoxelData.ViewDistanceInChunks; z++)
            {
                CreateChunk(new ChunkCoord(x, z));
            }
        }

        player.position = spawnPosition;
    }

    private ChunkCoord GetChunkCoordFromVector3(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
        return new ChunkCoord(x, z);
    }

    private void CheckViewDistance()
    {
        ChunkCoord coord = GetChunkCoordFromVector3(player.position);

        List<ChunkCoord> previouslyActiveChunks = new List<ChunkCoord>(currentActiveChunks);

        for (int x = coord.x - VoxelData.ViewDistanceInChunks; x < coord.x + VoxelData.ViewDistanceInChunks; x++)
        {
            for (int z = coord.z - VoxelData.ViewDistanceInChunks; z < coord.z + VoxelData.ViewDistanceInChunks; z++)
            {
                ChunkCoord thisChunk = new ChunkCoord(x, z);
                if(IsChunkInWorld(thisChunk))
                {
                    if (chunks[x, z] == null)
                        CreateChunk(thisChunk);
                    else if (!chunks[x, z].isActive)
                    {
                        chunks[x, z].isActive = true;
                        currentActiveChunks.Add(thisChunk);
                    }
                }

                for(int i = 0; i < previouslyActiveChunks.Count; i++)
                {
                    if (previouslyActiveChunks[i].Equals (thisChunk))
                        previouslyActiveChunks.RemoveAt (i);
                }
            }
        }

        foreach (ChunkCoord c in previouslyActiveChunks)
            chunks[c.x, c.z].isActive = false;
    }

    public byte GetVoxel (Vector3 pos)
    {
        int yPos = (Mathf.FloorToInt(pos.y));

        // IMMUTABLE PASS
        
        // If outside world, return air
        if (!IsVoxelInWorld(pos))
            return 0;

        // if bottom block of chunk, return bedrock.
        if (yPos == 0)
            return 1;

        // BASIC TERRAIN PASS

        int terrainHeight = Mathf.FloorToInt(VoxelData.ChunkHeight * Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 500, 0.25f));

        if (yPos == terrainHeight)
            return 3;
        else if (yPos > terrainHeight)
            return 0;
        else
            return 2;

    }

    private void CreateChunk(ChunkCoord coord) 
    {
        chunks[coord.x, coord.z] = new Chunk(coord, this);
        currentActiveChunks.Add(coord);
    }

    private bool IsChunkInWorld(ChunkCoord coord) => coord.x >= 0 && coord.x < VoxelData.WorldSizeInChunks && coord.z >= 0 && coord.z < VoxelData.WorldSizeInChunks;

    private bool IsVoxelInWorld(Vector3 pos) => pos.x >= 0 && pos.x < VoxelData.WorldSizeInVoxels && pos.y >= 0 && pos.y < VoxelData.ChunkHeight && pos.z >= 0 && pos.z < VoxelData.WorldSizeInVoxels;
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