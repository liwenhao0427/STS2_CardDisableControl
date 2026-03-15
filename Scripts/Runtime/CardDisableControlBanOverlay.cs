using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace CardDisableControl.Scripts.Runtime;

internal partial class CardDisableControlBanOverlay : Control
{
    public const string OverlayNodeName = "CardDisableControlBanOverlay";

    private const float BottomPadding = 10f;
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
        Node parent = holder.GetParent() ?? holder;
        string overlayNodeName = $"{OverlayNodeName}_{holder.GetInstanceId()}";
        if (parent.GetNodeOrNull<CardDisableControlBanOverlay>(overlayNodeName) != null)
        {
            return;
        }

        CardDisableControlBanOverlay overlay = new()
        {
            Name = overlayNodeName,
            _holder = holder
        };

        parent.AddChild(overlay);
        CardDisableControlLogger.Info($"已为总览卡牌挂载禁用勾选层: {holder.Name}, parent={parent.Name}");
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.TopLeft);
        Position = Vector2.Zero;
        Size = Vector2.Zero;
        ZIndex = 500;

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
                MouseFilter = MouseFilterEnum.Pass,
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
        Rect2 cardGlobalRect = GetCardGlobalRect();
        Vector2 currentSize = cardGlobalRect.Size;

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
        float globalX = cardGlobalRect.Position.X + (cardGlobalRect.Size.X - groupWidth) * 0.5f;
        float globalY = cardGlobalRect.Position.Y + cardGlobalRect.Size.Y + BottomPadding;
        Vector2 localGroupPos = GlobalToParentLocal(new Vector2(globalX, globalY));

        Position = localGroupPos;
        Size = new Vector2(groupWidth, checkSize);
        _banCheckBox.Position = Vector2.Zero;
        _banLabel.Position = new Vector2(checkSize + Gap, 0f);

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

    private Rect2 GetCardGlobalRect()
    {
        if (_holder?.CardNode != null && GodotObject.IsInstanceValid(_holder.CardNode))
        {
            Vector2 origin = _holder.CardNode.GetGlobalTransformWithCanvas().Origin;
            Vector2 size = Vector2.Zero;

            if (_holder.CardNode is NCard nCard && GodotObject.IsInstanceValid(nCard))
            {
                size = nCard.GetCurrentSize();
            }

            if ((size.X <= 1f || size.Y <= 1f) && _holder.CardNode is Control cardControl)
            {
                size = cardControl.Size;
            }

            if ((size.X <= 1f || size.Y <= 1f) &&
                _holder.Hitbox != null &&
                GodotObject.IsInstanceValid(_holder.Hitbox))
            {
                size = _holder.Hitbox.Size;
            }

            return new Rect2(origin, size);
        }

        if (_holder?.Hitbox != null && GodotObject.IsInstanceValid(_holder.Hitbox))
        {
            return new Rect2(_holder.Hitbox.GetGlobalTransformWithCanvas().Origin, _holder.Hitbox.Size);
        }

        return new Rect2(Vector2.Zero, Vector2.Zero);
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

        Rect2 cardGlobalRect = GetCardGlobalRect();
        if (cardGlobalRect.Size.X <= 1f || cardGlobalRect.Size.Y <= 1f)
        {
            reason = "card_rect_unavailable";
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

    private Vector2 GlobalToParentLocal(Vector2 globalPosition)
    {
        if (GetParent() is not CanvasItem parentCanvas)
        {
            return globalPosition;
        }

        Transform2D inverse = parentCanvas.GetGlobalTransformWithCanvas().AffineInverse();
        return inverse * globalPosition;
    }

    private void OnBanStateChanged(string _, bool __)
    {
        RefreshUi();
    }
}


