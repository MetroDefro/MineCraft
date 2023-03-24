using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIItemSlot : MonoBehaviour
{
    public bool isLinked = false;
    public ItemSlot itemSlot;
    public Image slotImage;
    public Image slotIcon;
    public TextMeshProUGUI slotAmount;

    World world;

    private void Awake()
    {
        world = FindObjectOfType<World>();
    }

    public bool HasItem 
    {
        get 
        {
            if(itemSlot == null)
                return false;
            else
                return itemSlot.HasItem;
        }
    }

    public void Link (ItemSlot itemSlot)
    {
        this.itemSlot = itemSlot;
        isLinked = true;
        itemSlot.LinkUISlot(this);
        UpdateSlot();
    }

    public void UnLink()
    {
        itemSlot.UnLinkUISlot();
        itemSlot = null;
        UpdateSlot();
    }

    public void UpdateSlot()
    {
        if (itemSlot != null && itemSlot.HasItem)
        {
            slotIcon.sprite = world.BlockTypes[itemSlot.stack.id].icon;
            slotAmount.text = itemSlot.stack.amount.ToString();
            slotIcon.enabled = true;
            slotAmount.enabled = true;
        }
        else
            Clear();
    }

    public void Clear()
    {
        slotIcon.sprite = null;
        slotAmount.text = "";
        slotIcon.enabled = false;
        slotAmount.enabled = false;
    }

    private void OnDestroy()
    {
        if (itemSlot != null)
            itemSlot.UnLinkUISlot();
    }
}

public class ItemSlot
{
    public bool HasItem { get => stack != null; }

    public ItemStack stack;
    public bool isCreative;
    private UIItemSlot uiItemSlot = null;

    public ItemSlot(UIItemSlot uiItemSlot)
    {
        stack = null;
        this.uiItemSlot = uiItemSlot;
        uiItemSlot.Link(this);
    }

    public ItemSlot(UIItemSlot uiItemSlot, ItemStack stack)
    {
        this.stack = stack;
        this.uiItemSlot = uiItemSlot;
        uiItemSlot.Link(this);
    }

    public void LinkUISlot(UIItemSlot uiSlot)
    {
        uiItemSlot = uiSlot;
    }
    
    public void UnLinkUISlot()
    {
        uiItemSlot = null;
    }

    public void EmptySlot()
    {
        stack = null;
        if (uiItemSlot != null)
            uiItemSlot.UpdateSlot();
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
            uiItemSlot.UpdateSlot();
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

    public void InsertStack(ItemStack stack)
    {
        this.stack = stack;
        uiItemSlot.UpdateSlot();
    }

    public void UnLockUISlot()
    {
        uiItemSlot = null;
    }

}