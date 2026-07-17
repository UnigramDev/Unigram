//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;

namespace Telegram.Common
{
    /// <summary>
    /// Enforces a single text selection per window across the independent per-bubble
    /// <see cref="TextSelectionManager"/>s: when one manager takes over (a press/selection),
    /// any other manager that currently owns the selection is cleared — so two messages are
    /// never selected at once. One instance per window, keyed by <see cref="XamlRoot"/>.
    /// </summary>
    public sealed class TextSelectionCoordinator
    {
        private static readonly ConditionalWeakTable<XamlRoot, TextSelectionCoordinator> _windows = new();
        private static readonly ConditionalWeakTable<XamlRoot, TextSelectionCoordinator>.CreateValueCallback _create = _ => new TextSelectionCoordinator();

        // Weak so a recycled/destroyed bubble's manager isn't kept alive by the coordinator.
        private WeakReference<TextSelectionManager> _active;

        public static TextSelectionCoordinator GetFor(XamlRoot xamlRoot)
        {
            return xamlRoot != null ? _windows.GetValue(xamlRoot, _create) : null;
        }

        /// <summary>
        /// Makes <paramref name="manager"/> the window's sole selection owner, clearing the
        /// previous owner's selection (if any other).
        /// </summary>
        public void Activate(TextSelectionManager manager)
        {
            if (_active != null && _active.TryGetTarget(out var previous) && previous != manager)
            {
                // ClearSelection calls back into Deactivate(previous); harmless, we overwrite below.
                previous.ClearSelection();
            }

            _active = new WeakReference<TextSelectionManager>(manager);
        }

        /// <summary><paramref name="manager"/> no longer owns the selection.</summary>
        public void Deactivate(TextSelectionManager manager)
        {
            if (_active != null && _active.TryGetTarget(out var current) && current == manager)
            {
                _active = null;
            }
        }
    }
}
