using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreativeInventory : MonoBehaviour
{
    [SerializeField] private ItemSlot slotPrefab;

    public List<ItemSlot> Slots = new List<ItemSlot>();

    public void Initialize()
    {
        for (int i = 1; i < World.instance.VoxelType.Length; i++)
        {
            ItemSlot slot = Instantiate(slotPrefab, transform).InsertStack(new ItemStack((BLOCK_TYPE_ID)i, 64));
            slot.isCreative = true;
            Slots.Add(slot);
        }
    }
}
