namespace KosmosAdapterV2.Core.Interfaces;

public interface IStartupManager
{
    void EnableStartup();
    void DisableStartup();
    bool IsStartupEnabled { get; }
}
