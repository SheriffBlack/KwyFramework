namespace KwyTemplate.Vision.Models;

/// <summary>
/// 流程项目：包含多个流程图（类似 VS 项目包含多个 CS 文件）
/// </summary>
public class FlowProject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "新项目";

    /// <summary>项目中的所有流程图</summary>
    public List<FlowGraph> Graphs { get; set; } = new();

    /// <summary>当前正在编辑的流程图 Id</summary>
    public Guid ActiveGraphId { get; set; }

    /// <summary>项目运行时的入口流程 Id。项目运行会从该流程开始，按列表顺序继续执行。</summary>
    public Guid EntryGraphId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
