using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DebugScreen : MonoBehaviour
{
    private World world;
    [SerializeField] private TextMeshProUGUI text;

    private float frameRate;
    private float timer;

    private int halfWorldSizeInVoxels;
    private int halfWorldSizeInChunks;

    private void Start()
    {
        world = GameObject.FindObjectOfType<World>();

        halfWorldSizeInVoxels = VoxelData.WorldSizeInVoxels / 2;
        halfWorldSizeInChunks = VoxelData.WorldSizeInChunks / 2;
    }

    private void Update()
    {
        string debugText = "b3agz' code a Game Like Minecraft in Unity";
        debugText += "\n";
        debugText += frameRate + "fps";
        debugText += "\n\n";
        debugText += "XYZ: " + (Mathf.FloorToInt(world.player.position.x) - halfWorldSizeInVoxels) + " / " + Mathf.FloorToInt(world.player.position.y) + " / " + (Mathf.FloorToInt(world.player.position.z) - halfWorldSizeInVoxels);
        debugText += "\n";
        debugText += "Chunk: " + (world.playerChunkCoord.x - halfWorldSizeInChunks) + " / " + (world.playerChunkCoord.z - halfWorldSizeInChunks);

        text.text = debugText;

        if (timer > 1f)
        {
            frameRate = (int)(1f / Time.unscaledDeltaTime);
            timer = 0;
        }
        else
            timer += Time.deltaTime;
    }
}
