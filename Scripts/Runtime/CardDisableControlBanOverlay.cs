using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace CardDisableControl.Scripts.Runtime;

internal partial class CardDisableControlBanOverlay : Control
{
    public const string OverlayNodeName = "CardDisableControlBanOverlay";

    private NGridCardHolder? _holder;
    private bool _shouldDraw;
    private string? _lastCardKey;
    private Vector2 _lastSize = Vector2.Zero;

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
        CardDisableControlLogger.Info($"已为总览卡牌挂载红X覆盖层: {holder.Name}");
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
        RefreshDrawState();
    }

    public override void _Draw()
    {
        if (!_shouldDraw)
        {
            return;
        }

        Vector2 size = Size;
        if (size.X < 8f || size.Y < 8f)
        {
            return;
        }

        float margin = Math.Max(16f, Math.Min(size.X, size.Y) * 0.08f);
        float width = Math.Max(6f, Math.Min(size.X, size.Y) * 0.03f);
        Color shadow = new(0.15f, 0f, 0f, 0.6f);
        Color main = new(0.95f, 0.1f, 0.1f, 0.9f);

        Vector2 topLeft = new(margin, margin);
        Vector2 topRight = new(size.X - margin, margin);
        Vector2 bottomLeft = new(margin, size.Y - margin);
        Vector2 bottomRight = new(size.X - margin, size.Y - margin);

        DrawLine(topLeft + Vector2.Down * 2f, bottomRight + Vector2.Down * 2f, shadow, width + 2f, true);
        DrawLine(bottomLeft + Vector2.Down * 2f, topRight + Vector2.Down * 2f, shadow, width + 2f, true);
        DrawLine(topLeft, bottomRight, main, width, true);
        DrawLine(bottomLeft, topRight, main, width, true);
    }

    private void RefreshDrawState()
    {
        string? currentCardKey = CardDisableControlBanState.GetCardKey(_holder?.CardModel);
        bool shouldDraw = ShouldDrawNow();
        Vector2 currentSize = Size;

        if (_shouldDraw != shouldDraw || !string.Equals(_lastCardKey, currentCardKey, StringComparison.OrdinalIgnoreCase) || currentSize != _lastSize)
        {
            _shouldDraw = shouldDraw;
            _lastCardKey = currentCardKey;
            _lastSize = currentSize;
            QueueRedraw();
        }
    }

    private bool ShouldDrawNow()
    {
        if (_holder == null || !GodotObject.IsInstanceValid(_holder))
        {
            return false;
        }

        if (!CardDisableControlUiState.IsCardLibraryHolder(_holder))
        {
            return false;
        }

        if (_holder.CardNode == null || _holder.CardNode.Visibility != ModelVisibility.Visible)
        {
            return false;
        }

        return CardDisableControlBanState.IsBanned(_holder.CardModel);
    }

    private void OnBanStateChanged(string _, bool __)
    {
        QueueRedraw();
    }
}
