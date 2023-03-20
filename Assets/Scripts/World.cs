using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
    public Transform PlayerTransform;
    public ChunkCoord PlayerChunkCoord;
    
    public BlockType[] BlockTypes => blockTypes;

    [SerializeField] private int Seed;
    [SerializeField] private BiomeAttributes biome;
    [SerializeField] private Material material;
    [SerializeField] private Material transparentMaterial;
    [SerializeField] private GameObject DebugScreen;
    [SerializeField] private BlockType[] blockTypes;

    private Vector3 spawnPosition;
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
        PlayerTransform.position = spawnPosition;
        playerLastChunkCoord = PlayerChunkCoord = GetChunkCoordFromVector3(PlayerTransform.position);
    }

    private void Update()
    {
        PlayerChunkCoord = GetChunkCoordFromVector3(PlayerTransform.position);

        if (!PlayerChunkCoord.Equals(playerLastChunkCoord))
            CheckViewDistance();

        if (chunksToCreate.Count > 0 && !isCreatingChunks)
            StartCoroutine(CreateChunks());

        if (Input.GetKeyDown(KeyCode.F3))
            DebugScreen.SetActive(!DebugScreen.activeSelf);
    }

    #region public method

    public bool CheckForVoxel(Vector3 pos)
    {
        ChunkCoord thisChunkCoord = new ChunkCoord(pos);

        if (!IsChunkInWorld(thisChunkCoord) || pos.y < 0 || pos.y > VoxelData.ChunkHeight)
            return false;

        if (chunks[thisChunkCoord.x, thisChunkCoord.z] != null && chunks[thisChunkCoord.x, thisChunkCoord.z].isVoxelMapPopulated)
            return blockTypes[chunks[thisChunkCoord.x, thisChunkCoord.z].GetVoxelFromGlobalVector3(pos)].isSolid;

        return blockTypes[GetVoxel(pos)].isSolid;
    }

    public bool CheckIfVoxelTransparent(Vector3 pos)
    {
        ChunkCoord thisChunkCoord = new ChunkCoord(pos);

        if (!IsChunkInWorld(thisChunkCoord) || pos.y < 0 || pos.y > VoxelData.ChunkHeight)
            return false;

        if (chunks[thisChunkCoord.x, thisChunkCoord.z] != null && chunks[thisChunkCoord.x, thisChunkCoord.z].isVoxelMapPopulated)
            return blockTypes[chunks[thisChunkCoord.x, thisChunkCoord.z].GetVoxelFromGlobalVector3(pos)].isTransparent;

        return blockTypes[GetVoxel(pos)].isTransparent;
    }

    public byte GetVoxel(Vector3 pos)
    {
        int yPos = (Mathf.FloorToInt(pos.y));
        byte voxelValue = (byte)BLOCK_TYPE_ID.Air;

        // IMMUTABLE PASS
        // If outside world, return air
        // probably nothing
        // if (!IsVoxelInWorld(pos))
        //     voxelValue = (byte)BLOCK_TYPE_ID.Air;

        // if bottom block of chunk, return bedrock.
        if (yPos == 0)
            voxelValue = (byte)BLOCK_TYPE_ID.bedrock;


        // BASIC TERRAIN PASS
        int terrainHeight = Mathf.FloorToInt(VoxelData.ChunkHeight * Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.terrainScale)) + biome.solidGroundHeight;

        if (yPos > terrainHeight)
            voxelValue = (byte)BLOCK_TYPE_ID.Air;
        else if (yPos == terrainHeight)
            voxelValue = (byte)BLOCK_TYPE_ID.Soil;
        else if (yPos < terrainHeight && yPos > terrainHeight - 4)
            voxelValue = (byte)BLOCK_TYPE_ID.Dirt;
        else
        {
            // SECOND PASS
            foreach (Lode lode in biome.lodes)
            {
                if (yPos > lode.minHeight && yPos < lode.maxHeight)
                    if (Noise.Get3DPerlin(pos, lode.noiseOffset, lode.scale, lode.threshold))
                        voxelValue = lode.blockID;
            }
        }

        return voxelValue;
    }
    #endregion

    #region private method
    private void GenerateWorld()
    {
        int chunkStartPosition = (VoxelData.WorldSizeInChunks / 2) - VoxelData.ViewDistanceInChunks;
        int chunkEndPosition = (VoxelData.WorldSizeInChunks / 2) + VoxelData.ViewDistanceInChunks;

        for (int x = chunkStartPosition; x < chunkEndPosition; x++)
        {
            for(int z = chunkStartPosition; z < chunkEndPosition; z++)
            {
                ChunkCoord coord = new ChunkCoord(x, z);
                chunks[x, z] = new Chunk(coord, this, material, transparentMaterial, true);
                currentActiveChunks.Add(coord);
            }
        }
    }

    private IEnumerator CreateChunks()
    {
        isCreatingChunks = true;

        while(chunksToCreate.Count > 0)
        {
            chunks[chunksToCreate[0].x, chunksToCreate[0].z].Init(material, transparentMaterial);
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

    public Chunk GetChunkFromVector3(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
        return chunks[x, z];
    }

    private void CheckViewDistance()
    {
        ChunkCoord playerCoord = GetChunkCoordFromVector3(PlayerTransform.position);
        playerLastChunkCoord = PlayerChunkCoord;

        List<ChunkCoord> previouslyActiveChunks = new List<ChunkCoord>(currentActiveChunks);

        for (int x = playerCoord.x - VoxelData.ViewDistanceInChunks; x < playerCoord.x + VoxelData.ViewDistanceInChunks; x++)
        {
            for (int z = playerCoord.z - VoxelData.ViewDistanceInChunks; z < playerCoord.z + VoxelData.ViewDistanceInChunks; z++)
            {
                ChunkCoord coord = new ChunkCoord(x, z);
                if(IsChunkInWorld(coord))
                {
                    if (chunks[x, z] == null)
                    {
                        chunks[x, z] = new Chunk(coord, this, material, transparentMaterial,false);
                        chunksToCreate.Add(coord);
                    }   
                    else if (!chunks[x, z].IsActive)
                    {
                        chunks[x, z].IsActive = true;
                    }
                    currentActiveChunks.Add(coord);
                }

                for (int i = 0; i < previouslyActiveChunks.Count; i++)
                {
                    if (previouslyActiveChunks[i].Equals(coord))
                        previouslyActiveChunks.RemoveAt(i);
                }
            }
        }

        foreach (ChunkCoord c in previouslyActiveChunks)
            chunks[c.x, c.z].IsActive = false;
    }

    private bool IsChunkInWorld(ChunkCoord coord) => coord.x > 0 && coord.x < VoxelData.WorldSizeInChunks - 1 && coord.z > 0 && coord.z < VoxelData.WorldSizeInChunks - 1;
    // private bool IsVoxelInWorld(Vector3 pos) => pos.x >= 0 && pos.x < VoxelData.WorldSizeInVoxels && pos.y >= 0 && pos.y < VoxelData.ChunkHeight && pos.z >= 0 && pos.z < VoxelData.WorldSizeInVoxels;
    #endregion
}