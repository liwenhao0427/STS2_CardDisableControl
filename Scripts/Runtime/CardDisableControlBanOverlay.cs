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

    private const float BottomPadding = 6f;
    // 预览勾选位置微调：负值=向左/向上
    private const float PositionOffsetX = -18f;
    private const float PositionOffsetY = -28f;
    private const string HintDefault = "鼠标中键点击禁用";
    private const string HintBanned = "鼠标中键取消禁用";

    private NGridCardHolder? _holder;
    private ColorRect? _banBackground;
    private Label? _banLabel;

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

        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        RefreshUi();
    }

    private void EnsureControls()
    {
        if (_banBackground == null)
        {
            _banBackground = new ColorRect
            {
                Name = "CardDisableControlGridBanBackground",
                FocusMode = FocusModeEnum.None,
                MouseFilter = MouseFilterEnum.Ignore,
                Color = new Color(0f, 0f, 0f, 0.35f)
            };
            AddChild(_banBackground);
        }

        if (_banLabel == null)
        {
            _banLabel = new Label
            {
                Name = "CardDisableControlGridBanLabel",
                MouseFilter = MouseFilterEnum.Ignore,
                FocusMode = FocusModeEnum.None,
                Text = HintDefault,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.Off,
                Size = new Vector2(120f, 14f),
                CustomMinimumSize = new Vector2(120f, 14f),
                Modulate = new Color(0.95f, 0.85f, 0.2f, 1f)
            };
            AddChild(_banLabel);
        }
    }

    private void RefreshUi()
    {
        if (_banLabel == null || _banBackground == null)
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

        _banLabel.Visible = visible;
        _banBackground.Visible = visible;

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
        string labelText = isBanned ? HintBanned : HintDefault;
        float labelHeight = fontSize;
        float textWidth = fontSize * 8.5f;

        _banLabel.AddThemeFontSizeOverride("font_size", fontSize);
        _banLabel.Text = labelText;
        _banLabel.Modulate = isBanned
            ? new Color(0.95f, 0.35f, 0.25f, 1f)
            : new Color(0.95f, 0.85f, 0.2f, 1f);
        _banLabel.Size = new Vector2(textWidth, labelHeight);

        float groupWidth = textWidth;
        float globalX = cardGlobalRect.Position.X + (cardGlobalRect.Size.X - groupWidth) * 0.5f + PositionOffsetX;
        float globalY = cardGlobalRect.Position.Y + cardGlobalRect.Size.Y + BottomPadding + PositionOffsetY;
        Vector2 localGroupPos = GlobalToParentLocal(new Vector2(globalX, globalY));

        Position = localGroupPos;
        Size = new Vector2(groupWidth, labelHeight);
        _banBackground.Position = new Vector2(-2f, -1f);
        _banBackground.Size = new Vector2(groupWidth + 4f, labelHeight + 2f);
        _banLabel.Position = Vector2.Zero;
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
        // 总览定位优先使用 Hitbox（更贴近网格中卡牌实际显示与命中区域）
        if (_holder?.Hitbox != null && GodotObject.IsInstanceValid(_holder.Hitbox))
        {
            return new Rect2(_holder.Hitbox.GetGlobalTransformWithCanvas().Origin, _holder.Hitbox.Size);
        }

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

            return new Rect2(origin, size);
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


