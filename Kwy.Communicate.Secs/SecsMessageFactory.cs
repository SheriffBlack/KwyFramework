namespace Kwy.Communicate.Secs;

public static class SecsMessageFactory
{
    public static SecsMessage LinkTestRequest()
        => new(1, 1, ReplyExpected: true, Name: "AreYouThere");

    public static SecsMessage LinkTestResponse()
        => new(1, 2, Name: "OnLineData");

    public static SecsMessage EstablishCommunicationRequest(string mdln, string softrev)
        => new(1, 13, true, SecsItem.L(SecsItem.A(mdln), SecsItem.A(softrev)), Name: "EstablishCommunicationsRequest");

    public static SecsMessage EstablishCommunicationAcknowledge(byte commack, string mdln, string softrev)
        => new(1, 14, false, SecsItem.L(SecsItem.B(commack), SecsItem.L(SecsItem.A(mdln), SecsItem.A(softrev))), Name: "EstablishCommunicationsAcknowledge");
}
