using Godot;

public interface IInteractable {
    string HoverText { get; set; }
    MeshInstance3D Outline {get; set;}
    void Interact(PlayerController player);
    void OutlineOn()
    {
        if(Outline == null) return;
        Outline.Visible = true;
    }
    void OutlineOff()
    {
        if(Outline == null) return;
        Outline.Visible = false;
    }
}