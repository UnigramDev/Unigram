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
// Not on WinUI 3, where the workaround is both unnecessary and wrong: CsWinRT #2524 is about
// several UWP views on several threads, this host has one thread, and CompositionTargetImpl binds
// the ABI interfaces of Windows.UI.Xaml.Media - not the Microsoft.UI.Xaml.Media ones the framework
// would actually answer to. The plain projected type is right there.
#if WINUI3
global using CompositionTarget = Microsoft.UI.Xaml.Media.CompositionTarget;
global using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;
#elif NET9_0_OR_GREATER
global using CompositionTarget = Telegram.Common.CompositionTargetImpl;
global using DispatcherQueue = Windows.System.DispatcherQueue;
#else
global using CompositionTarget = Windows.UI.Xaml.Media.CompositionTarget;
global using DispatcherQueue = Windows.System.DispatcherQueue;
#endif
global using Object = Telegram.Td.Api.Object;
global using Point = Windows.Foundation.Point;
global using TimeZone = Telegram.Td.Api.TimeZone;
global using User = Telegram.Td.Api.User;
global using VirtualKey = Windows.System.VirtualKey;
global using VirtualKeyModifiers = Windows.System.VirtualKeyModifiers;
// Not Path: that name is System.IO.Path wherever the Shapes namespace is not imported.
#if WINUI3
global using XamlPath = Microsoft.UI.Xaml.Shapes.Path;
global using Windows.Foundation;
#else
global using XamlPath = Windows.UI.Xaml.Shapes.Path;
#endif
// A delegate handed to a WinRT event or to AddHandler has to be the type the projection uses:
// TypedEventHandler on .NET Native and CsWinRT 2, System.EventHandler<,> on CsWinRT 3. C# cannot
// alias an open generic, so each closed type that is stored or passed around gets its own.
#if CSWINRT3_0
global using CharacterReceivedHandler = System.EventHandler<Microsoft.UI.Xaml.UIElement, Microsoft.UI.Xaml.Input.CharacterReceivedRoutedEventArgs>;
global using CompositionBatchCompletedHandler = System.EventHandler<object, Microsoft.UI.Composition.CompositionBatchCompletedEventArgs>;
global using ContainerContentChangingHandler = System.EventHandler<Microsoft.UI.Xaml.Controls.ListViewBase, Microsoft.UI.Xaml.Controls.ContainerContentChangingEventArgs>;
global using ContextRequestedHandler = System.EventHandler<Microsoft.UI.Xaml.UIElement, Microsoft.UI.Xaml.Input.ContextRequestedEventArgs>;
global using HyperlinkClickHandler = System.EventHandler<Microsoft.UI.Xaml.Documents.Hyperlink, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs>;
#elif WINUI3
global using CharacterReceivedHandler = Windows.Foundation.TypedEventHandler<Microsoft.UI.Xaml.UIElement, Microsoft.UI.Xaml.Input.CharacterReceivedRoutedEventArgs>;
global using CompositionBatchCompletedHandler = Windows.Foundation.TypedEventHandler<object, Microsoft.UI.Composition.CompositionBatchCompletedEventArgs>;
global using ContainerContentChangingHandler = Windows.Foundation.TypedEventHandler<Microsoft.UI.Xaml.Controls.ListViewBase, Microsoft.UI.Xaml.Controls.ContainerContentChangingEventArgs>;
global using ContextRequestedHandler = Windows.Foundation.TypedEventHandler<Microsoft.UI.Xaml.UIElement, Microsoft.UI.Xaml.Input.ContextRequestedEventArgs>;
global using HyperlinkClickHandler = Windows.Foundation.TypedEventHandler<Microsoft.UI.Xaml.Documents.Hyperlink, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs>;
#else
global using CharacterReceivedHandler = Windows.Foundation.TypedEventHandler<Windows.UI.Xaml.UIElement, Windows.UI.Xaml.Input.CharacterReceivedRoutedEventArgs>;
global using CompositionBatchCompletedHandler = Windows.Foundation.TypedEventHandler<object, Windows.UI.Composition.CompositionBatchCompletedEventArgs>;
global using ContainerContentChangingHandler = Windows.Foundation.TypedEventHandler<Windows.UI.Xaml.Controls.ListViewBase, Windows.UI.Xaml.Controls.ContainerContentChangingEventArgs>;
global using ContextRequestedHandler = Windows.Foundation.TypedEventHandler<Windows.UI.Xaml.UIElement, Windows.UI.Xaml.Input.ContextRequestedEventArgs>;
global using HyperlinkClickHandler = Windows.Foundation.TypedEventHandler<Windows.UI.Xaml.Documents.Hyperlink, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs>;
#endif
#if NET9_0_OR_GREATER && !CSWINRT3_0
using WinRT;

// Direct2D
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Windows.Foundation.Rect>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<System.Collections.Generic.List<Windows.Foundation.Rect>>))]

[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.NameColor>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.ProfileColor>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.QuickReplyShortcut>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Views.Popups.CodeLanguage>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.PremiumFeature[]))]

// A grouped CollectionViewSource boxes every group on its own to QI it for IBindableIterable, so
// the group type needs a vtable of its own and not just the collection holding it. TG1001 cannot
// see those sites: nothing in source converts a group, the framework does it while enumerating.
// Without one the list comes up empty rather than failing - the QI just yields no children.
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.KeyedList<Telegram.ViewModels.Settings.KeyedGroup, Telegram.Td.Api.Session>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.KeyedList<string, object>))]
#elif CSWINRT3_0
// CsWinRT 3 renamed the attribute; the alias lets the 2.x spelling drive its generator, which
// resolves aliases. The namespace stays for the files that import it for the attribute.
global using GeneratedBindableCustomProperty = WindowsRuntime.Xaml.GeneratedCustomPropertyProviderAttribute;

namespace WinRT
{
}
#else
namespace WinRT
{
    // This attribute is just a dummy for making it easier to port the code to .NET 9 and Native AOT.
    public partial class GeneratedBindableCustomPropertyAttribute : System.Attribute
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
