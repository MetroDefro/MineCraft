using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BlockType", menuName = "MinecraftTutorial/Block Type")]
public class BlockType : ScriptableObject
{
    public string blockName;
    public bool isSolid;
    public bool isTransparent;
    public Sprite icon;

    [Header("Texture Values")]
    public int backFaceTexture;
    public int frontFaceTexture;
    public int topFaceTexture;
    public int bottomFaceTexture;
    public int leftFaceTexture;
    public int rightFaceTexture;

    private enum FACE
    {
        back = 0,
        front = 1,
        top = 2,
        bottom = 3,
        left = 4,
        right = 5,
    }

    public int GetTextureID(int faceIndex)
    {
        switch (faceIndex)
        {
            case (int)FACE.back:
                return backFaceTexture;
            case (int)FACE.front:
                return frontFaceTexture;
            case (int)FACE.top:
                return topFaceTexture;
            case (int)FACE.bottom:
                return bottomFaceTexture;
            case (int)FACE.left:
                return leftFaceTexture;
            case (int)FACE.right:
                return rightFaceTexture;
            default:
                Debug.Log("Error in GetTextureID; invalid face index");
                return 0;
        }
    }
}

public enum BLOCK_TYPE_ID
{
    Air = 0,
    bedrock = 1,
    Stone = 2,
    Soil = 3,
    Send = 4,
    Dirt = 5,
}