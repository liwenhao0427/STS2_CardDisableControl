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

    private const float BottomPadding = 8f;

    private NGridCardHolder? _holder;
    private Label? _banLabel;

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
        CardDisableControlLogger.Info($"已为总览卡牌挂载禁用标记层: {holder.Name}, parent={parent.Name}");
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = 0f;
        OffsetTop = 0f;
        OffsetRight = 0f;
        OffsetBottom = 0f;
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
        if (_banLabel == null)
        {
            _banLabel = new Label
            {
                Name = "CardDisableControlGridBanLabel",
                MouseFilter = MouseFilterEnum.Ignore,
                FocusMode = FocusModeEnum.None,
                Text = "禁用中",
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.Off,
                Size = new Vector2(52f, 14f),
                CustomMinimumSize = new Vector2(52f, 14f),
                Modulate = new Color(0.95f, 0.25f, 0.22f, 1f)
            };
            AddChild(_banLabel);
        }
    }

    private void RefreshUi()
    {
        if (_banLabel == null)
        {
            return;
        }

        string? currentCardKey = CardDisableControlBanState.GetCardKey(_holder?.CardModel);
        bool canShow = ShouldShowAction(out string hiddenReason);
        bool isBanned = canShow && CardDisableControlBanState.IsBanned(_holder?.CardModel);
        bool visible = isBanned;
        Rect2 descriptionRect = GetDescriptionRect();
        Vector2 currentSize = descriptionRect.Size;

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

        if (!_hasLoggedProbe || !visible)
        {
            _hasLoggedProbe = true;
            if (visible)
            {
                CardDisableControlLogger.Info($"总览禁用标记可见: card={currentCardKey}, area={currentSize}");
            }
            else
            {
                CardDisableControlLogger.Info($"总览禁用标记隐藏: card={currentCardKey}, reason={hiddenReason}, area={currentSize}");
            }
        }

        if (!visible)
        {
            return;
        }

        int fontSize = ResolveDescriptionFontSize();
        float textWidth = fontSize * 3.8f;
        float textHeight = fontSize;
        _banLabel.AddThemeFontSizeOverride("font_size", fontSize);
        _banLabel.Text = "禁用中";
        _banLabel.Modulate = new Color(0.95f, 0.25f, 0.22f, 1f);
        _banLabel.Size = new Vector2(textWidth, textHeight);

        float x = descriptionRect.Position.X + (descriptionRect.Size.X - textWidth) * 0.5f;
        float y = descriptionRect.Position.Y + descriptionRect.Size.Y - textHeight - BottomPadding;
        _banLabel.Position = new Vector2(x, y);
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

    private Rect2 GetDescriptionRect()
    {
        Control? descriptionLabel = _holder?.CardNode?.GetNodeOrNull<Control>("%DescriptionLabel");
        if (descriptionLabel != null && GodotObject.IsInstanceValid(descriptionLabel))
        {
            return new Rect2(descriptionLabel.Position, descriptionLabel.Size);
        }

        if (_holder?.CardNode != null && GodotObject.IsInstanceValid(_holder.CardNode))
        {
            Vector2 size = Vector2.Zero;

            if (_holder.CardNode is NCard nCard && GodotObject.IsInstanceValid(nCard))
            {
                size = nCard.GetCurrentSize();
            }

            if ((size.X <= 1f || size.Y <= 1f) && _holder.CardNode is Control cardControl)
            {
                size = cardControl.Size;
            }

            return new Rect2(Vector2.Zero, size);
        }

        if (_holder?.Hitbox != null && GodotObject.IsInstanceValid(_holder.Hitbox))
        {
            return new Rect2(Vector2.Zero, _holder.Hitbox.Size);
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

        Rect2 descriptionRect = GetDescriptionRect();
        if (descriptionRect.Size.X <= 1f || descriptionRect.Size.Y <= 1f)
        {
            reason = "description_rect_unavailable";
            return false;
        }

        reason = "ok";
        return true;
    }

    private void OnBanStateChanged(string _, bool __)
    {
        RefreshUi();
    }
}


