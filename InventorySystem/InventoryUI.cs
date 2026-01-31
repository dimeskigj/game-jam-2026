using Godot;
using System.Collections.Generic;

public partial class InventoryUI : CanvasLayer
{
    private GridContainer _gridContainer;
    private Inventory _inventory;
    private List<Label> _labels = new List<Label>();
    private List<ColorRect> _backgrounds = new List<ColorRect>();

    public override void _Ready()
    {
        _gridContainer = GetNode<GridContainer>("Control/GridContainer");

        // Initialize UI slots
        foreach (Node child in _gridContainer.GetChildren())
        {
            if (child is ColorRect bg)
            {
                _backgrounds.Add(bg);
                _labels.Add(bg.GetNode<Label>("Label"));
            }
        }
    }

    public void SetInventory(Inventory inventory)
    {
        _inventory = inventory;
        if (_inventory != null)
        {
            _inventory.InventoryUpdated += UpdateUI;
            _inventory.SelectedSlotChanged += UpdateSelection;
            UpdateUI();
            UpdateSelection(0);
        }
    }

    private void UpdateUI()
    {
        // Guard against null inventory if called prematurely
        if (_inventory == null) return;

        for (int i = 0; i < Inventory.MaxSlots; i++)
        {
            if (i < _labels.Count)
            {
                InventoryItem item = _inventory.items[i];
                if (item != null)
                {
                    _labels[i].Text = $"{item.Name} ({item.CurrentStack})";
                }
                else
                {
                    _labels[i].Text = "";
                }
            }
        }
    }

    private void UpdateSelection(int slot)
    {
        for (int i = 0; i < _backgrounds.Count; i++)
        {
            if (i == slot)
            {
                _backgrounds[i].Color = new Color(0.5f, 0.5f, 0.5f, 0.8f); // Selected
            }
            else
            {
                _backgrounds[i].Color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Unselected
            }
        }
    }
}
