using Secs4Net;
using static Secs4Net.Item;

namespace Kwy.Communicate.Gem;

public static partial class GemMessageFactory
{
    /// <summary>S1F1 </summary>
    public static SecsMessage AreYouThereRequest()
        => Create(GemMessageDefinitions.AreYouThereRequest);

    /// <summary>S1F2 </summary>
    public static SecsMessage AreYouThereResponse()
        => Create(GemMessageDefinitions.AreYouThereResponse);

    /// <summary>S1F13 </summary>
    public static SecsMessage EstablishCommunicationsRequest(string model, string softwareRevision)
        => Create(GemMessageDefinitions.EstablishCommunicationsRequest, L(A(model), A(softwareRevision)));

    /// <summary>S1F14</summary>
    public static SecsMessage EstablishCommunicationsAcknowledge(
        bool accepted,
        string model,
        string softwareRevision)
        => Create(
            GemMessageDefinitions.EstablishCommunicationsAcknowledge,
            L(B(accepted ? (byte)0 : (byte)1), L(A(model), A(softwareRevision))));

    /// <summary>S1F17 </summary>
    public static SecsMessage RequestOnline()
        => Create(GemMessageDefinitions.RequestOnline);

    /// <summary>S1F18 </summary>
    public static SecsMessage OnlineAcknowledge(bool accepted)
        => Create(GemMessageDefinitions.OnlineAcknowledge, B(accepted ? (byte)0 : (byte)1));

    /// <summary>S1F3 </summary>
    public static SecsMessage SelectedEquipmentStatusRequest(params GemVid[] vids)
        => Create(GemMessageDefinitions.SelectedEquipmentStatusRequest, L(vids.Select(id => U4(id.Value)).ToArray()));

    /// <summary>S1F4 </summary>
    public static SecsMessage SelectedEquipmentStatusData(IEnumerable<GemVariable> variables)
        => Create(
            GemMessageDefinitions.SelectedEquipmentStatusData,
            L(variables.Select(item => GemItemCloner.Clone(item.Value)).ToArray()));
}
