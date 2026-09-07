using Godot;

public abstract class AmbientEventBase : IAmbientEvent
{
    public abstract string EventId { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public virtual Color BannerColor => Colors.White;
    public virtual float DefaultDuration => 15.0f;
    public virtual float Weight { get; set; } = 1.0f;

    public virtual void OnStart(AmbientEventManager manager, float duration) { }
    public virtual void OnProcess(AmbientEventManager manager, float delta) { }
    public virtual void OnEnd(AmbientEventManager manager) { }
}
