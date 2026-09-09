//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

// CsWinRT cannot subscribe Windows.UI.Xaml.Media.CompositionTarget from more than one view:
// every view's handler ends up on the first one's thread. CompositionTargetImpl registers
// through the ABI instead, and the alias keeps every call site written the way it always was.
// Aliasing it in both directions also settles the ambiguity with Windows.UI.Composition's own
// CompositionTarget, which is why those call sites used to spell the namespace out.
#if NET9_0_OR_GREATER
global using CompositionTarget = Telegram.Common.CompositionTargetImpl;
#else
global using CompositionTarget = Windows.UI.Xaml.Media.CompositionTarget;
#endif
global using DispatcherQueue = Windows.System.DispatcherQueue;
global using Object = Telegram.Td.Api.Object;
global using Point = Windows.Foundation.Point;
global using TimeZone = Telegram.Td.Api.TimeZone;
global using User = Telegram.Td.Api.User;
global using VirtualKey = Windows.System.VirtualKey;
global using VirtualKeyModifiers = Windows.System.VirtualKeyModifiers;
#if NET9_0_OR_GREATER
using WinRT;

// Direct2D
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Windows.Foundation.Rect>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<System.Collections.Generic.List<Windows.Foundation.Rect>>))]

[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.NameColor>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.ProfileColor>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.QuickReplyShortcut>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.PremiumFeature[]))]

// A grouped CollectionViewSource boxes every group on its own to QI it for IBindableIterable, so
// the group type needs a vtable of its own and not just the collection holding it. TG1001 cannot
// see those sites: nothing in source converts a group, the framework does it while enumerating.
// Without one the list comes up empty rather than failing - the QI just yields no children.
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.KeyedList<Telegram.ViewModels.Settings.KeyedGroup, Telegram.Td.Api.Session>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.KeyedList<string, object>))]
#else
namespace WinRT
{
    // This attribute is just a dummy for making it easier to port the code to .NET 9 and Native AOT.
    public partial class GeneratedBindableCustomPropertyAttribute : Attribute
    {
        public GeneratedBindableCustomPropertyAttribute()
        {

        }

        public GeneratedBindableCustomPropertyAttribute(object arg1, object arg2)
        {

        }
    }
}
#endif
