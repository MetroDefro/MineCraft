using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DebugScreen : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;

    private float frameRate;
    private float timer;

    private int halfWorldSizeInVoxels;
    private int halfWorldSizeInChunks;

    private void Start()
    {
        halfWorldSizeInVoxels = VoxelData.WorldSizeInVoxels / 2;
        halfWorldSizeInChunks = VoxelData.WorldSizeInChunks / 2;
    }

    private void Update()
    {
        string debugText = "b3agz' code a Game Like Minecraft in Unity";
        debugText += "\n";
        debugText += frameRate + "fps";
        debugText += "\n\n";
        debugText += "XYZ: " + (Mathf.FloorToInt(World.instance.PlayerTransform.position.x) - halfWorldSizeInVoxels) + " / " + Mathf.FloorToInt(World.instance.PlayerTransform.position.y) + " / " + (Mathf.FloorToInt(World.instance.PlayerTransform.position.z) - halfWorldSizeInVoxels);
        debugText += "\n";
        debugText += "Chunk: " + (World.instance.PlayerChunkCoord.x - halfWorldSizeInChunks) + " / " + (World.instance.PlayerChunkCoord.z - halfWorldSizeInChunks);

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
