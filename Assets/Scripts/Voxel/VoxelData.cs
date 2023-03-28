using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelData : MonoBehaviour
{
    public static readonly int ChunkWidth = 16;
    public static readonly int ChunkHeight = 128;
    public static readonly int WorldSizeInChunks = 100;

    // Lighting Value
    public static float minLightLevel = 0.15f;
    public static float maxLightLevel = 0.8f;
    public static float lightFalloff = 0.08f;

    public static int WorldSizeInVoxels { get => WorldSizeInChunks * ChunkWidth; }

    public static readonly int ViewDistanceInChunks = 5;

    public static readonly int TextureAtlassWidth = 16;
    public static readonly int TextureAtlassHeight = 16;

    public static float NormalizedTextureAtlassWidth { get => 1f / (float) TextureAtlassWidth; }    
    public static float NormalizedTextureAtlassHeight { get => 1f / (float)TextureAtlassHeight; }

    public static readonly Vector3Int[] VoxelVerts = new Vector3Int[8]
    {
        new Vector3Int(0, 0, 0),
        new Vector3Int(1, 0, 0),
        new Vector3Int(1, 1, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, 0, 1),
        new Vector3Int(1, 0, 1),
        new Vector3Int(1, 1, 1),
        new Vector3Int(0, 1, 1),
    };

    public static readonly Vector3Int[] FaceChecks = new Vector3Int[6]
    {
        new Vector3Int(0, 0, -1),
        new Vector3Int(0, 0, 1),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(1, 0, 0),
    };

    public static readonly int[,] VoxelTris = new int[6, 4]
    {
        // 0 1 2 2 1 3
        {0, 3, 1, 2 }, // Back Face
        {5, 6, 4, 7 }, // Front Face
        {3, 7, 2, 6 }, // Top Face
        {1, 5, 0, 4 }, // Bottom Face
        {4, 7, 0, 3 }, // Left Face
        {1, 2, 5, 6} // Right Face
    };

    public static readonly Vector2[] VoxelUvs = new Vector2[4]
    {
        new Vector2(0.0f, 0.0f),
        new Vector2(0.0f, 1.0f),
        new Vector2(1.0f, 0.0f),
        new Vector2(1.0f, 1.0f),
    };

    public static bool IsChunkInWorld(ChunkCoord coord) => coord.x >= 0 && coord.x < WorldSizeInChunks && coord.z >= 0 && coord.z < WorldSizeInChunks;
    public static bool IsVoxelInWorld(Vector3 pos) => pos.x >= 0 && pos.x < WorldSizeInVoxels && pos.y >= 0 && pos.y < ChunkHeight && pos.z >= 0 && pos.z < WorldSizeInVoxels;
    public static bool IsVoxelInChunk(int x, int y, int z) => x >= 0 && x < ChunkWidth && y >= 0 && y < ChunkHeight && z >= 0 && z < ChunkWidth;

}
