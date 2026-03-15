using MegaCrit.Sts2.Core.Logging;

namespace CardDisableControl.Scripts.Runtime;

internal static class CardDisableControlLogger
{
    private const string Prefix = "[CardDisableControl]";

    public static void Info(string message)
    {
        Log.Info($"{Prefix} {message}");
    }

    public static void Warn(string message)
    {
        Log.Warn($"{Prefix} {message}");
    }

    public static void Error(string message)
    {
        Log.Error($"{Prefix} {message}");
    }
}
