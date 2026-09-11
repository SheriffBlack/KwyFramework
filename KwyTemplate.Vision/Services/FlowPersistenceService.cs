using Kwy.Files;
using KwyTemplate.Vision.Models;
using System.IO;

namespace KwyTemplate.Vision.Services;

/// <summary>
/// 流程图持久化服务：负责项目保存、加载与旧版单图文件迁移。
/// </summary>
public class FlowPersistenceService
{
    /// <summary>将流程项目保存到文件。</summary>
    public async Task SaveProjectAsync(FlowProject project, string filePath)
    {
        project.UpdatedAt = DateTime.Now;
        await JsonHelper.WriteAsync(filePath, project).ConfigureAwait(false);
    }

    /// <summary>从文件加载流程项目。支持旧版单图文件自动迁移。</summary>
    public async Task<FlowProject?> LoadProjectAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var project = await JsonHelper.ReadAsync<FlowProject>(filePath).ConfigureAwait(false);
            if (project?.Graphs is { Count: > 0 })
            {
                EnsureProjectEntry(project);
                return project;
            }

            var singleGraph = await JsonHelper.ReadAsync<FlowGraph>(filePath).ConfigureAwait(false);
            if (singleGraph != null)
            {
                return new FlowProject
                {
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    Graphs = new List<FlowGraph> { singleGraph },
                    ActiveGraphId = singleGraph.Id,
                    EntryGraphId = singleGraph.Id
                };
            }
        }
        catch
        {
            // TODO: 接入日志服务后记录加载失败原因。
        }

        return null;
    }

    /// <summary>序列化项目为 JSON 字符串。</summary>
    public string SerializeProject(FlowProject project)
        => JsonHelper.Serialize(project);

    private static void EnsureProjectEntry(FlowProject project)
    {
        if (project.Graphs.Count == 0)
        {
            return;
        }

        if (project.EntryGraphId == Guid.Empty || project.Graphs.All(graph => graph.Id != project.EntryGraphId))
        {
            project.EntryGraphId = project.Graphs[0].Id;
        }

        foreach (FlowGraph graph in project.Graphs)
        {
            graph.IsProjectEntry = graph.Id == project.EntryGraphId;
        }
    }
}
