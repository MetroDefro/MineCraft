using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemSlot : MonoBehaviour
{
    public bool HasItem { get => stack != null; }

    public Image slotImage;
    public Image slotIcon;
    public ItemStack stack;
    public bool isCreative;

    [SerializeField] private TextMeshProUGUI slotAmount;

    public void UpdateSlot()
    {
        if (HasItem)
        {
            slotIcon.sprite = World.instance.VoxelType[(int)stack.id].icon;
            slotAmount.text = stack.amount.ToString();
            slotIcon.enabled = true;
            slotAmount.enabled = true;
        }
        else
            Clear();
    }
    public void EmptySlot()
    {
        stack = null;
        UpdateSlot();
    }

    public void Clear()
    {
        slotIcon.sprite = null;
        slotAmount.text = "";
        slotIcon.enabled = false;
        slotAmount.enabled = false;
    }

    public int Take(int amount)
    {
        if (amount > stack.amount)
        {
            int result = stack.amount;
            EmptySlot();
            return result;
        }
        else if (amount < stack.amount)
        {
            stack.amount -= amount;
            UpdateSlot();
            return amount;
        }
        else
        {
            EmptySlot();
            return amount;
        }
    }

    public ItemStack TakeAll()
    {
        ItemStack handOver = new ItemStack(stack.id, stack.amount);
        EmptySlot();
        return handOver;
    }

    public ItemSlot InsertStack(ItemStack stack)
    {
        this.stack = stack;
        UpdateSlot();

        return this;
    }
}