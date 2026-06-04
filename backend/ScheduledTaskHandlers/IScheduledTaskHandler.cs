namespace backend.ScheduledTaskHandlers;

/// <summary>
/// 计划任务执行器参数元数据
/// </summary>
public class HandlerParameterMeta
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public string DefaultValue { get; set; } = "";
}

/// <summary>
/// 计划任务执行器接口 — 实现此接口后，在前端配置全路径类名即可被计划任务调度。
/// 前端「执行参数」以 Key-Value 形式配置，通过 parameters 字典传入。
/// </summary>
public interface IScheduledTaskHandler
{
    /// <summary>
    /// 执行计划任务
    /// </summary>
    /// <param name="services">DI 容器，可获取 AppDbContext 等已注册服务</param>
    /// <param name="parameters">前端配置的参数（Key → Value），可能为空字典</param>
    /// <param name="cancellation">取消令牌</param>
    /// <returns>执行结果消息</returns>
    Task<string> ExecuteAsync(IServiceProvider services, Dictionary<string, string?> parameters, CancellationToken cancellation);

    /// <summary>
    /// 返回该执行器支持的参数列表、说明和默认值。不支持的返回空列表即可。
    /// </summary>
    List<HandlerParameterMeta> GetParameterMetas() => new();
}
