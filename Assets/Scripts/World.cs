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
    private List<Chunk> chunksToUpdate = new List<Chunk>();
    private bool applyingModifications = false;

    private Queue<VoxelMode> modifications = new Queue<VoxelMode>();

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

        if (modifications.Count > 0 && !applyingModifications)
            StartCoroutine(ApplyModifications());

        if (chunksToCreate.Count > 0)
            CreateChunk();

        if (chunksToCreate.Count > 0)
            UpdateChunks();

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
       
        return blockTypes[GetVoxelBlockType(pos)].isSolid;
    }

    public bool CheckIfVoxelTransparent(Vector3 pos)
    {
        ChunkCoord thisChunkCoord = new ChunkCoord(pos);

        // When a player creating a block in-game
        if (!IsChunkInWorld(thisChunkCoord) || pos.y < 0 || pos.y > VoxelData.ChunkHeight)
            return false;

        if (chunks[thisChunkCoord.x, thisChunkCoord.z] != null && chunks[thisChunkCoord.x, thisChunkCoord.z].isVoxelMapPopulated)
            return blockTypes[chunks[thisChunkCoord.x, thisChunkCoord.z].GetVoxelFromGlobalVector3(pos)].isTransparent;

        return blockTypes[GetVoxelBlockType(pos)].isTransparent;
    }

    public byte GetVoxelBlockType(Vector3 pos)
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

        // TREE PASS
        if(yPos == terrainHeight)
        {
            if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treeZoneScale) > biome.treeZoneThreshold)
            {
                if(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treePlacementScale) > biome.treePlacementThreshold)
                {
                    Structure.MakeTree(pos, modifications, biome.minTreeHeight, biome.maxTreeHeight);
                }
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

        while (modifications.Count > 0)
        {
            VoxelMode v = modifications.Dequeue();

            ChunkCoord c = GetChunkCoordFromVector3(v.position);

            if (chunks[c.x, c.z] == null)
            {
                chunks[c.x, c.z] = new Chunk(c, this, material, transparentMaterial, true);
                currentActiveChunks.Add(c);
            }

            chunks[c.x, c.z].modifications.Enqueue(v);

            if (!chunksToUpdate.Contains(chunks[c.x, c.z]))
                chunksToUpdate.Add(chunks[c.x, c.z]);
        }

        for(int i = 0; i < chunksToUpdate.Count; i++)
        {
            chunksToUpdate[0].UpdateChunk();
            chunksToUpdate.RemoveAt(0);
        }
    }

    private void CreateChunk()
    {
        ChunkCoord c = chunksToCreate[0];
        chunksToCreate.RemoveAt(0);
        currentActiveChunks.Add(c);
        chunks[c.x, c.z].Init(material, transparentMaterial);
    }

    private void UpdateChunks()
    {
        bool updated = false;
        int index = 0;

        while (!updated && index < chunksToUpdate.Count - 1)
        {
            if (chunksToUpdate[index].isVoxelMapPopulated)
            {
                chunksToUpdate[index].UpdateChunk();
                chunksToUpdate.RemoveAt(index);
                updated = true;
            }
            else
                index++;
        }
    }

    private IEnumerator ApplyModifications()
    {
        applyingModifications = true;
        int count = 0;

        while (modifications.Count > 0)
        {
            VoxelMode v = modifications.Dequeue();

            ChunkCoord c = GetChunkCoordFromVector3(v.position);

            if (chunks[c.x, c.z] == null)
            {
                chunks[c.x, c.z] = new Chunk(c, this, material, transparentMaterial, true);
                currentActiveChunks.Add(c);
            }

            chunks[c.x, c.z].modifications.Enqueue(v);

            if (!chunksToUpdate.Contains(chunks[c.x, c.z]))
                chunksToUpdate.Add(chunks[c.x, c.z]);

            count++;
            if(count > 200)
            {
                count = 0;
                yield return null;
            }
        }

        applyingModifications = false;
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
                        chunks[x, z] = new Chunk(coord, this, material, transparentMaterial, false);
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

    private bool IsChunkInWorld(ChunkCoord coord) => coord.x >= 0 && coord.x < VoxelData.WorldSizeInChunks && coord.z >= 0 && coord.z < VoxelData.WorldSizeInChunks;
    // private bool IsVoxelInWorld(Vector3 pos) => pos.x >= 0 && pos.x < VoxelData.WorldSizeInVoxels && pos.y >= 0 && pos.y < VoxelData.ChunkHeight && pos.z >= 0 && pos.z < VoxelData.WorldSizeInVoxels;
    #endregion
}

public class VoxelMode
{
    public Vector3 position;
    public byte id;

    public VoxelMode()
    {
        position = new Vector3();
        id = 0;
    }

    public VoxelMode (Vector3 position, byte id)
    {
        this.position = position;
        this.id = id;
    }
}