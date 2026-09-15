//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.InteropServices;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Common
{
    /// <summary>
    /// Writes <see cref="ToggleButton.IsChecked"/> on behalf of an x:Bind, so a failure there
    /// cannot take the rest of the page's bindings with it.
    /// </summary>
    /// <remarks>
    /// Native XAML returns a bare E_FAIL out of put_IsChecked on a small number of machines, from
    /// every kind of ToggleButton and from every path that changes it, and nothing in the crash
    /// report names the failing frame. So this is not the fix: it is containment plus the
    /// measurement that decides what the fix is. Today the throw escapes the generated
    /// Bindings.Loading and abandons every remaining binding on the page - that, not the toggle
    /// itself, is what the user sees. Bound here instead, the write is caught, probed, retried
    /// once, and still reported.
    ///
    /// It reports and it repairs, and it no longer probes. The probes answered what they were for -
    /// the element is not broken, the value applies before the throw, the failure is a state and not
    /// a window, and only a write of true fails - which leaves the RadioButton group walk, and that
    /// is answered in a spike. One of them answered its question and then left a live user looking
    /// at an unchecked radio, because restoring the value fails the same way the write did.
    ///
    /// Delete this, and the probes with it, once the cause is known.
    /// </remarks>
    public static class ToggleHelper
    {
        public static bool GetIsChecked(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsCheckedProperty);
        }

        // Everything happens here rather than in a change callback, because a binding's first write
        // is usually false, which is this property's default, and a change callback does not run
        // for it. Subscribing there left every toggle that starts unchecked with no Checked handler,
        // so the user's click never reached the view model and the setting was silently dropped.
        // The setter is the one door in: the generated x:Bind calls it, and so does XAML markup.
        public static void SetIsChecked(DependencyObject obj, bool value)
        {
            obj.SetValue(IsCheckedProperty, value);

            if (obj is ToggleButton toggle)
            {
                Attach(toggle);

                Write(toggle, value, true);
            }
        }

        // bool and not bool?: every binding this stands in for carries a bool, and a nullable value
        // crosses the ABI boxed - one more moving part on the exact path that is failing.
        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.RegisterAttached("IsChecked", typeof(bool), typeof(ToggleHelper), new PropertyMetadata(false));

        private static readonly DependencyProperty AttachedProperty =
            DependencyProperty.RegisterAttached("Attached", typeof(bool), typeof(ToggleHelper), new PropertyMetadata(false));

        private static bool _reported;

        private static void Attach(ToggleButton toggle)
        {
            // Both handlers are static methods, so the delegate holds nothing that could keep the
            // element alive: the element owns the subscription and takes it to the grave. There is
            // deliberately no matching -=, because there is nothing to detach it from.
            if ((bool)toggle.GetValue(AttachedProperty) is false)
            {
                toggle.SetValue(AttachedProperty, true);

                toggle.Checked += OnToggled;
                toggle.Unchecked += OnToggled;
            }
        }

        // The other half of Mode=TwoWay: the user's click lands on the real property, and pushing it
        // back into this one is what lets the binding carry it to the view model.
        private static void OnToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle)
            {
                SetIsChecked(toggle, toggle.IsChecked is true);
            }
        }

        private static void Write(ToggleButton toggle, bool value, bool retry)
        {
            try
            {
                toggle.IsChecked = value;
            }
            catch (Exception ex)
            {
                Report(toggle, value, ex, retry);
            }
        }

        private static void Report(ToggleButton toggle, bool value, Exception ex, bool retry)
        {
            try
            {
                ReportCore(toggle, value, ex, retry);
            }
            catch
            {
                // Diagnostics do not get to break the thing they are diagnosing. Reading XamlRoot
                // alone can throw E_POINTER once the window is gone.
            }
        }

        private static void ReportCore(ToggleButton toggle, bool value, Exception ex, bool retry)
        {
            // Reading it back is not a probe but the repair condition below: the failing write
            // applies the value before it throws, so most of the time there is nothing to repair.
            var current = toggle.IsChecked;

            // The group name is the one thing left worth collecting. Only a write of true fails,
            // clearing the same property works, and unchecking is the one that does not walk the
            // group - so the walk is the suspect, and every failing radio so far is in a group.
            Logger.Error(string.Format("IsChecked = {0} failed on {1} '{2}' group '{3}', now {4} -> {5} 0x{6:X8}, loaded {7}, root {8}, parent {9}",
                value,
                toggle.GetType().Name,
                toggle.Name,
                toggle is RadioButton radio ? radio.GroupName : null,
                current?.ToString() ?? "null",
                ex.GetType().Name,
                Marshal.GetHRForException(ex),
                toggle.IsLoaded,
                toggle.XamlRoot != null,
                toggle.Parent != null));

            if (retry && current != value)
            {
                // Only when the value did not stick, and then it is repair rather than diagnosis:
                // the write that failed usually applies before it throws, and re-running one that
                // already took hold would be a no-op anyway.
                var ignore = toggle.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    Logger.Error("retrying IsChecked = " + value);
                    Write(toggle, value, false);
                });
            }

            // One report per session: the log tail carries every occurrence, and a page full of
            // toggles would otherwise send a report each.
            if (_reported is false)
            {
                _reported = true;
                Logger.Exception(ex);
            }
        }
    }
}
