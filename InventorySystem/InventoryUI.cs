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
        
        // Try to find the Player node first, then Ball as fallback
        Node root = GetTree().Root.GetNodeOrNull("Node3D");
        if (root != null)
        {
            Node playerNode = root.GetNodeOrNull("Player") ?? root.GetNodeOrNull("Ball");
            if (playerNode != null)
            {
                _inventory = playerNode.GetNodeOrNull<Inventory>("Inventory");
            }
        }

        if (_inventory != null)
        {
            _inventory.InventoryUpdated += UpdateUI;
            _inventory.SelectedSlotChanged += UpdateSelection;
        }
        else
        {
            GD.PrintErr("InventoryUI: Could not find Inventory node! UI will not update.");
        }

        // Initialize UI slots
        foreach (Node child in _gridContainer.GetChildren())
        {
            if (child is ColorRect bg)
            {
                _backgrounds.Add(bg);
                _labels.Add(bg.GetNode<Label>("Label"));
            }
        }
        
        UpdateUI();
        UpdateSelection(0);
    }

    private void UpdateUI()
    {
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
