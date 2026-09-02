using Godot;

public interface IInteractable {
    string HoverText { get; set; }
    MeshInstance3D Outline {get; set;}
    Label3D HoverLabel {get; set;}
    void Interact(PlayerController player);
    void OutlineOn()
    {
        if(Outline != null) Outline.Visible = true;
        if(HoverLabel != null) HoverLabel.Visible = true;
    }
    void OutlineOff()
    {
        if(Outline != null) Outline.Visible = false;
        if(HoverLabel != null) HoverLabel.Visible = false;
    }
}