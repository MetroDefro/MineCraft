using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragAnDropHandler : MonoBehaviour
{
    [SerializeField] private ItemSlot cursorSlot = null;
    [SerializeField] private GraphicRaycaster raycaster = null;
    [SerializeField] private EventSystem eventSystem = null;

    private PointerEventData pointerEventData;


    private void Update()
    {
        if (!World.instance.InUI)
            return;

        cursorSlot.transform.position = Input.mousePosition;

        if (Input.GetMouseButtonDown(0))
        {
            HandleSlotClick(CheckForSlot());
        }
    }

    private void HandleSlotClick(ItemSlot clickedSlot)
    {
        if (clickedSlot == null)
            return;

        if (clickedSlot.isCreative)
        {
            cursorSlot.EmptySlot();
            cursorSlot.InsertStack(clickedSlot.stack);
        }

        if (!cursorSlot.HasItem)
        {
            if (clickedSlot.HasItem)
                cursorSlot.InsertStack(clickedSlot.TakeAll());
        }
        else
        {
            if (clickedSlot.HasItem)
            {
                if (cursorSlot.stack.id != clickedSlot.stack.id)
                {
                    ItemStack oldCursorSlot = cursorSlot.TakeAll();
                    ItemStack oldSlot = clickedSlot.TakeAll();

                    clickedSlot.InsertStack(oldCursorSlot);
                    cursorSlot.InsertStack(oldSlot);
                }
            } 
            else
            {
                clickedSlot.InsertStack(cursorSlot.TakeAll());
            }
        }
    }

    private ItemSlot CheckForSlot()
    {
        pointerEventData = new PointerEventData(eventSystem);
        pointerEventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult> ();
        raycaster.Raycast(pointerEventData, results);

        foreach(RaycastResult result in results)
        {
            if (result.gameObject.TryGetComponent(out ItemSlot itemSlot))
                return itemSlot;
        }

        return null;
    }
}
