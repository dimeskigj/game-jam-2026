using Godot;

public partial class PlayerInput : Node
{
    public Vector2 MoveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool SprintPressed { get; private set; }
    public bool LeftClickHeld { get; private set; }

    [Signal]
    public delegate void InteractEventHandler();
    [Signal]
    public delegate void UseItemEventHandler();
    [Signal]
    public delegate void DropItemEventHandler();
    [Signal]
    public delegate void SlotSelectedEventHandler(int slotIndex);
    [Signal]
    public delegate void ScrollSlotEventHandler(int direction); // 1 = up, -1 = down
    [Signal]
    public delegate void LookInputEventHandler(Vector2 relative);
    [Signal]
    public delegate void ToggleMouseCaptureEventHandler(bool captured);


    public override void _Input(InputEvent @event)
    {
        // Mouse Look
        if (@event is InputEventMouseMotion mouseMotion)
        {
            EmitSignal(SignalName.LookInput, mouseMotion.Relative);
        }

        // Mouse Capture Toggle
        if (Input.IsMouseButtonPressed(MouseButton.Left))
        {
            EmitSignal(SignalName.ToggleMouseCapture, true);
        }
        if (Input.IsKeyPressed(Key.Escape))
        {
            EmitSignal(SignalName.ToggleMouseCapture, false);
        }

        // Interactions
        if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed)
        {
            if (mouseBtn.ButtonIndex == MouseButton.Right)
            {
                EmitSignal(SignalName.Interact);
            }
            // Scroll
            if (mouseBtn.ButtonIndex == MouseButton.WheelUp)
            {
                EmitSignal(SignalName.ScrollSlot, -1); // Previous slot
            }
            else if (mouseBtn.ButtonIndex == MouseButton.WheelDown)
            {
                EmitSignal(SignalName.ScrollSlot, 1); // Next slot
            }
        }
        
        if (@event is InputEventKey k && k.Pressed && !k.Echo)
        {
             if (k.Keycode == Key.E) EmitSignal(SignalName.UseItem);
             if (k.Keycode == Key.Q) EmitSignal(SignalName.DropItem);
             
             // Slots
             if (k.Keycode >= Key.Key1 && k.Keycode <= Key.Key8)
             {
                 EmitSignal(SignalName.SlotSelected, (int)(k.Keycode - Key.Key1));
             }
        }
    }

    public override void _Process(double delta)
    {
        // Continuous Inputs
        MoveInput = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down"); 
        
        // Backup WASD check if input map not set
        if (MoveInput == Vector2.Zero)
        {
            Vector2 inputDir = Vector2.Zero;
            if (Input.IsKeyPressed(Key.W)) inputDir.Y -= 1;
            if (Input.IsKeyPressed(Key.S)) inputDir.Y += 1;
            if (Input.IsKeyPressed(Key.A)) inputDir.X -= 1;
            if (Input.IsKeyPressed(Key.D)) inputDir.X += 1;
            MoveInput = inputDir.Normalized();
        }

        JumpPressed = Input.IsKeyPressed(Key.Space);
        LeftClickHeld = Input.IsMouseButtonPressed(MouseButton.Left);
    }
}
