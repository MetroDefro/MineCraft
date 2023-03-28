using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WorldUIPresenter : MonoBehaviour
{
    [SerializeField] private GameObject DebugScreen;

    [SerializeField] private ItemSlot cursorSlot = null;
    [SerializeField] private CreativeInventory creativeInventory;
    [SerializeField] private Toolbar toolbar;

    private void Start()
    {
        creativeInventory.Initialize();
        toolbar.Initialize();

        foreach (var slot in creativeInventory.Slots)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerDown;
            entry.callback.AddListener(_ => HandleSlotClick(slot));
            slot.gameObject.AddComponent<EventTrigger>().triggers.Add(entry);
        }

        foreach (var slot in toolbar.Slots)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerDown;
            entry.callback.AddListener(_ => HandleSlotClick(slot));
            slot.gameObject.AddComponent<EventTrigger>().triggers.Add(entry);
        }
    }

    private void Update()
    {
        if (!World.instance.InUI)
            return;

        cursorSlot.transform.position = Input.mousePosition;

        if (Input.GetKeyDown(KeyCode.F3))
            DebugScreen.SetActive(!DebugScreen.activeSelf);
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
}
