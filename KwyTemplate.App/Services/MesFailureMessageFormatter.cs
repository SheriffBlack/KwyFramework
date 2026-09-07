using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.App.Services;

public static class MesFailureMessageFormatter
{
    public static string Format(string operationName, MesResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        string returnCode = result.Exchange?.ReturnCode?.ToString() ?? result.Code;
        string returnMessage = !string.IsNullOrWhiteSpace(result.Exchange?.ReturnMessage)
            ? result.Exchange.ReturnMessage
            : string.IsNullOrWhiteSpace(result.Message) ? result.Code : result.Message;

        return $"{operationName}失败！\nReturnCode={returnCode}\nReturnMessage={returnMessage}";
    }
}


