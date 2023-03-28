using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemStack
{
    public BLOCK_TYPE_ID id;
    public int amount;

    public ItemStack(BLOCK_TYPE_ID id, int amount)
    {
        this.id = id;
        this.amount = amount;
    }
}