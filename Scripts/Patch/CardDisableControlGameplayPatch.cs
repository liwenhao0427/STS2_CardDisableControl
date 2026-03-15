using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Godot;
using CardDisableControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace CardDisableControl.Scripts.Patch;

[HarmonyPatch(typeof(NCardHolder), "OnFocus")]
internal static class CardDisableControlHolderFocusPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCardHolder __instance)
    {
        if (__instance is not NGridCardHolder gridHolder)
        {
            return;
        }

        if (!CardDisableControlUiState.IsCardLibraryHolder(gridHolder))
        {
            return;
        }

        CardDisableControlUiState.SetFocusedCardLibraryHolder(gridHolder);
    }
}

[HarmonyPatch(typeof(NCardHolder), "OnUnfocus")]
internal static class CardDisableControlHolderUnfocusPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCardHolder __instance)
    {
        if (__instance is not NGridCardHolder gridHolder)
        {
            return;
        }

        CardDisableControlUiState.ClearFocusedCardLibraryHolder(gridHolder);
    }
}

[HarmonyPatch(typeof(NGame), nameof(NGame._Input))]
internal static class CardDisableControlHotkeyPatch
{
    [HarmonyPostfix]
    private static void Postfix(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey keyEvent || keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        if (keyEvent.Keycode != Key.B && keyEvent.PhysicalKeycode != Key.B)
        {
            return;
        }

        if (ActiveScreenContext.Instance.GetCurrentScreen() is not NCardLibrary)
        {
            return;
        }

        NGridCardHolder? holder = CardDisableControlUiState.GetFocusedCardLibraryHolder();
        if (holder == null || !GodotObject.IsInstanceValid(holder) || holder.CardModel == null || !CardDisableControlUiState.IsCardLibraryHolder(holder))
        {
            return;
        }

        bool hasFocus = holder.HasFocus() || holder.Hitbox.HasFocus();
        bool isMouseOver = holder.GetGlobalRect().HasPoint(holder.GetViewport().GetMousePosition());
        if (!hasFocus && !isMouseOver)
        {
            return;
        }

        bool isBanned = CardDisableControlBanState.Toggle(holder.CardModel, "总览快捷键 B");
        CardDisableControlLogger.Info($"快捷键 B 已{(isBanned ? "禁用" : "解禁")}卡牌: {CardDisableControlBanState.GetCardKey(holder.CardModel)}");
    }
}

[HarmonyPatch(typeof(NGridCardHolder), nameof(NGridCardHolder._Ready))]
internal static class CardDisableControlOverlayAttachPatch
{
    [HarmonyPostfix]
    private static void Postfix(NGridCardHolder __instance)
    {
        CardDisableControlBanOverlay.EnsureAttached(__instance);
    }
}

[HarmonyPatch(typeof(NInspectCardScreen), nameof(NInspectCardScreen._Ready))]
internal static class CardDisableControlInspectReadyPatch
{
    [HarmonyPostfix]
    private static void Postfix(NInspectCardScreen __instance)
    {
        CardDisableControlInspectToggleController.EnsureAttached(__instance);
        CardDisableControlInspectToggleController.RefreshFor(__instance);
    }
}

[HarmonyPatch(typeof(NInspectCardScreen), "SetCard")]
internal static class CardDisableControlInspectSetCardPatch
{
    [HarmonyPostfix]
    private static void Postfix(NInspectCardScreen __instance)
    {
        CardDisableControlInspectToggleController.RefreshFor(__instance);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyCardRewardCreationOptions))]
internal static class CardDisableControlRewardCreationPatch
{
    [HarmonyPostfix]
    private static void Postfix(Player player, ref CardCreationOptions __result)
    {
        __result = CardDisableControlBanState.ApplyToCreationOptions(__result, player, "奖励/事件卡池");
    }
}

[HarmonyPatch(typeof(CardFactory), nameof(CardFactory.CreateForMerchant), new[] { typeof(Player), typeof(IEnumerable<CardModel>), typeof(CardType) })]
internal static class CardDisableControlMerchantTypePatch
{
    [HarmonyPrefix]
    private static void Prefix(ref IEnumerable<CardModel> options)
    {
        options = CardDisableControlBanState.FilterCardsWithFallback(options, "商店卡池(按类型)");
    }
}

[HarmonyPatch(typeof(CardFactory), nameof(CardFactory.CreateForMerchant), new[] { typeof(Player), typeof(IEnumerable<CardModel>), typeof(CardRarity) })]
internal static class CardDisableControlMerchantRarityPatch
{
    [HarmonyPrefix]
    private static void Prefix(ref IEnumerable<CardModel> options)
    {
        options = CardDisableControlBanState.FilterCardsWithFallback(options, "商店卡池(按稀有度)");
    }
}

[HarmonyPatch(typeof(CardFactory), "GetFilteredTransformationOptions", new[] { typeof(CardModel), typeof(IEnumerable<CardModel>), typeof(bool) })]
internal static class CardDisableControlTransformPoolPatch
{
    [HarmonyPrefix]
    private static void Prefix(bool isInCombat, ref IEnumerable<CardModel> originalOptions)
    {
        if (isInCombat)
        {
            return;
        }

        originalOptions = CardDisableControlBanState.FilterCardsWithFallback(originalOptions, "非战斗转化卡池");
    }
}

