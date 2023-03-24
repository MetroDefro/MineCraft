using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreativeInventory : MonoBehaviour
{
    [SerializeField] private ItemSlot slotPrefab;

    private List<ItemSlot> slots = new List<ItemSlot>();

    void Start()
    {
        for(int i = 1; i < World.instance.BlockTypes.Length; i++)
        {
            ItemSlot slot = Instantiate(slotPrefab, transform).InsertStack(new ItemStack((byte)i, 64));
            slot.isCreative = true;
            slots.Add(slot);
        }
    }
}
