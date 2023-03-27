using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Toolbar : MonoBehaviour
{
    public int SlotIndex = 0;
    public ItemSlot[] Slots;

    [SerializeField] private RectTransform highlight;

    private void Update()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll != 0)
        {
            if (scroll > 0)
                SlotIndex--;
            else
                SlotIndex++;

            if (SlotIndex > Slots.Length - 1)
                SlotIndex = 0;
            if (SlotIndex < 0)
                SlotIndex = Slots.Length - 1;

            highlight.position = Slots[SlotIndex].slotIcon.transform.position;
        }
    }

    public void Initialize()
    {
        byte index = 1;
        foreach (ItemSlot s in Slots)
        {
            ItemStack stack = new ItemStack(index, Random.Range(2, 65));
            s.InsertStack(stack);
            index++;
        }
    }
}
