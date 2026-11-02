using Secs4Net;
using static Secs4Net.Item;

namespace Kwy.Communicate.Gem;

public static partial class GemMessageFactory
{
    public static SecsMessage AreYouThereRequest()
        => Create(GemMessageDefinitions.AreYouThereRequest);

    public static SecsMessage AreYouThereResponse()
        => Create(GemMessageDefinitions.AreYouThereResponse);

    public static SecsMessage EstablishCommunicationsRequest(string model, string softwareRevision)
        => Create(GemMessageDefinitions.EstablishCommunicationsRequest, L(A(model), A(softwareRevision)));

    public static SecsMessage EstablishCommunicationsAcknowledge(
        bool accepted,
        string model,
        string softwareRevision)
        => Create(
            GemMessageDefinitions.EstablishCommunicationsAcknowledge,
            L(B(accepted ? (byte)0 : (byte)1), L(A(model), A(softwareRevision))));

    public static SecsMessage RequestOnline()
        => Create(GemMessageDefinitions.RequestOnline);

    public static SecsMessage OnlineAcknowledge(bool accepted)
        => Create(GemMessageDefinitions.OnlineAcknowledge, B(accepted ? (byte)0 : (byte)1));

    public static SecsMessage SelectedEquipmentStatusRequest(params GemVid[] vids)
        => Create(GemMessageDefinitions.SelectedEquipmentStatusRequest, L(vids.Select(id => U4(id.Value)).ToArray()));

    public static SecsMessage SelectedEquipmentStatusData(IEnumerable<GemVariable> variables)
        => Create(
            GemMessageDefinitions.SelectedEquipmentStatusData,
            L(variables.Select(item => GemItemCloner.Clone(item.Value)).ToArray()));
}
