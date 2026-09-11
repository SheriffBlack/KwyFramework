using KwyTemplate.Vision.Models;
using KwyTemplate.Vision.ViewModels.Items;
using System.Windows;

namespace KwyTemplate.Vision.Services;

/// <summary>
/// 负责节点自动排版逻辑的服务类。
/// 将原本在 ViewModel 里的排版计算能力进行“沉降”，
/// 便于各视图复用这一规则且便于多策略排版算法的升级迭代。
/// </summary>
public class FlowLayoutService
{
    private const double LevelSpacing = 300; // 前后层级间的间距 (水平排版时是 X，垂直时是 Y)
    private const double InLevelSpacing = 200;  // 同一层级节点间的间距 (水平排版时是 Y，垂直时是 X)

    /// <summary>
    /// 对传入的节点列表执行基于入度的拓扑分层排版算法，并分离不相关的连通块以防极大留白。
    /// </summary>
    public void AutoLayoutNodes(IEnumerable<FlowNodeViewModel> nodes, IEnumerable<FlowConnectionViewModel> connections, Enum? direction = null)
    {
        if (!nodes.Any()) return;

        // 获取用户是否显式指定了全局方向
        var explicitDir = direction is KwyTemplate.Vision.Models.FlowLayoutDirection d ? (KwyTemplate.Vision.Models.FlowLayoutDirection?)d : null;

        var nodeList = nodes.ToList();
        var connList = connections.ToList();

        // 1. 将整个画布分离成完全没有连线关联的“连通分量” (Connected Components)
        var components = GetConnectedComponents(nodeList, connList);

        double currentStartY = 50;
        double targetStartX = 50; // 设定全局统一的左侧对齐基准线

        // 2. 将互不相干的成分组进行单独计算排版，再在垂直方向上逐个紧凑堆叠
        foreach (var compNodes in components)
        {
            var compConnections = connList.Where(c =>
                c.Source.Node != null &&
                c.Target.Node != null &&
                compNodes.Contains(c.Source.Node) &&
                compNodes.Contains(c.Target.Node)).ToList();

            var compDir = explicitDir ?? InferDirection(compNodes, compConnections);

            // 为当前连通块布置最优几何排列
            LayoutComponent(compNodes, compConnections, targetStartX, currentStartY, compDir);

            // ─── 核心修复：包围盒归一化强制左对齐 ──────────────────────────────────
            double minX = compNodes.Min(n => n.Location.X);
            double minY = compNodes.Min(n => n.Location.Y);

            // 🌟 关键修改：去掉 minX < 50 的条件判断。
            // 无论这个块本来排在哪里，强制让它的最左侧节点对齐到 targetStartX (50)
            double shiftX = targetStartX - minX;
            double shiftY = currentStartY - minY;

            if (shiftX != 0 || shiftY != 0)
            {
                foreach (var n in compNodes)
                {
                    n.Location = new Point(n.Location.X + shiftX, n.Location.Y + shiftY);
                }
            }
            // ─────────────────────────────────────────────────────────────────

            // 重新计算偏移后的最大 Y 边界，并预留足够的留白 (100像素)
            double maxY = compNodes.Max(n => n.Location.Y + GetEstimatedSize(n, FlowLayoutDirection.Vertical));

            // 确保下一个连通块绝对安全地排在下面
            currentStartY = Math.Max(currentStartY + 100, maxY + 100);
        }
    }

    /// <summary>
    /// 根据当前连通块中节点连接的“端口朝向”来判断排版意图，而不是依赖当前不稳定的屏幕坐标。
    /// 彻底解决多次点击排版方向来回跳动（翻转）的问题。
    /// </summary>
    private FlowLayoutDirection InferDirection(List<FlowNodeViewModel> nodes, List<FlowConnectionViewModel> connections)
    {
        // 视觉平台默认向下生长
        if (nodes.Count < 2) return FlowLayoutDirection.Vertical;

        int horizontalScore = 0;
        int verticalScore = 0;

        foreach (var conn in connections)
        {
            if (conn.Target != null)
            {
                if (conn.Target.Side == PortSide.Left || conn.Target.Side == PortSide.Right)
                    horizontalScore++;
                else if (conn.Target.Side == PortSide.Top || conn.Target.Side == PortSide.Bottom)
                    verticalScore++;
            }
        }

        // 🌟 核心修改：只有当左右横向连线数量是上下纵向连线的 2 倍以上时，才认为是横向排版。否则死守纵向。
        return horizontalScore > verticalScore * 2
            ? FlowLayoutDirection.Horizontal
            : FlowLayoutDirection.Vertical;
    }

    /// <summary>
    /// 提取出互不关联的节点连通图分量，避免它在最终渲染时因没有数据依赖而被扯出极大空白。
    /// </summary>
    private List<List<FlowNodeViewModel>> GetConnectedComponents(List<FlowNodeViewModel> nodes, List<FlowConnectionViewModel> connections)
    {
        var adj = new Dictionary<FlowNodeViewModel, HashSet<FlowNodeViewModel>>();
        foreach (var n in nodes) adj[n] = new HashSet<FlowNodeViewModel>();

        // 构建无向图邻接表来寻找分量
        foreach (var c in connections)
        {
            if (c.Source.Node != null && c.Target.Node != null)
            {
                adj[c.Source.Node].Add(c.Target.Node);
                adj[c.Target.Node].Add(c.Source.Node);
            }
        }

        var visited = new HashSet<FlowNodeViewModel>();
        var components = new List<List<FlowNodeViewModel>>();

        foreach (var n in nodes)
        {
            if (!visited.Contains(n))
            {
                var comp = new List<FlowNodeViewModel>();
                var queue = new Queue<FlowNodeViewModel>();
                queue.Enqueue(n);
                visited.Add(n);

                while (queue.Count > 0)
                {
                    var curr = queue.Dequeue();
                    comp.Add(curr);
                    foreach (var neighbor in adj[curr])
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
                components.Add(comp);
            }
        }
        return components;
    }

    /// <summary>
    /// 粗略估计某个节点在当前排版方向上的尺寸。
    /// </summary>
    private double GetEstimatedSize(FlowNodeViewModel node, FlowLayoutDirection direction)
    {
        if (direction == FlowLayoutDirection.Horizontal)
        {
            // 水平排版时，关注的是高度（为了让垂直方向不重叠）
            int inputCount = GetLayoutPortCount(node, PortDirection.Input);
            int outputCount = GetLayoutPortCount(node, PortDirection.Output);
            int maxPorts = Math.Max(inputCount, outputCount);
            return 60 + maxPorts * 26;
        }
        else
        {
            // 垂直排版时，关注的是宽度（为了让水平方向不重叠）
            return 160; // 节点默认大概 160 宽
        }
    }

    private static int GetLayoutPortCount(FlowNodeViewModel node, PortDirection direction)
        => Math.Max(1, node.GetVisiblePorts().Count(port => port.Direction == direction));

    private static int GetLayoutPortIndex(FlowNodeViewModel node, PortViewModel port)
    {
        var ports = node.GetVisiblePorts()
            .Where(item => item.Direction == port.Direction)
            .ToList();

        int index = ports.FindIndex(item => item.PortId == port.PortId && item.Side == port.Side);
        if (index < 0)
        {
            index = ports.FindIndex(item => item.PortId == port.PortId);
        }

        return Math.Max(0, index);
    }

    /// <summary>
    /// 对单块互相存在逻辑关联的节点集进行深入的排布、引流和碰撞规避算法。
    /// 采用 Sugiyama 风格的重心法交叉消减，确保连线不发生穿越。
    /// </summary>
    private void LayoutComponent(List<FlowNodeViewModel> nodes, List<FlowConnectionViewModel> connections, double startX, double startY, FlowLayoutDirection direction)
    {
        // ─── 第 1 步：拓扑分层（Kahn 算法） ─────────────────────────────
        var inDegree = nodes.ToDictionary(n => n, n => 0);
        var adj = nodes.ToDictionary(n => n, n => new List<FlowNodeViewModel>());
        var predAdj = nodes.ToDictionary(n => n, n => new List<FlowNodeViewModel>());

        foreach (var conn in connections)
        {
            var src = conn.Source?.Node;
            var tgt = conn.Target?.Node;
            if (src == null || tgt == null || src == tgt) continue;
            adj[src].Add(tgt);
            predAdj[tgt].Add(src);
            inDegree[tgt]++;
        }

        var level = nodes.ToDictionary(n => n, n => 0);
        var q = new Queue<FlowNodeViewModel>(nodes.Where(n => inDegree[n] == 0));
        // 处理环路：如果全部有入度，强制从第一个开始
        if (!q.Any()) q.Enqueue(nodes.First());

        var tempIndeg = new Dictionary<FlowNodeViewModel, int>(inDegree);
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            foreach (var nxt in adj[cur])
            {
                level[nxt] = Math.Max(level[nxt], level[cur] + 1);
                tempIndeg[nxt]--;
                if (tempIndeg[nxt] == 0) q.Enqueue(nxt);
            }
        }

        // 按层分组
        var levels = nodes
            .GroupBy(n => level[n])
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());

        int maxLvl = levels.Keys.Max();

        // ─── 第 2 步：重心法交叉消减（前向+后向，多轮迭代） ─────────────
        // 核心思路：每个节点的排序权值 = 其上/下游节点在相邻层中位置索引的平均值
        // 这样具有连接关系的节点会尽量挨在一起，避免交叉。
        var order = nodes.ToDictionary(n => n, n => (double)levels[level[n]].IndexOf(n));

        for (int iter = 0; iter < 6; iter++)
        {
            // 前向扫描：从下游到上游（按层从左到右）
            for (int lvl = 1; lvl <= maxLvl; lvl++)
            {
                if (!levels.ContainsKey(lvl)) continue;
                foreach (var node in levels[lvl])
                {
                    var preds = connections
                        .Where(c => c.Target?.Node == node && c.Source?.Node != null && level[c.Source.Node] == lvl - 1)
                        .ToList();
                    if (preds.Any())
                    {
                        // 权值 = 上游节点的当前位次 + 目标端口的绝对索引偏差
                        double sum = preds.Sum(c =>
                        {
                            double srcOrd = order[c.Source.Node];
                            int tgtPortIdx = GetLayoutPortIndex(node, c.Target);
                            int srcPortIdx = GetLayoutPortIndex(c.Source.Node, c.Source);
                            return srcOrd + (tgtPortIdx - srcPortIdx) * 0.05;
                        });
                        order[node] = sum / preds.Count;
                    }
                }
                // 按权值重新排序本层节点，并更新位次
                levels[lvl] = levels[lvl].OrderBy(n => order[n]).ToList();
                for (int i = 0; i < levels[lvl].Count; i++) order[levels[lvl][i]] = i;
            }

            // 后向扫描：从上游到下游
            for (int lvl = maxLvl - 1; lvl >= 0; lvl--)
            {
                if (!levels.ContainsKey(lvl)) continue;
                foreach (var node in levels[lvl])
                {
                    var succs = connections
                        .Where(c => c.Source?.Node == node && c.Target?.Node != null && level[c.Target.Node] == lvl + 1)
                        .ToList();
                    if (succs.Any())
                    {
                        double sum = succs.Sum(c =>
                        {
                            double tgtOrd = order[c.Target.Node];
                            int srcPortIdx = GetLayoutPortIndex(node, c.Source);
                            int tgtPortIdx = GetLayoutPortIndex(c.Target.Node, c.Target);
                            return tgtOrd + (srcPortIdx - tgtPortIdx) * 0.05;
                        });
                        order[node] = sum / succs.Count;
                    }
                }
                levels[lvl] = levels[lvl].OrderBy(n => order[n]).ToList();
                for (int i = 0; i < levels[lvl].Count; i++) order[levels[lvl][i]] = i;
            }
        }

        // ─── 第 3 步：按层次顺序均匀分配初始辅轴坐标 ────────────────────────
        var nodeSecondaryPos = new Dictionary<FlowNodeViewModel, double>();
        double nodeStep = (direction == FlowLayoutDirection.Horizontal) ? 100.0 : 200.0;
        double sGap = Math.Max(InLevelSpacing, nodeStep + 30);

        foreach (var kv in levels)
        {
            var levelNodes = kv.Value;
            int count = levelNodes.Count;
            double totalSize = count * nodeStep + (count - 1) * 30;
            double basePos = (direction == FlowLayoutDirection.Horizontal ? startY : startX) - totalSize / 2.0;
            for (int i = 0; i < count; i++)
            {
                nodeSecondaryPos[levelNodes[i]] = basePos + i * (nodeStep + 30);
            }
        }

        // ─── 第 4 步：弹性松弛（Spring Relaxation）精细对齐 ─────────────
        // 引入“端口级引力分布”：根据端口在节点上的相对位置施加引力偏移，
        // 既能避免多条连线扎堆交错，又能促成左侧端口天然形成干净的 L 型连线，且不影响 1对1 节点的居中。

        double portSpreadSpacing = (direction == FlowLayoutDirection.Horizontal) ? 100.0 : 200.0;

        for (int iter = 0; iter < 12; iter++)
        {
            // 前向松弛：目标节点(下游)向上游源节点靠拢
            for (int lvl = 1; lvl <= maxLvl; lvl++)
            {
                if (!levels.ContainsKey(lvl)) continue;
                foreach (var node in levels[lvl])
                {
                    var preds = connections
                        .Where(c => c.Target?.Node == node && c.Source?.Node != null)
                        .ToList();
                    if (!preds.Any()) continue;

                    double idealPos = preds.Average(c =>
                    {
                        double basePos = nodeSecondaryPos[c.Source.Node];

                        // 计算源端偏移
                        int srcIdx = GetLayoutPortIndex(c.Source.Node, c.Source);
                        int srcCount = GetLayoutPortCount(c.Source.Node, PortDirection.Output);
                        double srcOffset = (srcIdx - (srcCount - 1) / 2.0) * portSpreadSpacing;

                        // 计算目标端偏移
                        int tgtIdx = GetLayoutPortIndex(c.Target.Node, c.Target);
                        int tgtCount = GetLayoutPortCount(c.Target.Node, PortDirection.Input);
                        double tgtOffset = (tgtIdx - (tgtCount - 1) / 2.0) * portSpreadSpacing;

                        // Target 期望对齐到 Source 对应的端口物理位置
                        return basePos + srcOffset - tgtOffset;
                    });
                    nodeSecondaryPos[node] = nodeSecondaryPos[node] * 0.4 + idealPos * 0.6;
                }
                ResolveOverlaps(levels[lvl], nodeSecondaryPos, direction);
            }

            // 后向松弛：源节点(上游)向下游目标节点靠拢
            for (int lvl = maxLvl - 1; lvl >= 0; lvl--)
            {
                if (!levels.ContainsKey(lvl)) continue;
                foreach (var node in levels[lvl])
                {
                    var succs = connections
                        .Where(c => c.Source?.Node == node && c.Target?.Node != null)
                        .ToList();
                    if (!succs.Any()) continue;

                    double idealPos = succs.Average(c =>
                    {
                        double basePos = nodeSecondaryPos[c.Target.Node];

                        // 计算源端偏移
                        int srcIdx = GetLayoutPortIndex(c.Source.Node, c.Source);
                        int srcCount = GetLayoutPortCount(c.Source.Node, PortDirection.Output);
                        double srcOffset = (srcIdx - (srcCount - 1) / 2.0) * portSpreadSpacing;

                        // 计算目标端偏移
                        int tgtIdx = GetLayoutPortIndex(c.Target.Node, c.Target);
                        int tgtCount = GetLayoutPortCount(c.Target.Node, PortDirection.Input);
                        double tgtOffset = (tgtIdx - (tgtCount - 1) / 2.0) * portSpreadSpacing;

                        // Source 期望对齐到 Target 对应的端口物理位置
                        return basePos + tgtOffset - srcOffset;
                    });
                    nodeSecondaryPos[node] = nodeSecondaryPos[node] * 0.4 + idealPos * 0.6;
                }
                ResolveOverlaps(levels[lvl], nodeSecondaryPos, direction);
            }
        }

        // ─── 第 5 步：将整体漂移锚定，并根据方向设置 Location ────────────────
        double minSecondary = nodeSecondaryPos.Values.Min();
        double startSecondary = (direction == FlowLayoutDirection.Horizontal) ? startY : startX;
        double offsetSecondary = startSecondary - minSecondary;

        // 根据方向动态确定层间距：
        // 水平排版通常宽度较大，所以层间距 300 比较合适；
        // 垂直排版时，节点通常较扁（宽160x高约60），所以层间距 180~200 视觉上更紧凑、更好看。
        double currentLevelSpacing = (direction == FlowLayoutDirection.Horizontal) ? LevelSpacing : 180;

        foreach (var kv in levels)
        {
            foreach (var node in kv.Value)
            {
                double secondary = nodeSecondaryPos[node] + offsetSecondary;
                double primary = (direction == FlowLayoutDirection.Horizontal ? startX : startY) + kv.Key * currentLevelSpacing;

                if (direction == FlowLayoutDirection.Horizontal)
                    node.Location = new Point(primary, secondary);
                else
                    node.Location = new Point(secondary, primary);
            }
        }

        // ─── 第 6 步：全图双向正交传播吸附 (Bi-directional Snapping) ─────────────
        var adjustedNodes = new HashSet<FlowNodeViewModel>();

        // 1. 将连线按主干到旁支的优先级排序
        var sortedConnections = connections
            .Where(c => c.Source?.Node != null && c.Target?.Node != null)
            .OrderBy(c =>
            {
                bool srcTrunk = c.Source.Side == PortSide.Top || c.Source.Side == PortSide.Bottom;
                bool tgtTrunk = c.Target.Side == PortSide.Top || c.Target.Side == PortSide.Bottom;

                if (srcTrunk && tgtTrunk) return 0; // 优先垂直主干
                if (tgtTrunk) return 1;             // 其次汇入主干
                if (srcTrunk) return 2;             // 其次引出主干
                return 3;                           // 最后纯横向
            })
            .ToList();

        // 2. 选取主干源头作为初始锚点
        var anchor = nodes.FirstOrDefault(n =>
            !connections.Any(c => c.Target?.Node == n) &&
            connections.Any(c => c.Source?.Node == n && c.Source.Side == PortSide.Bottom)
        ) ?? nodes.FirstOrDefault();

        if (anchor != null)
        {
            adjustedNodes.Add(anchor);
        }

        // 💡 优化：为了让 switch 矩阵极致简洁，定义简短的常量别名
        const PortSide R = PortSide.Right;
        const PortSide L = PortSide.Left;
        const PortSide T = PortSide.Top;
        const PortSide B = PortSide.Bottom;

        // 3. 水波纹双向传播
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var conn in sortedConnections)
            {
                var src = conn.Source.Node;
                var tgt = conn.Target.Node;

                bool srcLocked = adjustedNodes.Contains(src);
                bool tgtLocked = adjustedNodes.Contains(tgt);

                if (srcLocked && tgtLocked) continue;
                if (!srcLocked && !tgtLocked) continue;

                // 使用实际测量宽度用于上下端口居中对齐；未测量时回退到节点默认宽度。
                double srcWidth = GetNodeWidth(src);
                double tgtWidth = GetNodeWidth(tgt);

                var srcSide = conn.Source.Side;
                var tgtSide = conn.Target.Side;

                // 🌟 核心优化：使用 C# 的元组 Switch 表达式 (Tuple Switch Expression)
                // 直接将 (源端口, 目标端口) 映射为 (dX, dY)
                (double dX, double dY) = (srcSide, tgtSide) switch
                {
                    // 【情况 A：同轴直连】
                    (R, L) => (320, 0),
                    (B, T) => ((srcWidth - tgtWidth) / 2.0, 160),
                    (L, R) => (-320, 0),
                    (T, B) => ((srcWidth - tgtWidth) / 2.0, -160),

                    // 【情况 B：L型正交折线】
                    (B, L) => (280, 120),
                    (B, R) => (-280, 120),

                    (R, T) => (280, 140),
                    (R, B) => (280, -140),

                    (L, T) => (-280, 140),
                    (L, B) => (-280, -140),

                    (T, L) => (280, -120),
                    (T, R) => (-280, -120),

                    // 兜底错开默认值 ( _ 代表 default)
                    _ => (320, 120)
                };

                // 根据锁定状态进行弹射对齐
                if (srcLocked && !tgtLocked)
                {
                    tgt.Location = new Point(src.Location.X + dX, src.Location.Y + dY);
                    adjustedNodes.Add(tgt);
                    changed = true;
                }
                else if (!srcLocked && tgtLocked)
                {
                    src.Location = new Point(tgt.Location.X - dX, tgt.Location.Y - dY);
                    adjustedNodes.Add(src);
                    changed = true;
                }
            }
        }
    }

    private static double GetNodeWidth(FlowNodeViewModel node)
        => node.Size.Width > 0 ? node.Size.Width : 160.0;

    private void ResolveOverlaps(List<FlowNodeViewModel> nodesInLevel, Dictionary<FlowNodeViewModel, double> nodeSecondaryPos, FlowLayoutDirection direction)
    {
        if (nodesInLevel.Count <= 1) return;

        double originalCenterPos = nodesInLevel.Average(n => nodeSecondaryPos[n]);

        for (int i = 1; i < nodesInLevel.Count; i++)
        {
            var prevNode = nodesInLevel[i - 1];
            var currNode = nodesInLevel[i];

            double requiredSpacing = GetEstimatedSize(prevNode, direction) + 20;

            if (nodeSecondaryPos[currNode] < nodeSecondaryPos[prevNode] + requiredSpacing)
            {
                nodeSecondaryPos[currNode] = nodeSecondaryPos[prevNode] + requiredSpacing;
            }
        }

        double newCenterPos = nodesInLevel.Average(n => nodeSecondaryPos[n]);
        double driftOffset = originalCenterPos - newCenterPos;

        foreach (var node in nodesInLevel)
        {
            nodeSecondaryPos[node] += driftOffset;
        }
    }
}

