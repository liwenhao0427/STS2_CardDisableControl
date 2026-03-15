using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace CardDisableControl.Scripts.Runtime;

internal partial class CardDisableControlBanOverlay : Control
{
    public const string OverlayNodeName = "CardDisableControlBanOverlay";
    private const float IconSize = 30f;
    private const float IconMargin = 6f;

    private NGridCardHolder? _holder;
    private TextureButton? _actionButton;
    private bool _isActionVisible;
    private bool _isBanned;
    private string? _lastCardKey;
    private Vector2 _lastSize = Vector2.Zero;

    private static Texture2D? _disableIcon;
    private static Texture2D? _restoreIcon;

    public static void EnsureAttached(NGridCardHolder holder)
    {
        if (holder.GetNodeOrNull<CardDisableControlBanOverlay>(OverlayNodeName) != null)
        {
            return;
        }

        CardDisableControlBanOverlay overlay = new()
        {
            Name = OverlayNodeName,
            _holder = holder
        };

        holder.AddChild(overlay);
        CardDisableControlLogger.Info($"已为总览卡牌挂载操作图标层: {holder.Name}");
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = 0f;
        OffsetTop = 0f;
        OffsetRight = 0f;
        OffsetBottom = 0f;
        ZIndex = 150;

        EnsureIconsLoaded();
        CreateActionButton();

        CardDisableControlBanState.BanStateChanged += OnBanStateChanged;
        SetProcess(true);
    }

    public override void _ExitTree()
    {
        CardDisableControlBanState.BanStateChanged -= OnBanStateChanged;

        if (_actionButton != null)
        {
            _actionButton.Pressed -= OnActionPressed;
        }

        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        RefreshButtonState();
    }

    private void EnsureIconsLoaded()
    {
        _disableIcon ??= ModelDb.Relic<PreciseScissors>().Icon;
        _restoreIcon ??= ModelDb.Relic<TinyMailbox>().Icon;
    }

    private void CreateActionButton()
    {
        if (_actionButton != null)
        {
            return;
        }

        _actionButton = new TextureButton
        {
            Name = "CardDisableControlActionIcon",
            MouseFilter = MouseFilterEnum.Stop,
            FocusMode = FocusModeEnum.None,
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
            Size = new Vector2(IconSize, IconSize),
            CustomMinimumSize = new Vector2(IconSize, IconSize),
            Visible = false
        };

        _actionButton.Pressed += OnActionPressed;
        AddChild(_actionButton);
    }

    private void RefreshButtonState()
    {
        if (_actionButton == null)
        {
            return;
        }

        string? currentCardKey = CardDisableControlBanState.GetCardKey(_holder?.CardModel);
        bool shouldShow = ShouldShowAction();
        bool isBanned = shouldShow && CardDisableControlBanState.IsBanned(_holder?.CardModel);
        Vector2 currentSize = Size;

        if (_isActionVisible == shouldShow &&
            _isBanned == isBanned &&
            string.Equals(_lastCardKey, currentCardKey, StringComparison.OrdinalIgnoreCase) &&
            currentSize == _lastSize)
        {
            return;
        }

        _isActionVisible = shouldShow;
        _isBanned = isBanned;
        _lastCardKey = currentCardKey;
        _lastSize = currentSize;

        _actionButton.Visible = shouldShow;
        if (!shouldShow)
        {
            return;
        }

        Texture2D? icon = isBanned ? _restoreIcon : _disableIcon;
        _actionButton.TextureNormal = icon;
        _actionButton.TextureHover = icon;
        _actionButton.TexturePressed = icon;
        _actionButton.TextureDisabled = icon;
        _actionButton.Position = new Vector2(Math.Max(0f, Size.X - IconSize - IconMargin), IconMargin);
    }

    private bool ShouldShowAction()
    {
        if (_holder == null || !GodotObject.IsInstanceValid(_holder))
        {
            return false;
        }

        if (!CardDisableControlUiState.IsCardLibraryHolder(_holder))
        {
            return false;
        }

        if (_holder.CardModel == null)
        {
            return false;
        }

        if (_holder.CardNode == null || _holder.CardNode.Visibility != ModelVisibility.Visible)
        {
            return false;
        }

        return Size.X >= 30f && Size.Y >= 30f;
    }

    private void OnActionPressed()
    {
        if (_holder == null || !GodotObject.IsInstanceValid(_holder) || _holder.CardModel == null)
        {
            return;
        }

        bool nextBanned = !CardDisableControlBanState.IsBanned(_holder.CardModel);
        CardDisableControlBanState.SetBanned(_holder.CardModel, nextBanned, "总览图标");
    }

    private void OnBanStateChanged(string _, bool __)
    {
        RefreshButtonState();
    }
}
