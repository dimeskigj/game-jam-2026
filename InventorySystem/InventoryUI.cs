using Godot;
using System.Collections.Generic;

public partial class InventoryUI : CanvasLayer
{
    private GridContainer _gridContainer;
    private Inventory _inventory;
    private List<Label> _labels = new List<Label>();
    private List<ColorRect> _backgrounds = new List<ColorRect>();
    private List<SubViewport> _viewports = new List<SubViewport>();
    private Label _descriptionLabel;

    public override void _Ready()
    {
        _gridContainer = GetNode<GridContainer>("Control/GridContainer");
        _descriptionLabel = GetNodeOrNull<Label>("Control/ItemDescription");
        
        // Find player/ball and inventory robustly
        var window = GetTree().Root;
        for (int i = 0; i < window.GetChildCount(); i++)
        {
            var sceneRoot = window.GetChild(i);
            Node playerNode = sceneRoot.FindChild("Player", true, false) ?? sceneRoot.FindChild("Ball", true, false);
            if (playerNode != null)
            {
                _inventory = playerNode.GetNodeOrNull<Inventory>("Inventory");
                if (_inventory != null) break;
            }
        }

        if (_inventory != null)
        {
            _inventory.InventoryUpdated += UpdateUI;
            _inventory.SelectedSlotChanged += UpdateSelection;
        }

        // Initialize UI slots
        foreach (Node child in _gridContainer.GetChildren())
        {
            if (child is ColorRect bg)
            {
                _backgrounds.Add(bg);
                var label = bg.GetNode<Label>("Label");
                _labels.Add(label);

                // Create Viewport Setup for 3D item preview
                var container = new SubViewportContainer();
                container.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                container.Stretch = true;
                bg.AddChild(container);
                
                bg.MoveChild(label, -1); // Ensure label is on top of 3D preview

                var viewport = new SubViewport();
                viewport.TransparentBg = true;
                viewport.OwnWorld3D = true;
                viewport.Size = new Vector2I(128, 128);
                container.AddChild(viewport);
                _viewports.Add(viewport);

                // Add Camera to Viewport
                var camera = new Camera3D();
                camera.Transform = new Transform3D(Basis.Identity, new Vector3(0, 0, 1.5f));
                viewport.AddChild(camera);

                // Add Light to Viewport
                var light = new DirectionalLight3D();
                light.Transform = new Transform3D(new Basis(Vector3.Right, -0.5f), Vector3.Zero);
                viewport.AddChild(light);
            }
        }
        
        UpdateUI();
        UpdateSelection(0);
    }

    private void UpdateUI()
    {
        if (_inventory == null) return;

        for (int i = 0; i < Inventory.MaxSlots; i++)
        {
            if (i < _labels.Count)
            {
                InventoryItem item = _inventory.items[i];
                var viewport = _viewports[i];
                
                // Clear old model
                foreach (var child in viewport.GetChildren())
                {
                    if (child is not Camera3D && child is not Light3D)
                        child.QueueFree();
                }

                if (item != null)
                {
                    _labels[i].Text = item.Stackable ? $"{item.CurrentStack}" : "";
                    
                    if (item.ModelScene != null)
                    {
                        var model = item.ModelScene.Instantiate<Node3D>();
                        viewport.AddChild(model);
                        model.Scale = Vector3.One * 0.05f; // Scale down to fit slot properly
                        
                        // Add rotation animation node
                        UpdateModelRotation(viewport, model);
                    }
                }
                else
                {
                    _labels[i].Text = "";
                }
            }
        }
        UpdateSelection(_inventory.selectedSlot);
    }

    private async void UpdateModelRotation(SubViewport viewport, Node3D model)
    {
        while (IsInstanceValid(model) && IsInstanceValid(this) && IsInstanceValid(viewport))
        {
            if (!viewport.IsAncestorOf(model)) break;
            model.RotateY(0.02f);
            await ToSignal(GetTree(), "process_frame");
        }
    }

    private void UpdateSelection(int slot)
    {
        for (int i = 0; i < _backgrounds.Count; i++)
        {
            if (i == slot)
            {
                _backgrounds[i].Color = new Color(0.5f, 0.5f, 0.5f, 0.8f); // Selected
                
                // Update description
                if (_descriptionLabel != null && _inventory != null)
                {
                    var item = _inventory.items[i];
                    _descriptionLabel.Text = item != null ? $"{item.Name}\n{item.Description}" : "Empty Slot";
                }
            }
            else
            {
                _backgrounds[i].Color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Unselected
            }
        }
    }
}
