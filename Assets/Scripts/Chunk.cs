using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk
{
    public bool isVoxelMapPopulated = false;

    private GameObject chunkObject;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;

    private ChunkCoord Coord;
    private int vertexIndex = 0;
    private byte[,,] VoxelMap = new byte[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector2> uvs = new List<Vector2>();

    private World world;

    private bool isActive;

    public bool IsActive
    {
        get { return isActive; }
        set 
        {
            isActive = value;
            if(chunkObject != null)
                chunkObject.SetActive(value); 
        }
    }

    public Vector3 position { get => chunkObject.transform.position; }

    public Chunk(ChunkCoord coord, World world, Material material, bool generateOnLoad)
    {
        this.Coord = coord;
        this.world = world;
        isActive = true;

        if(generateOnLoad)
            Init(material);
    }

    public void Init(Material material)
    {
        chunkObject = new GameObject();
        meshRenderer = chunkObject.AddComponent<MeshRenderer>();
        meshFilter = chunkObject.AddComponent<MeshFilter>();

        meshRenderer.material = material;
        chunkObject.transform.SetParent(world.transform);
        chunkObject.transform.position = new Vector3(Coord.x * VoxelData.ChunkWidth, 0f, Coord.z * VoxelData.ChunkWidth);
        chunkObject.name = "Chunk " + Coord.x + ", " + Coord.z;

        PopulateVoxelMap();
        CreateChunkMesh();
        CreateMesh();
    }

    public byte GetVoxelFromGlobalVector3(Vector3 pos)
    {
        int xCheck = Mathf.FloorToInt(pos.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z);

        xCheck -= Mathf.FloorToInt(chunkObject.transform.position.x);
        zCheck -= Mathf.FloorToInt(chunkObject.transform.position.z);

        return VoxelMap[xCheck, yCheck, zCheck];
    }

    #region private method
    private void PopulateVoxelMap()
    {
        for (int y = 0; y < VoxelData.ChunkHeight; y++)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                {
                    // Get Block Type
                    VoxelMap[x, y, z] = world.GetVoxel(new Vector3(x, y, z) + position);
                }
            }
        }

        isVoxelMapPopulated = true;
    }

    private void CreateChunkMesh()
    {
        for (int y = 0; y < VoxelData.ChunkHeight; y++)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                {
                    if (world.BlockTypes[VoxelMap[x, y, z]].isSolid)
                        AddVoxelDataToChunk(new Vector3(x, y, z));
                }
            }
        }
    }

    private void AddVoxelDataToChunk(Vector3 pos)
    {
        for (int p = 0; p < 6; p++)
        {
            if (!CheckVoxel(pos + VoxelData.FaceChecks[p]))
            {
                byte blockID = VoxelMap[(int)pos.x, (int)pos.y, (int)pos.z];
                for (int i = 0; i < 4; i++)
                    vertices.Add(pos + VoxelData.VoxelVerts[VoxelData.VoxelTris[p, i]]);

                AddTexture(world.BlockTypes[blockID].GetTextureID(p));

                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 3);
                vertexIndex += 4;
            }
        }
    }

    private void CreateMesh()
    {
        Mesh mesh = new Mesh
        {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray(),
            uv = uvs.ToArray()
        };

        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
    }

    private bool IsVoxelInChunk(int x, int y, int z) => x > 0 && x < VoxelData.ChunkWidth -1 && y > 0 && y < VoxelData.ChunkHeight - 1 && z > 0 && z < VoxelData.ChunkWidth - 1;

    private bool CheckVoxel(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        // if voxel not in chunk
        if (!IsVoxelInChunk(x, y, z))
            return world.CheckForVoxel(pos + position);

        return world.BlockTypes[VoxelMap[x, y, z]].isSolid;
    }

    private void AddTexture(int textureID)
    {
        float y = textureID / VoxelData.TextureAtlassWidth;
        float x = textureID - (y * VoxelData.TextureAtlassWidth);

        x *= VoxelData.NormalizedTextureAtlassWidth;
        y *= VoxelData.NormalizedTextureAtlassHeight;

        y = 1f - y - VoxelData.NormalizedTextureAtlassHeight;

        uvs.Add(new Vector2(x, y));
        uvs.Add(new Vector2(x, y + VoxelData.NormalizedTextureAtlassHeight));
        uvs.Add(new Vector2(x + VoxelData.NormalizedTextureAtlassWidth, y));
        uvs.Add(new Vector2(x + VoxelData.NormalizedTextureAtlassWidth, y + VoxelData.NormalizedTextureAtlassHeight));
    }

    #endregion
}

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
        int xCheck = Mathf.FloorToInt(pos.x);
        int zCheck = Mathf.FloorToInt(pos.z);

        x = xCheck / VoxelData.ChunkWidth;
        z = zCheck / VoxelData.ChunkWidth;
    }

    public bool Equals (ChunkCoord other)
    {
        if (other == null)
            return false;
        else if (other.x == x && other.z == z)
            return true;
        else
            return false;
    }
}