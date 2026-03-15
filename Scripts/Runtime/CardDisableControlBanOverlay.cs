using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace CardDisableControl.Scripts.Runtime;

internal partial class CardDisableControlBanOverlay : Control
{
    public const string OverlayNodeName = "CardDisableControlBanOverlay";

    private const float BottomRatio = 0.84f;
    private const float Gap = 4f;

    private NGridCardHolder? _holder;
    private CheckBox? _banCheckBox;
    private Label? _banLabel;
    private bool _isSyncingUi;

    private bool _lastVisible;
    private bool _lastBanned;
    private Vector2 _lastSize = Vector2.Zero;
    private string? _lastCardKey;
    private bool _hasLoggedProbe;

    public static void EnsureAttached(NGridCardHolder holder)
    {
        Node parent;
        if (holder.CardNode != null)
        {
            parent = holder.CardNode;
        }
        else if (holder.Hitbox != null)
        {
            parent = holder.Hitbox;
        }
        else
        {
            parent = holder;
        }
        if (parent.GetNodeOrNull<CardDisableControlBanOverlay>(OverlayNodeName) != null)
        {
            return;
        }

        CardDisableControlBanOverlay overlay = new()
        {
            Name = OverlayNodeName,
            _holder = holder
        };

        parent.AddChild(overlay);
        CardDisableControlLogger.Info($"已为总览卡牌挂载禁用勾选层: {holder.Name}, parent={parent.Name}");
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = 0f;
        OffsetTop = 0f;
        OffsetRight = 0f;
        OffsetBottom = 0f;
        ZIndex = 200;

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
                Size = new Vector2(14f, 14f),
                CustomMinimumSize = new Vector2(14f, 14f)
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
                Text = "禁用",
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.Off,
                Size = new Vector2(38f, 14f),
                CustomMinimumSize = new Vector2(38f, 14f),
                Modulate = new Color(0.95f, 0.85f, 0.2f, 1f)
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
        bool visible = ShouldShowAction(out string hiddenReason);
        bool isBanned = visible && CardDisableControlBanState.IsBanned(_holder?.CardModel);
        Vector2 currentSize = GetActionAreaSize();

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

        if (!_hasLoggedProbe || !visible)
        {
            _hasLoggedProbe = true;
            if (visible)
            {
                CardDisableControlLogger.Info($"总览勾选可见: card={currentCardKey}, area={currentSize}");
            }
            else
            {
                CardDisableControlLogger.Info($"总览勾选隐藏: card={currentCardKey}, reason={hiddenReason}, area={currentSize}");
            }
        }

        if (!visible)
        {
            return;
        }

        int fontSize = ResolveDescriptionFontSize();
        float textWidth = fontSize * 2.2f;
        float checkSize = fontSize;

        _banCheckBox.CustomMinimumSize = new Vector2(checkSize, checkSize);
        _banCheckBox.Size = _banCheckBox.CustomMinimumSize;
        _banLabel.AddThemeFontSizeOverride("font_size", fontSize);
        _banLabel.Text = "禁用";
        _banLabel.Size = new Vector2(textWidth, checkSize);

        float groupWidth = checkSize + Gap + textWidth;
        float x = (currentSize.X - groupWidth) * 0.5f;
        float y = currentSize.Y * BottomRatio;

        _banCheckBox.Position = new Vector2(x, y);
        _banLabel.Position = new Vector2(x + checkSize + Gap, y);

        if (_banCheckBox.ButtonPressed != isBanned)
        {
            _isSyncingUi = true;
            _banCheckBox.ButtonPressed = isBanned;
            _isSyncingUi = false;
        }
    }

    private int ResolveDescriptionFontSize()
    {
        Control? descriptionLabel = _holder?.CardNode?.GetNodeOrNull<Control>("%DescriptionLabel");
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

        return 14;
    }

    private Vector2 GetActionAreaSize()
    {
        if (Size.X > 0f && Size.Y > 0f)
        {
            return Size;
        }

        if (_holder?.CardNode != null && GodotObject.IsInstanceValid(_holder.CardNode))
        {
            return _holder.CardNode.Size;
        }

        if (_holder?.Hitbox != null && GodotObject.IsInstanceValid(_holder.Hitbox))
        {
            return _holder.Hitbox.Size;
        }

        return Size;
    }

    private bool ShouldShowAction(out string reason)
    {
        if (_holder == null || !GodotObject.IsInstanceValid(_holder))
        {
            reason = "holder_invalid";
            return false;
        }

        if (NGame.Instance?.InspectCardScreen != null && NGame.Instance.InspectCardScreen.Visible)
        {
            reason = "inspect_opened";
            return false;
        }

        if (!CardDisableControlUiState.IsCardLibraryHolder(_holder))
        {
            reason = "not_card_library";
            return false;
        }

        if (_holder.CardModel == null)
        {
            reason = "card_model_null";
            return false;
        }

        if (_holder.CardNode == null || _holder.CardNode.Visibility != ModelVisibility.Visible)
        {
            reason = "card_node_not_visible";
            return false;
        }

        Vector2 area = GetActionAreaSize();
        if (area.X < 120f || area.Y < 120f)
        {
            reason = "area_too_small";
            return false;
        }

        reason = "ok";
        return true;
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
