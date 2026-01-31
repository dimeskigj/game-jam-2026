using Godot;

public enum ItemType { Generic, Mask }
public enum MaskEffect { None, XRay, Gas, Invisibility }

public partial class InventoryItem : Resource
{
    [Export]
    public string Name { get; set; } = "Item";
    [Export]
    public Texture2D Icon { get; set; }
    [Export]
    public bool Stackable { get; set; } = true;
    [Export]
    public int MaxStack { get; set; } = 64;
    [Export]
    public bool Usable { get; set; } = false;
    
    [Export]
    public ItemType Type { get; set; } = ItemType.Generic;
    [Export]
    public MaskEffect Effect { get; set; } = MaskEffect.None;
	
	[Export]
	public PackedScene PickupScene { get; set; }

    public int CurrentStack { get; set; } = 1;

    // Helper to clone item for inventory
    public InventoryItem Clone()
    {
        InventoryItem newItem = (InventoryItem)this.Duplicate();
        newItem.CurrentStack = 1;
        return newItem;
    }
}
