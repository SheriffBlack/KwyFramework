using Secs4Net;
using static Secs4Net.Item;

namespace Kwy.Communicate.Gem;

public static partial class GemMessageFactory
{
    public static SecsMessage ProcessProgramAcknowledge(GemProcessProgramAcknowledgeCode ackCode)
        => Create(GemMessageDefinitions.ProcessProgramAcknowledge, B((byte)ackCode));

    public static SecsMessage ProcessProgramLoadInquire(string ppid, uint length)
        => Create(GemMessageDefinitions.ProcessProgramLoadInquire, L(A(ppid), U4(length)));

    public static SecsMessage ProcessProgramLoadGrant(GemProcessProgramLoadGrant grant)
        => Create(GemMessageDefinitions.ProcessProgramLoadGrant, B((byte)grant));

    public static SecsMessage ProcessProgramSend(GemProcessProgram processProgram)
        => Create(
            GemMessageDefinitions.ProcessProgramSend,
            L(A(processProgram.Ppid), GemItemCloner.Clone(processProgram.Body)));

    public static SecsMessage ProcessProgramRequest(string ppid)
        => Create(GemMessageDefinitions.ProcessProgramRequest, A(ppid));

    public static SecsMessage ProcessProgramData(GemProcessProgram? processProgram)
        => processProgram is null
            ? Create(GemMessageDefinitions.ProcessProgramData, L())
            : Create(
                GemMessageDefinitions.ProcessProgramData,
                L(A(processProgram.Ppid), GemItemCloner.Clone(processProgram.Body)));

    public static SecsMessage DeleteProcessProgram(IEnumerable<string> ppids)
        => Create(GemMessageDefinitions.DeleteProcessProgram, L(ppids.Select(A).ToArray()));

    public static SecsMessage DeleteProcessProgramAcknowledge(GemProcessProgramAcknowledgeCode ackCode)
        => Create(GemMessageDefinitions.DeleteProcessProgramAcknowledge, B((byte)ackCode));

    public static SecsMessage CurrentProcessProgramDirectoryRequest()
        => Create(GemMessageDefinitions.CurrentProcessProgramDirectoryRequest);

    public static SecsMessage CurrentProcessProgramDirectoryData(IEnumerable<string> ppids)
        => Create(GemMessageDefinitions.CurrentProcessProgramDirectoryData, L(ppids.Select(A).ToArray()));
}
