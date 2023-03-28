using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Linq;

public class World : MonoBehaviour
{
    public static World instance;

    [Range(0f, 1f)]
    public float GlobalLightingLevel;
    public Color Day;
    public Color Night;

    public Transform PlayerTransform;
    public ChunkCoord PlayerChunkCoord;
    public object ChunkUpdateThreadLock = new object();

    public VoxelType[] VoxelType => voxelType;
    public Queue<Chunk> chunksToDraw = new Queue<Chunk>();
    public List<Chunk> chunksToUpdate = new List<Chunk>();

    public bool InUI { get => inUI; set => Cursor.lockState = (inUI = value) ? CursorLockMode.None : CursorLockMode.Locked; }

    [Header("World Generation Values")]
    [SerializeField] private int Seed;
    [SerializeField] private BiomeAttributes biome;

    [Header("Performance")]
    [SerializeField] private bool enableThreading;

    [SerializeField] private Material material;
    [SerializeField] private VoxelType[] voxelType;

    private Camera mainCamera;
    private ChunkCoord playerLastChunkCoord;
    private Thread chunkUpdateThread;

    private Chunk[,] chunks = new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];
    private List<Chunk> currentActiveChunks = new List<Chunk>();
    private List<Chunk> chunksToCreate = new List<Chunk>();
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

        PlayerTransform.position = new Vector3((VoxelData.WorldSizeInChunks * VoxelData.ChunkWidth) / 2f, VoxelData.ChunkHeight - 40f, (VoxelData.WorldSizeInChunks * VoxelData.ChunkWidth) / 2f);
        playerLastChunkCoord = PlayerChunkCoord = new ChunkCoord(PlayerTransform.position);

        voxelType.OrderBy(o => o.id);

        GenerateWorld();
    }

    private void Update()
    {
        PlayerChunkCoord = new ChunkCoord(PlayerTransform.position);

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
    }

    private void OnDisable()
    {
        if (enableThreading)
        {
            chunkUpdateThread.Abort();
        }
    }

    #region public method

    public bool CheckForVoxel(Vector3Int pos)
    {
        ChunkCoord thisChunkCoord = new ChunkCoord(pos);

        if (!VoxelData.IsChunkInWorld(thisChunkCoord) || pos.y < 0 || pos.y > VoxelData.ChunkHeight)
            return false;

        if (chunks[thisChunkCoord.x, thisChunkCoord.z] != null && chunks[thisChunkCoord.x, thisChunkCoord.z].IsEditable)
            return voxelType[(int)chunks[thisChunkCoord.x, thisChunkCoord.z].GetVoxelFromGlobalVector3(pos).id].isSolid;

        return voxelType[(int)GetVoxelType(pos)].isSolid;
    }

    public VoxelState GetVoxelState(Vector3Int pos)
    {
        ChunkCoord thisChunkCoord = new ChunkCoord(pos);

        // When a player creating a block in-game
        if (!VoxelData.IsChunkInWorld(thisChunkCoord) || pos.y < 0 || pos.y > VoxelData.ChunkHeight)
            return null;

        if (chunks[thisChunkCoord.x, thisChunkCoord.z] != null && chunks[thisChunkCoord.x, thisChunkCoord.z].IsEditable)
            return chunks[thisChunkCoord.x, thisChunkCoord.z].GetVoxelFromGlobalVector3(pos);

        return new VoxelState(GetVoxelType(pos));
    }

    public BLOCK_TYPE_ID GetVoxelType(Vector3Int pos)
    {
        int yPos = (Mathf.FloorToInt(pos.y));
        BLOCK_TYPE_ID voxelValue = BLOCK_TYPE_ID.Air;

        // IMMUTABLE PASS
        // If outside world, return air
        if (!VoxelData.IsVoxelInWorld(pos))
            voxelValue = (byte)BLOCK_TYPE_ID.Air;

        // if bottom block of chunk, return bedrock.
        if (yPos == 0)
            voxelValue = BLOCK_TYPE_ID.bedrock;


        // BASIC TERRAIN PASS
        int terrainHeight = Mathf.FloorToInt(VoxelData.ChunkHeight * Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.terrainScale)) + biome.solidGroundHeight;

        if (yPos > terrainHeight)
            voxelValue = BLOCK_TYPE_ID.Air;
        else if (yPos == terrainHeight)
            voxelValue = BLOCK_TYPE_ID.Soil;
        else if (yPos < terrainHeight && yPos > terrainHeight - 4)
            voxelValue = BLOCK_TYPE_ID.Dirt;
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

    public Chunk GetChunkFromVector3(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
        return chunks[x, z];
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
                chunksToCreate.Add(chunks[x, z] = new Chunk(new ChunkCoord(x, z)));
            }
        }

        CheckViewDistance();
    }

    private void CreateChunk()
    {
        Chunk chunk = chunksToCreate[0];
        chunksToCreate.RemoveAt(0);
        chunk.Init(material);
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
                    currentActiveChunks.Add(chunksToUpdate[index]);
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

                ChunkCoord c = new ChunkCoord(v.position);

                if (chunks[c.x, c.z] == null)
                {
                    chunksToCreate.Add(chunks[c.x, c.z] = new Chunk(c));
                }

                chunks[c.x, c.z].modifications.Enqueue(v);
            }
        }

        applyingModifications = false;
    }

    private void CheckViewDistance()
    {
        ChunkCoord playerCoord = new ChunkCoord(PlayerTransform.position);
        playerLastChunkCoord = PlayerChunkCoord;

        List<Chunk> previouslyActiveChunks = new List<Chunk>(currentActiveChunks);

        currentActiveChunks.Clear();

        for (int x = playerCoord.x - VoxelData.ViewDistanceInChunks; x < playerCoord.x + VoxelData.ViewDistanceInChunks; x++)
        {
            for (int z = playerCoord.z - VoxelData.ViewDistanceInChunks; z < playerCoord.z + VoxelData.ViewDistanceInChunks; z++)
            {
                ChunkCoord coord = new ChunkCoord(x, z);
                if (VoxelData.IsChunkInWorld(coord))
                {
                    if (chunks[x, z] == null)
                    {
                        chunks[x, z] = new Chunk(coord);
                        chunksToCreate.Add(chunks[x, z]);
                    }   
                    else if (!chunks[x, z].IsActive)
                    {
                        chunks[x, z].IsActive = true;
                    }
                    currentActiveChunks.Add(chunks[x, z]);
                }

                for (int i = 0; i < previouslyActiveChunks.Count; i++)
                {
                    if (previouslyActiveChunks[i].Coord.Equals(coord))
                        previouslyActiveChunks.RemoveAt(i);
                }
            }
        }

        foreach (Chunk c in previouslyActiveChunks)
            c.IsActive = false;
    }
    #endregion
}