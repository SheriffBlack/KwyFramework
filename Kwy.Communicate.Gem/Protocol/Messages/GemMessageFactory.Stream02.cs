using Secs4Net;
using static Secs4Net.Item;

namespace Kwy.Communicate.Gem;

public static partial class GemMessageFactory
{
    public static SecsMessage EquipmentConstantAcknowledge(bool accepted)
        => Create(GemMessageDefinitions.EquipmentConstantAcknowledge, B(accepted ? (byte)0 : (byte)1));

    public static SecsMessage TraceInitializeAcknowledge(bool accepted)
        => Create(GemMessageDefinitions.TraceInitializeAcknowledge, B(accepted ? (byte)0 : (byte)1));

    public static SecsMessage HostCommandAcknowledge(GemHostCommandAckCode ackCode)
        => Create(GemMessageDefinitions.HostCommandAcknowledge, L(B((byte)ackCode), L()));

    public static SecsMessage RemoteCommand(GemRemoteCommand command)
        => Create(GemMessageDefinitions.HostCommandSend, L(
            A(command.CommandName),
            L(command.Parameters
                .Select(pair => L(A(pair.Key), GemItemCloner.Clone(pair.Value)))
                .ToArray())));
}
