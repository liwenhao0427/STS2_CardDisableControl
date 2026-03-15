using HarmonyLib;
using CardDisableControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Runs;

namespace CardDisableControl.Scripts.Patch;

[HarmonyPatch]
internal static class CardDisableControlPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.Launch))]
    private static void PostfixRunManagerLaunch()
    {
        CardDisableControlLogger.Info("检测到 Run 启动，卡牌禁用控制补丁已激活。");
    }
}
