using HarmonyLib;
using CardDisableControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Modding;

namespace CardDisableControl.Scripts;

[ModInitializer(nameof(Init))]
public partial class Entry
{
    private static Harmony? _harmony;

    public static void Init()
    {
        CardDisableControlLogger.Info("开始初始化 CardDisableControl。");
        CardDisableControlBanState.Initialize();

        _harmony = new Harmony("sts2.carddisablecontrol");
        _harmony.PatchAll();

        CardDisableControlLogger.Info("CardDisableControl 初始化完成。");
    }
}
