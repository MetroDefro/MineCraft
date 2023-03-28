using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

public class World : MonoBehaviour
{
    public static World instance;

    [Range(0f, 1f)]
    public float GlobalLightingLevel;
    public Color Day;
    public Color Night;

    public Transform PlayerTransform;
    public ChunkCoord PlayerChunkCoord;
    public GameObject creativeInventoryWindow;
    public GameObject cursorSlot;
    public object ChunkUpdateThreadLock = new object();

    public BlockType[] BlockTypes => blockTypes;
    public Queue<Chunk> chunksToDraw = new Queue<Chunk>();
    public List<Chunk> chunksToUpdate = new List<Chunk>();

    public bool InUI
    {
        get => inUI;
        set
        {
            inUI = value;
            creativeInventoryWindow.SetActive(inUI);
            cursorSlot.SetActive(inUI);
            Cursor.lockState = inUI ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }

    [Header("World Generation Values")]
    [SerializeField] private int Seed;
    [SerializeField] private BiomeAttributes biome;

    [Header("Performance")]
    [SerializeField] private bool enableThreading;

    [SerializeField] private Material material;
    [SerializeField] private GameObject DebugScreen;
    [SerializeField] private BlockType[] blockTypes;

    private Camera mainCamera;
    private Vector3 spawnPosition;
    private ChunkCoord playerLastChunkCoord;
    private Thread chunkUpdateThread;

    private Chunk[,] chunks = new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];
    private List<ChunkCoord> currentActiveChunks = new List<ChunkCoord>();
    private List<ChunkCoord> chunksToCreate = new List<ChunkCoord>();
    private Queue<Queue<VoxelMode>> modifications = new Queue<Queue<VoxelMode>>();

    private bool applyingModifications = false;
    private bool inUI = false;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(this.gameObject);
        }

        instance = this;
    }

    private void Start()
    {
        Random.InitState(Seed);
        mainCamera = Camera.main;

        Shader.SetGlobalFloat("minGlobalLightLevel", VoxelData.minLightLevel);
        Shader.SetGlobalFloat("maxGlobalLightLevel", VoxelData.maxLightLevel);

        if (enableThreading)
        {
            chunkUpdateThread = new Thread(new ThreadStart(ThreadedUpdate));
            chunkUpdateThread.Start();
        }

        spawnPosition = new Vector3((VoxelData.WorldSizeInChunks * VoxelData.ChunkWidth) / 2f, VoxelData.ChunkHeight - 40f, (VoxelData.WorldSizeInChunks * VoxelData.ChunkWidth) / 2f);
        PlayerTransform.position = spawnPosition;
        playerLastChunkCoord = PlayerChunkCoord = GetChunkCoordFromVector3(PlayerTransform.position);

        GenerateWorld();
    }

    private void Update()
    {
        PlayerChunkCoord = GetChunkCoordFromVector3(PlayerTransform.position);

        Shader.SetGlobalFloat("GlobalLightLevel", GlobalLightingLevel);
        mainCamera.backgroundColor = Color.Lerp(Night, Day, GlobalLightingLevel);

        if (!PlayerChunkCoord.Equals(playerLastChunkCoord))
            CheckViewDistance();

        if (chunksToCreate.Count > 0)
            CreateChunk();

        if (chunksToDraw.Count > 0)
        {
            if (chunksToDraw.Peek().IsEditable)
                chunksToDraw.Dequeue().CreateMesh();
        }

        if (!enableThreading)
        {
            if (!applyingModifications)
                ApplyModifications();

            if (chunksToUpdate.Count > 0)
                UpdateChunks();
        }

        if (Input.GetKeyDown(KeyCode.F3))
            DebugScreen.SetActive(!DebugScreen.activeSelf);
    }

    private void OnDisable()
    {
        if (enableThreading)
        {
            chunkUpdateThread.Abort();
        }
    }
    #region public method

    public bool CheckForVoxel(Vector3 pos)
    {
        ChunkCoord thisChunkCoord = new ChunkCoord(pos);

        if (!IsChunkInWorld(thisChunkCoord) || pos.y < 0 || pos.y > VoxelData.ChunkHeight)
            return false;

        if (chunks[thisChunkCoord.x, thisChunkCoord.z] != null && chunks[thisChunkCoord.x, thisChunkCoord.z].IsEditable)
            return blockTypes[chunks[thisChunkCoord.x, thisChunkCoord.z].GetVoxelFromGlobalVector3(pos).id].isSolid;

        return blockTypes[GetVoxelBlockType(pos)].isSolid;
    }

    public VoxelState GetVoxelState(Vector3 pos)
    {
        ChunkCoord thisChunkCoord = new ChunkCoord(pos);

        // When a player creating a block in-game
        if (!IsChunkInWorld(thisChunkCoord) || pos.y < 0 || pos.y > VoxelData.ChunkHeight)
            return null;

        if (chunks[thisChunkCoord.x, thisChunkCoord.z] != null && chunks[thisChunkCoord.x, thisChunkCoord.z].IsEditable)
            return chunks[thisChunkCoord.x, thisChunkCoord.z].GetVoxelFromGlobalVector3(pos);

        return new VoxelState(GetVoxelBlockType(pos));
    }

    public byte GetVoxelBlockType(Vector3 pos)
    {
        int yPos = (Mathf.FloorToInt(pos.y));
        byte voxelValue = (byte)BLOCK_TYPE_ID.Air;

        // IMMUTABLE PASS
        // If outside world, return air
        // probably nothing
        if (!IsVoxelInWorld(pos))
            voxelValue = (byte)BLOCK_TYPE_ID.Air;

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
        if (yPos == terrainHeight)
        {
            if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treeZoneScale) > biome.treeZoneThreshold)
            {
                if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treePlacementScale) > biome.treePlacementThreshold)
                {
                    modifications.Enqueue(Structure.MakeTree(pos, biome.minTreeHeight, biome.maxTreeHeight));
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
            for (int z = chunkStartPosition; z < chunkEndPosition; z++)
            {
                ChunkCoord coord = new ChunkCoord(x, z);
                chunks[x, z] = new Chunk(coord);
                chunksToCreate.Add(coord);
            }
        }

        CheckViewDistance();
    }

    private void CreateChunk()
    {
        ChunkCoord c = chunksToCreate[0];
        chunksToCreate.RemoveAt(0);
        chunks[c.x, c.z].Init(material);
    }

    private void UpdateChunks()
    {
        bool updated = false;
        int index = 0;

        lock (ChunkUpdateThreadLock)
        {
            // At 1frane, Of the chunks that need to be updated, we will only update the first chunk that has been created.
            while (!updated && index < chunksToUpdate.Count - 1)
            {
                if (chunksToUpdate[index].IsEditable)
                {
                    chunksToUpdate[index].UpdateChunk();
                    currentActiveChunks.Add(chunksToUpdate[index].Coord);
                    chunksToUpdate.RemoveAt(index);
                    updated = true;
                }
                else
                    index++;
            }
        }
    }

    private void ThreadedUpdate()
    {
        while (true)
        {
            if (!applyingModifications)
                ApplyModifications();

            if (chunksToUpdate.Count > 0)
                UpdateChunks();
        }
    }

    private void ApplyModifications()
    {
        applyingModifications = true;

        while (modifications.Count > 0)
        {
            Queue<VoxelMode> queue = modifications.Dequeue();
            
            while (queue!= null && queue.Count > 0)
            {
                VoxelMode v = queue.Dequeue();

                ChunkCoord c = GetChunkCoordFromVector3(v.position);

                if (chunks[c.x, c.z] == null)
                {
                    chunks[c.x, c.z] = new Chunk(c);
                    chunksToCreate.Add(c);
                }

                chunks[c.x, c.z].modifications.Enqueue(v);
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

        currentActiveChunks.Clear();

        for (int x = playerCoord.x - VoxelData.ViewDistanceInChunks; x < playerCoord.x + VoxelData.ViewDistanceInChunks; x++)
        {
            for (int z = playerCoord.z - VoxelData.ViewDistanceInChunks; z < playerCoord.z + VoxelData.ViewDistanceInChunks; z++)
            {
                ChunkCoord coord = new ChunkCoord(x, z);
                if (IsChunkInWorld(coord))
                {
                    if (chunks[x, z] == null)
                    {
                        chunks[x, z] = new Chunk(coord);
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
    private bool IsVoxelInWorld(Vector3 pos) => pos.x >= 0 && pos.x < VoxelData.WorldSizeInVoxels && pos.y >= 0 && pos.y < VoxelData.ChunkHeight && pos.z >= 0 && pos.z < VoxelData.WorldSizeInVoxels;
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