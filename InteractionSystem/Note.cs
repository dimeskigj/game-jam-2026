using Godot;

public partial class Note : RigidBody3D
{
	[Export(PropertyHint.MultilineText)] 
	public string NoteText = "It's just a blank piece of paper.";
	
	public override void _Ready()
	{
		// Notes are usually lightweight
		Mass = 0.1f;
	}
}
