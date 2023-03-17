using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
    public int Seed;
    public BiomeAttributes biome;

    public Transform player;
    public Vector3 spawnPosition;

    public Material material;
    public BlockType[] blockTypes;

    public GameObject DebugScreen;

    public ChunkCoord playerChunkCoord;
    private ChunkCoord playerLastChunkCoord;

    private Chunk[,] chunks = new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];
    private List<ChunkCoord> currentActiveChunks = new List<ChunkCoord>();
    private List<ChunkCoord> chunksToCreate = new List<ChunkCoord>();
    private bool isCreatingChunks;

    private void Start()
    {
        Random.InitState(Seed);

        spawnPosition = new Vector3((VoxelData.WorldSizeInChunks * VoxelData.ChunkWidth) / 2f, VoxelData.ChunkHeight - 40f, (VoxelData.WorldSizeInChunks * VoxelData.ChunkWidth) / 2f);
        GenerateWorld();
        playerLastChunkCoord = GetChunkCoordFromVector3(player.position);

    }

    private void Update()
    {
        playerChunkCoord = GetChunkCoordFromVector3 (player.position);

        if (!playerChunkCoord.Equals(playerLastChunkCoord))
            CheckViewDistance();

        if (chunksToCreate.Count > 0 && !isCreatingChunks)
            StartCoroutine(CreateChunks());

        if (Input.GetKeyDown(KeyCode.F3))
            DebugScreen.SetActive(!DebugScreen.activeSelf);
    }

    public bool CheckForVoxel(Vector3 pos)
    {
        ChunkCoord thisChunkCoord = new ChunkCoord(pos);

        if (!IsChunkInWorld(thisChunkCoord) || pos.y < 0 || pos.y > VoxelData.ChunkHeight)
            return false;

        if (chunks[thisChunkCoord.x, thisChunkCoord.z] != null && chunks[thisChunkCoord.x, thisChunkCoord.z].isVoxelMapPopulated)
            return blockTypes[chunks[thisChunkCoord.x, thisChunkCoord.z].GetVoxelFromGlobalVector3(pos)].isSolid;

        return blockTypes[GetVoxel(pos)].isSolid;
    }

    public byte GetVoxel(Vector3 pos)
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

        int terrainHeight = Mathf.FloorToInt(VoxelData.ChunkHeight * Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.terrainScale)) + biome.solidGroundHeight;
        byte voxelValue = 0;

        if (yPos == terrainHeight)
            voxelValue = 3;
        else if (yPos < terrainHeight && yPos > terrainHeight - 4)
            voxelValue = 5;
        else if (yPos > terrainHeight)
            return 0;
        else
            voxelValue = 2;

        // SECOND PASS

        if (voxelValue == 2)
        {
            foreach (Lode lode in biome.lodes)
            {
                if (yPos > lode.minHeight && yPos < lode.maxHeight)
                    if (Noise.Get3DPerlin(pos, lode.noiseOffset, lode.scale, lode.threshold))
                        voxelValue = lode.blockID;
            }
        }

        return voxelValue;

    }

    private void GenerateWorld()
    {
        for (int x = (VoxelData.WorldSizeInChunks / 2) - VoxelData.ViewDistanceInChunks; x < (VoxelData.WorldSizeInChunks / 2) + VoxelData.ViewDistanceInChunks; x++)
        {
            for(int z = (VoxelData.WorldSizeInChunks / 2) - VoxelData.ViewDistanceInChunks; z < (VoxelData.WorldSizeInChunks / 2) + VoxelData.ViewDistanceInChunks; z++)
            {
                ChunkCoord coord = new ChunkCoord(x, z);
                chunks[x, z] = new Chunk(coord, this, true);
                currentActiveChunks.Add(coord);
                // Yield
            }
        }

        player.position = spawnPosition;
    }

    private IEnumerator CreateChunks()
    {
        isCreatingChunks = true;

        while(chunksToCreate.Count > 0)
        {
            chunks[chunksToCreate[0].x, chunksToCreate[0].z].Init();
            chunksToCreate.RemoveAt(0);
            yield return null;
        }
        isCreatingChunks = false;
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
        playerLastChunkCoord = playerChunkCoord;

        List<ChunkCoord> previouslyActiveChunks = new List<ChunkCoord>(currentActiveChunks);

        for (int x = coord.x - VoxelData.ViewDistanceInChunks; x < coord.x + VoxelData.ViewDistanceInChunks; x++)
        {
            for (int z = coord.z - VoxelData.ViewDistanceInChunks; z < coord.z + VoxelData.ViewDistanceInChunks; z++)
            {
                ChunkCoord thisChunkCoord = new ChunkCoord(x, z);
                if(IsChunkInWorld(thisChunkCoord))
                {
                    if (chunks[x, z] == null)
                    {
                        chunks[x, z] = new Chunk(thisChunkCoord, this, false);
                        chunksToCreate.Add(thisChunkCoord);
                    }   
                    else if (!chunks[x, z].IsActive)
                    {
                        chunks[x, z].IsActive = true;

                    }
                    currentActiveChunks.Add(thisChunkCoord);
                }

                for(int i = 0; i < previouslyActiveChunks.Count; i++)
                {
                    if (previouslyActiveChunks[i].Equals (thisChunkCoord))
                        previouslyActiveChunks.RemoveAt (i);
                }
            }
        }

        foreach (ChunkCoord c in previouslyActiveChunks)
            chunks[c.x, c.z].IsActive = false;
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