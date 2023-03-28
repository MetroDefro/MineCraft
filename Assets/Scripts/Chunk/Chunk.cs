using System.Collections.Generic;
using UnityEngine;

public class Chunk
{
    public Queue<VoxelMode> modifications = new Queue<VoxelMode>();
    public Vector3Int Position;
    public ChunkCoord Coord;

    private GameObject chunkObject;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;

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

    public Chunk(ChunkCoord coord)
    {
        this.Coord = coord;
        isActive = true;
    }

    #region public method

    public void Init(Material material)
    {
        chunkObject = new GameObject();
        meshRenderer = chunkObject.AddComponent<MeshRenderer>();
        meshFilter = chunkObject.AddComponent<MeshFilter>();

        meshRenderer.material = material;
        chunkObject.transform.SetParent(World.instance.transform);
        chunkObject.transform.position = new Vector3(Coord.x * VoxelData.ChunkWidth, 0f, Coord.z * VoxelData.ChunkWidth);
        chunkObject.name = "Chunk " + Coord.x + ", " + Coord.z;

        Position = new Vector3Int(Mathf.FloorToInt(chunkObject.transform.position.x), Mathf.FloorToInt(chunkObject.transform.position.y), Mathf.FloorToInt(chunkObject.transform.position.z));

        PopulateVoxelMap();
    }

    public VoxelState GetVoxelFromGlobalVector3(Vector3Int pos) => voxelMapBlockTypes[pos.x - Position.x, pos.y, pos.z - Position.z];

    public void EditVoxel(Vector3Int pos, BLOCK_TYPE_ID newID)
    {
        voxelMapBlockTypes[pos.x - Position.x, pos.y, pos.z - Position.z].id = newID;

        lock (World.instance.ChunkUpdateThreadLock)
        {
            World.instance.chunksToUpdate.Insert(0, this);
            UpdateSurroundingVoxels(pos.x - Position.x, pos.y, pos.z - Position.z);
        }
    }

    public void UpdateChunk()
    {
        // If there is something to be modified, set up the voxel block type of the that's position as the id.
        while (modifications.Count > 0)
        {
            VoxelMode v = modifications.Dequeue();
            Vector3Int pos = v.position -= Position;
            voxelMapBlockTypes[pos.x, pos.y, pos.z].id = v.id;
        }

        ClearMeshData();
        CalculateLight();

        for (int y = 0; y < VoxelData.ChunkHeight; y++)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                {
                    if (World.instance.VoxelType[(int)voxelMapBlockTypes[x, y, z].id].isSolid)
                        UpdateMeshData(new Vector3Int(x, y, z));
                }
            }
        }

        World.instance.chunksToDraw.Enqueue(this);

    }
    #endregion

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
                    voxelMapBlockTypes[x, y, z] = new VoxelState(World.instance.GetVoxelType(new Vector3Int(x, y, z) + Position));
                }
            }
        }

        isVoxelMapPopulated = true;

        lock (World.instance.ChunkUpdateThreadLock)
        {
            World.instance.chunksToUpdate.Add(this);
        }
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

                    if (thisVoxel.id > 0 && World.instance.VoxelType[(int)thisVoxel.id].transparency < lightRay)
                        lightRay = World.instance.VoxelType[(int)thisVoxel.id].transparency;

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
                Vector3Int neighbor = v + VoxelData.FaceChecks[p];

                if (VoxelData.IsVoxelInChunk(neighbor.x, neighbor.y, neighbor.z))
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

    private void UpdateMeshData(Vector3Int pos)
    {
        BLOCK_TYPE_ID blockID = voxelMapBlockTypes[pos.x, pos.y, pos.z].id;
        
        for (int p = 0; p < 6; p++)
        {
            VoxelState neigbor = CheckVoxel(pos + VoxelData.FaceChecks[p]);
            // If the face - 1 voxel is not transparent, there is no need to draw it.
            if (neigbor != null && World.instance.VoxelType[(int)neigbor.id].renderNeighborFaces)
            {
                vertices.Add(pos + VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 0]]);
                vertices.Add(pos + VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 1]]);
                vertices.Add(pos + VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 2]]);
                vertices.Add(pos + VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 3]]);

                AddTexture(World.instance.VoxelType[(int)blockID].GetTextureID(p));

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

    private void UpdateSurroundingVoxels(int x, int y, int z)
    {
        Vector3Int thisVoxel = new Vector3Int(x, y, z);

        for (int p = 0; p < 6; p++)
        {
            Vector3Int currentVoxel = thisVoxel + VoxelData.FaceChecks[p];

            // When the added voxel affects other chunks, that chunk also needs to be updated.
            if (!VoxelData.IsVoxelInChunk(currentVoxel.x, currentVoxel.y, currentVoxel.z))
            {
                World.instance.chunksToUpdate.Insert(0, World.instance.GetChunkFromVector3(currentVoxel + Position));
            }
        }
    }

    private VoxelState CheckVoxel(Vector3Int pos)
    {
        if (!VoxelData.IsVoxelInChunk(pos.x, pos.y, pos.z))
            return World.instance.GetVoxelState(pos + Position);

        return voxelMapBlockTypes[pos.x, pos.y, pos.z];
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