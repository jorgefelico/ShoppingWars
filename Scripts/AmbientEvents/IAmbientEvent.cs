using Godot;

public interface IAmbientEvent
{
    string EventId { get; }
    string DisplayName { get; }
    string Description { get; }
    Color BannerColor { get; }
    float DefaultDuration { get; }
    float Weight { get; set; }

    void OnStart(AmbientEventManager manager, float duration);
    void OnProcess(AmbientEventManager manager, float delta);
    void OnEnd(AmbientEventManager manager);
}
