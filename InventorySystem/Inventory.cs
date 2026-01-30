using Godot;
using System.Collections.Generic;

public partial class Inventory : Node
{
	public const int MaxSlots = 8;
	public InventoryItem[] items = new InventoryItem[MaxSlots];
	public int selectedSlot = 0;

	[Signal]
	public delegate void InventoryUpdatedEventHandler();
	[Signal]
	public delegate void SelectedSlotChangedEventHandler(int newSlot);
	[Signal]
	public delegate void ItemUsedEventHandler(InventoryItem item);

	public bool AddItem(InventoryItem newItem)
	{
		// Check for existing stack
		if (newItem.Stackable)
		{
			for (int i = 0; i < MaxSlots; i++)
			{
				if (items[i] != null && items[i].Name == newItem.Name && items[i].CurrentStack < items[i].MaxStack)
				{
					items[i].CurrentStack++;
					EmitSignal(SignalName.InventoryUpdated);
					return true;
				}
			}
		}

		// Find empty slot
		for (int i = 0; i < MaxSlots; i++)
		{
			if (items[i] == null)
			{
				items[i] = newItem.Clone();
				EmitSignal(SignalName.InventoryUpdated);
				return true;
			}
		}

		return false; // Inventory full
	}

	public void SelectSlot(int slotIndex)
	{
		if (slotIndex >= 0 && slotIndex < MaxSlots)
		{
			selectedSlot = slotIndex;
			EmitSignal(SignalName.SelectedSlotChanged, selectedSlot);
		}
	}

	public void UseCurrentItem()
	{
		InventoryItem item = items[selectedSlot];
		if (item != null)
		{
			// Emit signal for external handling (e.g. Player equipping mask, drinking potion)
			EmitSignal(SignalName.ItemUsed, item);

			if (item.Usable && item.Type != ItemType.Mask)
			{
				GD.Print($"Consumed item: {item.Name}");
				// Basic consumption logic
				if (item.Stackable)
				{
					item.CurrentStack--;
					if (item.CurrentStack <= 0)
					{
						items[selectedSlot] = null;
					}
				}
				else
				{
					items[selectedSlot] = null;
				}
				EmitSignal(SignalName.InventoryUpdated);
			}
		}
	}
}
