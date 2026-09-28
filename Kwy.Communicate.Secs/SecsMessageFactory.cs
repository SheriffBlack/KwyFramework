namespace Kwy.Communicate.Secs;

public static class SecsMessageFactory
{
    public static SecsMessage AreYouThereRequest()
        => new(SecsMessageCode.S1F1AreYouThere, ReplyExpected: true, Name: "AreYouThere");

    public static SecsMessage OnLineData()
        => new(SecsMessageCode.S1F2OnLineData, Name: "OnLineData");

    public static SecsMessage EstablishCommunicationRequest(string mdln, string softrev)
        => new(SecsMessageCode.S1F13EstablishCommunicationsRequest,
            true, SecsItem.L(SecsItem.A(mdln), SecsItem.A(softrev)), Name: "EstablishCommunicationsRequest");

    public static SecsMessage EstablishCommunicationAcknowledge(byte commack, string mdln, string softrev)
        => new(SecsMessageCode.S1F14EstablishCommunicationsAcknowledge,
            false, SecsItem.L(SecsItem.B(commack), SecsItem.L(SecsItem.A(mdln), SecsItem.A(softrev))), Name: "EstablishCommunicationsAcknowledge");
}
