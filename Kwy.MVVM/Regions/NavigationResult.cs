namespace Kwy.MVVM.Regions;

/// <summary>
/// 导航操作的结果。使用 record 提供不可变性和极简语法。
/// </summary>
public record NavigationResult(bool Result, Exception? Error = null, NavigationContext? Context = null)
{
    // 成功时的快捷入口
    public static NavigationResult Success(NavigationContext context)
        => new(true, null, context);

    // 失败时的快捷入口，对应你报错的地方
    public static NavigationResult Failure(Exception error, NavigationContext? context = null)
        => new(false, error, context);
}