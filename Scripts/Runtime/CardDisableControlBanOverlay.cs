using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace CardDisableControl.Scripts.Runtime;

internal partial class CardDisableControlBanOverlay : Control
{
    public const string OverlayNodeName = "CardDisableControlBanOverlay";

    private const float CheckSize = 20f;
    private const float Margin = 4f;
    private const float LabelWidth = 60f;

    private NGridCardHolder? _holder;
    private CheckBox? _banCheckBox;
    private Label? _banLabel;
    private bool _isSyncingUi;

    private bool _lastVisible;
    private bool _lastBanned;
    private Vector2 _lastSize = Vector2.Zero;
    private string? _lastCardKey;

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
        CardDisableControlLogger.Info($"已为总览卡牌挂载禁用勾选层: {holder.Name}");
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = 0f;
        OffsetTop = 0f;
        OffsetRight = 0f;
        OffsetBottom = 0f;
        ZIndex = 150;

        EnsureControls();
        CardDisableControlBanState.BanStateChanged += OnBanStateChanged;
        SetProcess(true);
    }

    public override void _ExitTree()
    {
        CardDisableControlBanState.BanStateChanged -= OnBanStateChanged;

        if (_banCheckBox != null)
        {
            _banCheckBox.Toggled -= OnCheckBoxToggled;
        }

        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        RefreshUi();
    }

    private void EnsureControls()
    {
        if (_banCheckBox == null)
        {
            _banCheckBox = new CheckBox
            {
                Name = "CardDisableControlGridBanCheck",
                FocusMode = FocusModeEnum.None,
                MouseFilter = MouseFilterEnum.Stop,
                Size = new Vector2(CheckSize, CheckSize),
                CustomMinimumSize = new Vector2(CheckSize, CheckSize)
            };
            _banCheckBox.Toggled += OnCheckBoxToggled;
            AddChild(_banCheckBox);
        }

        if (_banLabel == null)
        {
            _banLabel = new Label
            {
                Name = "CardDisableControlGridBanLabel",
                MouseFilter = MouseFilterEnum.Ignore,
                FocusMode = FocusModeEnum.None,
                Text = "禁用卡牌",
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.Off,
                Size = new Vector2(LabelWidth, CheckSize),
                CustomMinimumSize = new Vector2(LabelWidth, CheckSize)
            };
            AddChild(_banLabel);
        }
    }

    private void RefreshUi()
    {
        if (_banCheckBox == null || _banLabel == null)
        {
            return;
        }

        string? currentCardKey = CardDisableControlBanState.GetCardKey(_holder?.CardModel);
        bool visible = ShouldShowAction();
        bool isBanned = visible && CardDisableControlBanState.IsBanned(_holder?.CardModel);
        Vector2 currentSize = Size;

        if (_lastVisible == visible &&
            _lastBanned == isBanned &&
            currentSize == _lastSize &&
            string.Equals(_lastCardKey, currentCardKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _lastVisible = visible;
        _lastBanned = isBanned;
        _lastSize = currentSize;
        _lastCardKey = currentCardKey;

        _banCheckBox.Visible = visible;
        _banLabel.Visible = visible;

        if (!visible)
        {
            return;
        }

        float groupWidth = CheckSize + 4f + LabelWidth;
        float left = Math.Max(0f, Size.X - groupWidth - Margin);

        _banCheckBox.Position = new Vector2(left, Margin);
        _banLabel.Position = new Vector2(left + CheckSize + 4f, Margin);

        if (_banCheckBox.ButtonPressed != isBanned)
        {
            _isSyncingUi = true;
            _banCheckBox.ButtonPressed = isBanned;
            _isSyncingUi = false;
        }
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

        return Size.X >= 60f && Size.Y >= 30f;
    }

    private void OnCheckBoxToggled(bool pressed)
    {
        if (_isSyncingUi)
        {
            return;
        }

        if (_holder == null || !GodotObject.IsInstanceValid(_holder) || _holder.CardModel == null)
        {
            return;
        }

        CardDisableControlBanState.SetBanned(_holder.CardModel, pressed, "总览勾选");
    }

    private void OnBanStateChanged(string _, bool __)
    {
        RefreshUi();
    }
}
