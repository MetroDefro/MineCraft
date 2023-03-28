using System.Collections.Generic;
using UnityEngine;

public class Chunk
{
    public Queue<VoxelMode> modifications = new Queue<VoxelMode>();
    public Vector3 Position;

    private GameObject chunkObject;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;

    private ChunkCoord Coord;
    private int vertexIndex = 0;
    private VoxelState[,,] voxelMapBlockTypes = new VoxelState[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector2> uvs = new List<Vector2>();
    private List<Color> colors = new List<Color>();

    private bool isActive;
    private bool isVoxelMapPopulated = false;

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

    public bool IsEditable { get => isVoxelMapPopulated;  }

    public Chunk(ChunkCoord coord, Material material, bool generateOnLoad)
    {
        this.Coord = coord;
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
        chunkObject.transform.SetParent(World.instance.transform);
        chunkObject.transform.position = new Vector3(Coord.x * VoxelData.ChunkWidth, 0f, Coord.z * VoxelData.ChunkWidth);
        chunkObject.name = "Chunk " + Coord.x + ", " + Coord.z;

        Position = chunkObject.transform.position;

        PopulateVoxelMap();
    }

    public VoxelState GetVoxelFromGlobalVector3(Vector3 pos)
    {
        int xCheck = Mathf.FloorToInt(pos.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z);

        xCheck -= Mathf.FloorToInt(Position.x);
        zCheck -= Mathf.FloorToInt(Position.z);

        return voxelMapBlockTypes[xCheck, yCheck, zCheck];
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
                    voxelMapBlockTypes[x, y, z] = new VoxelState(World.instance.GetVoxelBlockType(new Vector3(x, y, z) + Position));
                }
            }
        }

        UpdateChunk();
        isVoxelMapPopulated = true;
    }

    public void UpdateChunk()
    {
        // If there is something to be modified, set up the voxel block type of the that's position as the id.
        while (modifications.Count > 0)
        {
            VoxelMode v = modifications.Dequeue();
            Vector3 pos = v.position -= Position;
            voxelMapBlockTypes[(int)pos.x, (int)pos.y, (int)pos.z].id = v.id;
        }

        ClearMeshData();
        CalculateLight();

        for (int y = 0; y < VoxelData.ChunkHeight; y++)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                {
                    if (World.instance.BlockTypes[voxelMapBlockTypes[x, y, z].id].isSolid)
                        UpdateMeshData(new Vector3(x, y, z));
                }
            }
        }

        World.instance.chunksToDraw.Enqueue(this);

    }

    private void CalculateLight()
    {
        Queue<Vector3Int> litVoxels = new Queue<Vector3Int>();

        for (int x = 0; x < VoxelData.ChunkWidth; x++)
        {
            for (int z = 0; z < VoxelData.ChunkWidth; z++)
            {
                float lightRay = 1f;

                for(int y = VoxelData.ChunkHeight - 1; y >= 0; y--)
                {
                    VoxelState thisVoxel = voxelMapBlockTypes[x, y, z];

                    if (thisVoxel.id > 0 && World.instance.BlockTypes[thisVoxel.id].transparency < lightRay)
                        lightRay = World.instance.BlockTypes[thisVoxel.id].transparency;

                    thisVoxel.globalLightPercent = lightRay;

                    voxelMapBlockTypes[x, y, z] = thisVoxel;

                    if(lightRay > VoxelData.lightFalloff)
                        litVoxels.Enqueue(new Vector3Int(x, y, z));
                }
            }
        }

        while(litVoxels.Count > 0)
        {
            Vector3Int v = litVoxels.Dequeue();

            for(int p = 0; p < 6; p++)
            {
                Vector3 currentVoxel = v + VoxelData.FaceChecks[p];
                Vector3Int neighbor = new Vector3Int((int)currentVoxel.x, (int)currentVoxel.y, (int)currentVoxel.z);

                if (IsVoxelInChunk(neighbor.x, neighbor.y, neighbor.z))
                {
                    if (voxelMapBlockTypes[neighbor.x, neighbor.y, neighbor.z].globalLightPercent < voxelMapBlockTypes[v.x, v.y, v.z].globalLightPercent - VoxelData.lightFalloff)
                    {
                        voxelMapBlockTypes[neighbor.x, neighbor.y, neighbor.z].globalLightPercent = voxelMapBlockTypes[v.x, v.y, v.z].globalLightPercent - VoxelData.lightFalloff;

                        if(voxelMapBlockTypes[neighbor.x, neighbor.y, neighbor.z].globalLightPercent > VoxelData.lightFalloff)
                            litVoxels.Enqueue(neighbor);
                    }
                }
            }
        }
    }

    private void ClearMeshData()
    {
        vertexIndex = 0;
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();
        colors.Clear();
    }

    private void UpdateMeshData(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        byte blockID = voxelMapBlockTypes[x, y, z].id;
        
        for (int p = 0; p < 6; p++)
        {
            VoxelState neigbor = CheckVoxel(pos + VoxelData.FaceChecks[p]);
            // If the face - 1 voxel is not transparent, there is no need to draw it.
            if (neigbor != null && World.instance.BlockTypes[neigbor.id].renderNeighborFaces)
            {
                vertices.Add(pos + VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 0]]);
                vertices.Add(pos + VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 1]]);
                vertices.Add(pos + VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 2]]);
                vertices.Add(pos + VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 3]]);

                AddTexture(World.instance.BlockTypes[blockID].GetTextureID(p));

                float lightLevel = neigbor.globalLightPercent;


                colors.Add(new Color(0, 0, 0, lightLevel));
                colors.Add(new Color(0, 0, 0, lightLevel));
                colors.Add(new Color(0, 0, 0, lightLevel));
                colors.Add(new Color(0, 0, 0, lightLevel));

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

    public void CreateMesh()
    {
        Mesh mesh = new Mesh() 
        {
            vertices = vertices.ToArray(),
            subMeshCount = 2,
            triangles = triangles.ToArray(),
            uv = uvs.ToArray(),
            colors = colors.ToArray()
        };

        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
    }

    private bool IsVoxelInChunk(int x, int y, int z) => x >= 0 && x < VoxelData.ChunkWidth && y >= 0 && y < VoxelData.ChunkHeight && z >= 0 && z < VoxelData.ChunkWidth;

    public void EditVoxel(Vector3 pos, byte newID)
    {
        int xCheck = Mathf.FloorToInt(pos.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z);

        xCheck -= Mathf.FloorToInt(chunkObject.transform.position.x);
        zCheck -= Mathf.FloorToInt(chunkObject.transform.position.z);

        voxelMapBlockTypes[xCheck, yCheck, zCheck].id = newID;

        UpdateSurroundingVoxels(xCheck, yCheck, zCheck);

        UpdateChunk();
    }

    private void UpdateSurroundingVoxels(int x, int y, int z)
    {
        Vector3 thisVoxel = new Vector3(x, y, z);

        for (int p = 0; p < 6; p++)
        {
            Vector3 currentVoxel = thisVoxel + VoxelData.FaceChecks[p];

            // When the added voxel affects other chunks, that chunk also needs to be updated.
            if (!IsVoxelInChunk((int)currentVoxel.x, (int)currentVoxel.y, (int)currentVoxel.z))
            {
                World.instance.GetChunkFromVector3(currentVoxel + Position).UpdateChunk();
            }
        }
    }

    private VoxelState CheckVoxel(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        if (!IsVoxelInChunk(x, y, z))
            return World.instance.GetVoxelState(pos + Position);

        return voxelMapBlockTypes[x, y, z];
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

public class VoxelState
{
    public byte id;
    public float globalLightPercent;

    public VoxelState()
    {
        id = 0;
        globalLightPercent = 0f;
    }

    public VoxelState (byte id)
    {
        this.id = id;
    }
}