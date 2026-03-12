namespace KosmosAdapterV2.Core.Interfaces;

public interface IProtocolHandler
{
    void RegisterProtocol(string protocolName);
    bool IsProtocolRegistered(string protocolName);
}
