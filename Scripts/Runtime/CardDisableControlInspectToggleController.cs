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
    private const string BanToggleName = "CardDisableControlBanCheckbox";
    private const string BanLabelName = "CardDisableControlBanLabel";
    private const float SideOffset = 180f;

    private static readonly FieldInfo? UpgradeTickboxField = AccessTools.Field(typeof(NInspectCardScreen), "_upgradeTickbox");
    private static readonly FieldInfo? CardsField = AccessTools.Field(typeof(NInspectCardScreen), "_cards");
    private static readonly FieldInfo? IndexField = AccessTools.Field(typeof(NInspectCardScreen), "_index");

    private NInspectCardScreen? _screen;
    private NTickbox? _upgradeTickbox;
    private Control? _upgradeLabel;
    private CheckBox? _banToggle;
    private Control? _banLabel;

    private bool _isSyncingUi;
    private bool _layoutInitialized;
    private bool _styleAppliedLogged;
    private Vector2 _upgradeTickboxBasePos;
    private Vector2 _upgradeLabelBasePos;

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
        controller?.CallDeferred(nameof(RefreshUi));
    }

    public override void _EnterTree()
    {
        CardDisableControlBanState.BanStateChanged += OnBanStateChanged;
        CallDeferred(nameof(RefreshUi));
    }

    public override void _ExitTree()
    {
        CardDisableControlBanState.BanStateChanged -= OnBanStateChanged;

        if (_banToggle != null)
        {
            _banToggle.Toggled -= OnBanToggleChanged;
        }

        base._ExitTree();
    }

    private void EnsureUi()
    {
        if (_screen == null || !GodotObject.IsInstanceValid(_screen))
        {
            return;
        }

        ResetInvalidReferences();

        _upgradeTickbox ??= UpgradeTickboxField?.GetValue(_screen) as NTickbox;
        _upgradeLabel ??= _screen.GetNodeOrNull<Control>("%ShowUpgradeLabel");
        if (_upgradeTickbox == null || _upgradeLabel == null)
        {
            CardDisableControlLogger.Warn("详情页禁用勾选初始化失败：未找到“查看升级”控件。");
            return;
        }

        if (!_layoutInitialized)
        {
            _layoutInitialized = true;
            _upgradeTickboxBasePos = _upgradeTickbox.Position;
            _upgradeLabelBasePos = _upgradeLabel.Position;
        }

        if (_banToggle == null)
        {
            Node? tickboxParent = _upgradeTickbox.GetParent();
            if (tickboxParent == null)
            {
                CardDisableControlLogger.Warn("详情页禁用勾选初始化失败：升级勾选父节点为空。");
                return;
            }

            _banToggle = new CheckBox
            {
                Name = BanToggleName,
                FocusMode = Control.FocusModeEnum.None,
                MouseFilter = Control.MouseFilterEnum.Stop,
                Size = _upgradeTickbox.Size,
                CustomMinimumSize = _upgradeTickbox.CustomMinimumSize,
                Scale = _upgradeTickbox.Scale,
                Theme = _upgradeTickbox.Theme,
                ThemeTypeVariation = _upgradeTickbox.ThemeTypeVariation,
                Modulate = _upgradeTickbox.Modulate
            };

            _banToggle.Toggled += OnBanToggleChanged;
            tickboxParent.AddChild(_banToggle);
        }

        if (_banLabel == null)
        {
            Node labelCopy = _upgradeLabel.Duplicate((int)(Node.DuplicateFlags.Scripts | Node.DuplicateFlags.UseInstantiation));
            if (labelCopy is not Control banLabel)
            {
                CardDisableControlLogger.Warn("详情页禁用勾选初始化失败：复制升级标签节点失败。");
                return;
            }

            Node? labelParent = _upgradeLabel.GetParent();
            if (labelParent == null)
            {
                CardDisableControlLogger.Warn("详情页禁用勾选初始化失败：升级标签父节点为空。");
                return;
            }

            _banLabel = banLabel;
            _banLabel.Name = BanLabelName;
            SetLabelText(_banLabel, "禁用卡牌");
            labelParent.AddChild(_banLabel);
        }

        ApplyLayout();

        if (!_styleAppliedLogged)
        {
            _styleAppliedLogged = true;
            CardDisableControlLogger.Info("详情页禁用勾选已应用：左侧查看升级，右侧禁用卡牌。");
        }
    }

    private void SetLabelText(Control label, string text)
    {
        if (label.HasMethod("SetTextAutoSize"))
        {
            label.Call("SetTextAutoSize", text);
        }

        if (label.HasMethod("SetText"))
        {
            label.Call("SetText", text);
        }

        if (label.HasMethod("SetPlaintext"))
        {
            label.Call("SetPlaintext", text);
        }

        label.Set("text", text);
    }

    private void ResetInvalidReferences()
    {
        if (_upgradeTickbox != null && !GodotObject.IsInstanceValid(_upgradeTickbox))
        {
            _upgradeTickbox = null;
        }

        if (_upgradeLabel != null && !GodotObject.IsInstanceValid(_upgradeLabel))
        {
            _upgradeLabel = null;
        }

        if (_banToggle != null && !GodotObject.IsInstanceValid(_banToggle))
        {
            _banToggle = null;
        }

        if (_banLabel != null && !GodotObject.IsInstanceValid(_banLabel))
        {
            _banLabel = null;
        }
    }

    private void ApplyLayout()
    {
        if (_upgradeTickbox == null || _upgradeLabel == null || _banToggle == null || _banLabel == null)
        {
            return;
        }

        _upgradeTickbox.Position = _upgradeTickboxBasePos + Vector2.Left * SideOffset;
        _upgradeLabel.Position = _upgradeLabelBasePos + Vector2.Left * SideOffset;
        _banToggle.Position = _upgradeTickboxBasePos + Vector2.Right * SideOffset;
        _banLabel.Position = _upgradeLabelBasePos + Vector2.Right * SideOffset;
    }

    private void RefreshUi()
    {
        if (_screen == null || !GodotObject.IsInstanceValid(_screen))
        {
            return;
        }

        EnsureUi();
        if (_banToggle == null || _banLabel == null)
        {
            return;
        }

        CardModel? currentCard = GetCurrentCard();
        bool visible = currentCard != null;

        _banToggle.Visible = visible;
        _banLabel.Visible = visible;

        if (!visible)
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

    private void OnBanToggleChanged(bool pressed)
    {
        if (_isSyncingUi)
        {
            return;
        }

        CardModel? card = GetCurrentCard();
        if (card == null)
        {
            CardDisableControlLogger.Warn("点击详情页禁用勾选失败：当前卡牌为空。");
            return;
        }

        CardDisableControlBanState.SetBanned(card, pressed, "详情页勾选");
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

        CallDeferred(nameof(RefreshUi));
    }
}
