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
		if (item != null && item.Usable)
		{
			GD.Print($"Used item: {item.Name}");
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
				// Unstackables might not be consumed? Or maybe they are. Assuming consumed for now if usable.
				items[selectedSlot] = null;
			}
			EmitSignal(SignalName.InventoryUpdated);
		}
		else
		{
			GD.Print("Nothing to use or item not usable.");
		}
	}
}
