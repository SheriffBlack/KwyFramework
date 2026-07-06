namespace Kwy.Communicate.Gem;

public sealed class GemRegistry
{
    private readonly Dictionary<uint, GemVariable> variables = new();
    private readonly Dictionary<uint, GemEquipmentConstant> constants = new();
    private readonly Dictionary<uint, GemReport> reports = new();
    private readonly Dictionary<uint, GemCollectionEvent> events = new();
    private readonly Dictionary<uint, GemAlarm> alarms = new();
    private readonly Dictionary<uint, GemVariableDefinition> variableDefinitions = new();
    private readonly Dictionary<uint, GemReportDefinition> reportDefinitions = new();
    private readonly Dictionary<uint, GemCollectionEventDefinition> eventDefinitions = new();
    private readonly Dictionary<uint, GemAlarmDefinition> alarmDefinitions = new();
    private readonly List<GemAlarmHistoryItem> alarmHistory = new();
    private readonly Dictionary<uint, GemTraceDefinition> traces = new();
    private readonly List<GemTraceSample> traceSamples = new();
    private readonly List<GemRecipeChangeRecord> recipeHistory = new();
    private readonly Dictionary<string, Func<GemRemoteCommand, CancellationToken, Task<GemRemoteCommandResult>>> commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GemRecipe> recipes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<uint, GemVariable> Variables => variables;

    public IReadOnlyDictionary<uint, GemEquipmentConstant> Constants => constants;

    public IReadOnlyDictionary<uint, GemReport> Reports => reports;

    public IReadOnlyDictionary<uint, GemCollectionEvent> Events => events;

    public IReadOnlyDictionary<uint, GemAlarm> Alarms => alarms;

    public IReadOnlyDictionary<uint, GemVariableDefinition> VariableDefinitions => variableDefinitions;

    public IReadOnlyDictionary<uint, GemReportDefinition> ReportDefinitions => reportDefinitions;

    public IReadOnlyDictionary<uint, GemCollectionEventDefinition> EventDefinitions => eventDefinitions;

    public IReadOnlyDictionary<uint, GemAlarmDefinition> AlarmDefinitions => alarmDefinitions;

    public IReadOnlyList<GemAlarmHistoryItem> AlarmHistory => alarmHistory;

    public IReadOnlyDictionary<uint, GemTraceDefinition> Traces => traces;

    public IReadOnlyList<GemTraceSample> TraceSamples => traceSamples;

    public IReadOnlyList<GemRecipeChangeRecord> RecipeHistory => recipeHistory;

    public void RegisterVariable(GemVariable variable) => variables[variable.Id] = variable;

    public void RegisterVariableDefinition(GemVariableDefinition variable) => variableDefinitions[variable.Vid.Value] = variable;

    public void RegisterConstant(GemEquipmentConstant constant) => constants[constant.Id] = constant;

    public void RegisterReport(GemReport report) => reports[report.ReportId] = report;

    public void RegisterReportDefinition(GemReportDefinition report) => reportDefinitions[report.Rptid.Value] = report;

    public void RegisterEvent(GemCollectionEvent collectionEvent) => events[collectionEvent.EventId] = collectionEvent;

    public void RegisterEventDefinition(GemCollectionEventDefinition collectionEvent) => eventDefinitions[collectionEvent.Ceid.Value] = collectionEvent;

    public void RegisterAlarmDefinition(GemAlarmDefinition alarm) => alarmDefinitions[alarm.Alid.Value] = alarm;

    public void SetAlarm(GemAlarm alarm)
    {
        alarms[alarm.AlarmId] = alarm;
        alarmHistory.Add(new GemAlarmHistoryItem(alarm, DateTimeOffset.Now));
    }

    public void RegisterCommand(string commandName, Func<GemRemoteCommand, CancellationToken, Task<GemRemoteCommandResult>> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        commands[commandName] = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public bool TryGetCommand(string commandName, out Func<GemRemoteCommand, CancellationToken, Task<GemRemoteCommandResult>> handler)
        => commands.TryGetValue(commandName, out handler!);

    public void SaveRecipe(GemRecipe recipe)
    {
        recipes[recipe.Ppid] = recipe;
        recipeHistory.Add(new GemRecipeChangeRecord(recipe.Ppid, "Save", "System", DateTimeOffset.Now));
    }

    public bool TryGetRecipe(string ppid, out GemRecipe recipe) => recipes.TryGetValue(ppid, out recipe!);

    public void RegisterTrace(GemTraceDefinition trace) => traces[trace.TraceId] = trace;

    public void AddTraceSample(GemTraceSample sample) => traceSamples.Add(sample);
}
