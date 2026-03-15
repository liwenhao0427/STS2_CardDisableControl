using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace CardDisableControl.Scripts.Runtime;

internal partial class CardDisableControlInspectToggleController : Node
{
    private const string ControllerNodeName = "CardDisableControlInspectToggleController";
    private const string BanToggleName = "CardDisableControlInspectBanCheck";
    private const string BanLabelName = "CardDisableControlInspectBanLabel";
    private const float BottomRatio = 0.84f;
    private const float Gap = 4f;

    private static readonly FieldInfo? CardsField = AccessTools.Field(typeof(NInspectCardScreen), "_cards");
    private static readonly FieldInfo? IndexField = AccessTools.Field(typeof(NInspectCardScreen), "_index");

    private NInspectCardScreen? _screen;
    private NCard? _inspectCard;
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

        if (_inspectCard == null || !GodotObject.IsInstanceValid(_inspectCard))
        {
            _inspectCard = _screen.GetNodeOrNull<NCard>("Card");
        }

        if (_inspectCard == null)
        {
            CardDisableControlLogger.Warn("详情页禁用勾选初始化失败：未找到 Card 节点。");
            return;
        }

        if (_banToggle == null)
        {
            _banToggle = new CheckBox
            {
                Name = BanToggleName,
                FocusMode = Control.FocusModeEnum.None,
                MouseFilter = Control.MouseFilterEnum.Stop,
                Size = new Vector2(16f, 16f),
                CustomMinimumSize = new Vector2(16f, 16f)
            };
            _banToggle.Toggled += OnBanToggleChanged;
            _inspectCard.AddChild(_banToggle);
        }

        if (_banLabel == null)
        {
            _banLabel = new Label
            {
                Name = BanLabelName,
                FocusMode = Control.FocusModeEnum.None,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Text = "禁用",
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.Off,
                Size = new Vector2(80f, 16f),
                CustomMinimumSize = new Vector2(80f, 16f),
                Modulate = new Color(0.95f, 0.85f, 0.2f, 1f)
            };
            _inspectCard.AddChild(_banLabel);
        }

        ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (_inspectCard == null || _banToggle == null || _banLabel == null)
        {
            return;
        }

        int fontSize = ResolveDescriptionFontSize();
        float checkSize = fontSize;
        float textWidth = fontSize * 2.2f;

        _banToggle.CustomMinimumSize = new Vector2(checkSize, checkSize);
        _banToggle.Size = _banToggle.CustomMinimumSize;

        _banLabel.AddThemeFontSizeOverride("font_size", fontSize);
        _banLabel.Text = "禁用";
        _banLabel.Size = new Vector2(textWidth, checkSize);

        Vector2 size = _inspectCard.Size;
        float groupWidth = checkSize + Gap + textWidth;
        float x = (size.X - groupWidth) * 0.5f;
        float y = size.Y * BottomRatio;

        _banToggle.Position = new Vector2(x, y);
        _banLabel.Position = new Vector2(x + checkSize + Gap, y);
    }

    private int ResolveDescriptionFontSize()
    {
        if (_inspectCard != null)
        {
            Control? descriptionLabel = _inspectCard.GetNodeOrNull<Control>("%DescriptionLabel");
            if (descriptionLabel != null)
            {
                int size = descriptionLabel.GetThemeFontSize("normal_font_size");
                if (size > 0)
                {
                    return size;
                }

                size = descriptionLabel.GetThemeFontSize("font_size");
                if (size > 0)
                {
                    return size;
                }
            }
        }

        return 16;
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
        bool visible = currentCard != null && _screen.Visible;

        _banToggle.Visible = visible;
        _banLabel.Visible = visible;

        if (!visible)
        {
            return;
        }

        ApplyLayout();

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
