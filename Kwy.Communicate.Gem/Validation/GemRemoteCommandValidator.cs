namespace Kwy.Communicate.Gem;

public sealed record GemRemoteCommandValidationResult(
    bool IsValid,
    GemAckCode AckCode,
    string? Message = null)
{
    public static GemRemoteCommandValidationResult Success { get; } =
        new(true, GemAckCode.Accepted);
}

/// <summary>根据与 Fab 确认的命令合同校验收到的 S2F41 内容。</summary>
public static class GemRemoteCommandValidator
{
    public static GemRemoteCommandValidationResult Validate(
        GemRemoteCommandDefinition definition,
        GemRemoteCommand command,
        GemControlState controlState)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(command);

        if (!string.Equals(definition.Name, command.CommandName, StringComparison.OrdinalIgnoreCase))
        {
            return new(false, GemAckCode.Denied, $"Remote command '{command.CommandName}' is not defined.");
        }

        if (definition.AllowedControlStates is { Count: > 0 } allowedStates &&
            !allowedStates.Contains(controlState))
        {
            return new(
                false,
                GemAckCode.InvalidState,
                $"Remote command '{definition.Name}' is not allowed in control state {controlState}.");
        }

        ILookup<string, KeyValuePair<string, Secs4Net.Item>> received = command.Parameters
            .ToLookup(pair => pair.Key, StringComparer.OrdinalIgnoreCase);

        if (received.Any(group => group.Count() > 1))
        {
            string duplicate = received.First(group => group.Count() > 1).Key;
            return new(false, GemAckCode.InvalidParameter, $"Parameter '{duplicate}' is duplicated.");
        }

        foreach (GemRemoteCommandParameterDefinition parameter in definition.Parameters)
        {
            KeyValuePair<string, Secs4Net.Item>? value = received[parameter.Name]
                .Select(pair => (KeyValuePair<string, Secs4Net.Item>?)pair)
                .SingleOrDefault();

            if (value is null)
            {
                if (parameter.Required)
                {
                    return new(false, GemAckCode.InvalidParameter, $"Required parameter '{parameter.Name}' is missing.");
                }

                continue;
            }

            if (value.Value.Value.Format != parameter.Format)
            {
                return new(
                    false,
                    GemAckCode.InvalidParameter,
                    $"Parameter '{parameter.Name}' expects {parameter.Format}, but received {value.Value.Value.Format}.");
            }
        }

        if (!definition.AllowAdditionalParameters)
        {
            HashSet<string> definedNames = definition.Parameters
                .Select(parameter => parameter.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            string? unknown = command.Parameters.Keys.FirstOrDefault(name => !definedNames.Contains(name));
            if (unknown is not null)
            {
                return new(false, GemAckCode.InvalidParameter, $"Parameter '{unknown}' is not defined.");
            }
        }

        return GemRemoteCommandValidationResult.Success;
    }
}
