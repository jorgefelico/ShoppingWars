using Godot;

public partial class HealthBar : CanvasLayer
{
    [Export] ColorRect ColorRect;

    public void Refresh(int currentHealth, int maxHealth)
    {
        if (ColorRect == null) return;
        float percent = (float)currentHealth / (float)maxHealth;
        float fillWidth = 300f * percent;
        ColorRect.CustomMaximumSize = new Vector2(Mathf.Clamp(fillWidth, 0f, 300f), 20.0f);
    }
}
