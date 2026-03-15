using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace CardDisableControl.Scripts.Runtime;

internal partial class CardDisableControlInspectToggleController : Node
{
    private const string ControllerNodeName = "CardDisableControlInspectToggleController";
    private const string BanToggleContainerName = "CardDisableControlBanToggleContainer";

    private static readonly FieldInfo? UpgradeTickboxField = AccessTools.Field(typeof(NInspectCardScreen), "_upgradeTickbox");
    private static readonly FieldInfo? CardsField = AccessTools.Field(typeof(NInspectCardScreen), "_cards");
    private static readonly FieldInfo? IndexField = AccessTools.Field(typeof(NInspectCardScreen), "_index");

    private NInspectCardScreen? _screen;
    private HBoxContainer? _container;
    private CheckBox? _banToggle;
    private Label? _banLabel;
    private bool _isSyncingUi;

    public static void EnsureAttached(NInspectCardScreen screen)
    {
        if (screen.GetNodeOrNull<CardDisableControlInspectToggleController>(ControllerNodeName) != null)
        {
            return;
        }

        CardDisableControlInspectToggleController controller = new()
        {
            Name = ControllerNodeName,
            _screen = screen
        };

        screen.AddChild(controller);
    }

    public static void RefreshFor(NInspectCardScreen screen)
    {
        CardDisableControlInspectToggleController? controller = screen.GetNodeOrNull<CardDisableControlInspectToggleController>(ControllerNodeName);
        controller?.RefreshUi();
    }

    public override void _EnterTree()
    {
        CardDisableControlBanState.BanStateChanged += OnBanStateChanged;
        CallDeferred(nameof(EnsureUi));
    }

    public override void _ExitTree()
    {
        CardDisableControlBanState.BanStateChanged -= OnBanStateChanged;
        base._ExitTree();
    }

    private void EnsureUi()
    {
        if (_screen == null || !GodotObject.IsInstanceValid(_screen) || _container != null)
        {
            return;
        }

        NTickbox? upgradeTickbox = UpgradeTickboxField?.GetValue(_screen) as NTickbox;
        if (upgradeTickbox == null)
        {
            CardDisableControlLogger.Warn("详情页禁用勾选初始化失败：未找到升级勾选节点。");
            return;
        }

        if (upgradeTickbox.GetParent() is not Control parent)
        {
            CardDisableControlLogger.Warn("详情页禁用勾选初始化失败：升级勾选父节点无效。");
            return;
        }

        _container = new HBoxContainer
        {
            Name = BanToggleContainerName,
            Position = upgradeTickbox.Position + new Vector2(220f, 0f),
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        _container.AddThemeConstantOverride("separation", 6);

        _banToggle = new CheckBox
        {
            ButtonPressed = false,
            FocusMode = Control.FocusModeEnum.All,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = "勾选后该卡牌不会进入随机奖励池。"
        };
        _banToggle.Toggled += OnBanToggleChanged;

        _banLabel = new Label
        {
            Text = "禁用卡牌",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        _container.AddChild(_banToggle);
        _container.AddChild(_banLabel);
        parent.AddChild(_container);

        RefreshUi();
    }

    private void RefreshUi()
    {
        if (_screen == null || !GodotObject.IsInstanceValid(_screen))
        {
            return;
        }

        EnsureUi();
        if (_container == null || _banToggle == null)
        {
            return;
        }

        CardModel? currentCard = GetCurrentCard();
        _container.Visible = currentCard != null;

        if (currentCard == null)
        {
            return;
        }

        bool isBanned = CardDisableControlBanState.IsBanned(currentCard);
        if (_banToggle.ButtonPressed != isBanned)
        {
            _isSyncingUi = true;
            _banToggle.ButtonPressed = isBanned;
            _isSyncingUi = false;
        }
    }

    private CardModel? GetCurrentCard()
    {
        if (_screen == null)
        {
            return null;
        }

        List<CardModel>? cards = CardsField?.GetValue(_screen) as List<CardModel>;
        if (cards == null || cards.Count == 0)
        {
            return null;
        }

        if (IndexField?.GetValue(_screen) is not int index || index < 0 || index >= cards.Count)
        {
            return null;
        }

        return cards[index];
    }

    private void OnBanToggleChanged(bool isPressed)
    {
        if (_isSyncingUi)
        {
            return;
        }

        CardModel? card = GetCurrentCard();
        if (card == null)
        {
            return;
        }

        CardDisableControlBanState.SetBanned(card, isPressed, "详情页勾选");
    }

    private void OnBanStateChanged(string key, bool _)
    {
        CardModel? card = GetCurrentCard();
        if (card == null)
        {
            return;
        }

        string? cardKey = CardDisableControlBanState.GetCardKey(card);
        if (!string.Equals(cardKey, key, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RefreshUi();
    }
}

