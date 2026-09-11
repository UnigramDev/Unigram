//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.InteropServices;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
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
    /// The probes exist to answer questions the reports cannot: whether an unrelated property on
    /// the same element fails too (the element is broken, not the property), and whether a write
    /// that the property system cannot short-circuit succeeds (transient, so we are looking for a
    /// timing window) or fails again (the failure is in what the write raises, not in reaching it).
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
        private static bool _probing;

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
            // The clear/restore probe raises both of these, and the view model must not see that:
            // the intermediate value would reach it as a real change, and would stay there if the
            // restore is the write that fails.
            if (sender is ToggleButton toggle && _probing is false)
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
            Logger.Error(string.Format("IsChecked = {0} failed on {1} '{2}' -> {3} 0x{4:X8}, loaded {5}, root {6}, parent {7}",
                value,
                toggle.GetType().Name,
                toggle.Name,
                ex.GetType().Name,
                Marshal.GetHRForException(ex),
                toggle.IsLoaded,
                toggle.XamlRoot != null,
                toggle.Parent != null));

            // The one thing put_IsChecked raises that every failing flavour has in common, and that
            // Tag does not: a PropertyChanged automation event, raised only while a UIA client is
            // listening - which would be why this is a handful of machines and not everyone.
            // Whether this element already has a peer would say more, but every way of asking risks
            // creating one, and a peer this code made would change what the writes below raise.
            Probe("automation listeners", () => AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged).ToString());

            // Does anything at all still work on this element? Tag is the cheapest unrelated
            // property on it, and it is not read anywhere, so writing it changes nothing.
            // The value read back is what makes the write below mean anything: if IsChecked
            // already holds what was asked for, the failed write applied and threw from what it
            // raised afterwards, and writing the same value again is an equal-value no-op.
            Probe("read IsChecked", () => toggle.IsChecked?.ToString() ?? "null");
            Probe("write Tag", () => { toggle.Tag = value; return "ok"; });

            var recovered = Probe("write IsChecked again", () => { toggle.IsChecked = value; return "ok"; });

            if (recovered)
            {
                // A change the property system cannot short-circuit, so everything put_IsChecked
                // raises runs again - and this is the split the equal-value write cannot make:
                // fail here and the failure lives in what the write raises, succeed and the window
                // really has closed. Clearing rather than inverting because a grouped RadioButton
                // driven to true unchecks a sibling that restoring this one does not bring back;
                // to null it only unchecks itself, and null and false share a visual state.
                try
                {
                    _probing = true;

                    Probe("clear IsChecked", () => { toggle.ClearValue(ToggleButton.IsCheckedProperty); return "ok"; });
                    Probe("write IsChecked back", () => { toggle.IsChecked = value; return "ok"; });
                }
                finally
                {
                    // Left set, it would silence the pushback for the rest of the session.
                    _probing = false;
                }
            }

            if (retry && recovered is false)
            {
                // Same thread, next tick. If this one lands, the failure was a window and not a
                // state, which is the single most useful thing the reports do not say.
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

        private static bool Probe(string name, Func<string> action)
        {
            try
            {
                Logger.Error("    " + name + ": " + action());
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("    {0}: {1} 0x{2:X8}", name, ex.GetType().Name, Marshal.GetHRForException(ex)));
                return false;
            }
        }
    }
}
