using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace CardDisableControl.Scripts.Runtime;

internal static class CardDisableControlUiState
{
    private static readonly object SyncRoot = new();
    private static WeakReference<NGridCardHolder>? _focusedCardLibraryHolder;

    public static void SetFocusedCardLibraryHolder(NGridCardHolder holder)
    {
        lock (SyncRoot)
        {
            _focusedCardLibraryHolder = new WeakReference<NGridCardHolder>(holder);
        }
    }

    public static void ClearFocusedCardLibraryHolder(NGridCardHolder holder)
    {
        lock (SyncRoot)
        {
            if (_focusedCardLibraryHolder == null)
            {
                return;
            }

            if (!_focusedCardLibraryHolder.TryGetTarget(out NGridCardHolder? current) || current == holder)
            {
                _focusedCardLibraryHolder = null;
            }
        }
    }

    public static NGridCardHolder? GetFocusedCardLibraryHolder()
    {
        lock (SyncRoot)
        {
            if (_focusedCardLibraryHolder == null)
            {
                return null;
            }

            if (_focusedCardLibraryHolder.TryGetTarget(out NGridCardHolder? holder))
            {
                return holder;
            }

            _focusedCardLibraryHolder = null;
            return null;
        }
    }

    public static bool IsCardLibraryHolder(Node? node)
    {
        Node? current = node;
        while (current != null)
        {
            if (current is NCardLibraryGrid)
            {
                return true;
            }

            current = current.GetParent();
        }

        return false;
    }
}

