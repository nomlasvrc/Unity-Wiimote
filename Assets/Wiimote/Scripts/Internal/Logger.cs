namespace WiimoteApi;

/// <summary>Legacy logging facade. Subscribe to <see cref="WiimoteManager.LogMessage"/> instead.</summary>
public abstract class Logger
{
    protected static void Log(object? message) =>
        WiimoteManager.Report(WiimoteLogLevel.Debug, Convert.ToString(message) ?? string.Empty);

    protected static void LogWarning(object? message) =>
        WiimoteManager.Report(WiimoteLogLevel.Warning, Convert.ToString(message) ?? string.Empty);

    protected static void LogError(object? message) =>
        WiimoteManager.Report(WiimoteLogLevel.Error, Convert.ToString(message) ?? string.Empty);
}
