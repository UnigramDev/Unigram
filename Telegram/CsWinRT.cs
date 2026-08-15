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
using System;
#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WinRT;

[assembly: GeneratedWinRTExposedExternalType(typeof(byte[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(int[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(long[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(string[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.AlternativeVideo[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.AttachmentMenuBot[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.AvailableReaction[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.ChatFolderInfo[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.ChatMember[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.ChatTheme[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.CloseBirthdayUser[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.ClosedVectorPath[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.Emojis[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.DirectMessagesChatTopic[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.ForumTopic[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.GroupCallParticipant[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.MessageEffect[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.MessageReaction[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.MessageSender[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.MessageViewer[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.NameColor[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.PaidReactor[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.ProfileColor[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.QuickReplyMessage[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.QuickReplyShortcut[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.ReactionType[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.SavedMessagesTag[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.Sticker[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.TextEntity[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.UnreadReaction[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.UpgradedGiftAttributeId[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Native.TextStylePart[]))]
// Set as ItemsSource in GroupCallPage's constructor. External generic instantiations get no CCW
// vtable of their own - the generator only emits those for the app's own partial types - so XAML
// fails the QI for IBindableIterable and set_ItemsSource returns E_INVALIDARG. On the UI thread's
// DispatcherQueue that is a fail-fast, not an exception.
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.ObjectModel.ObservableCollection<Telegram.Td.Api.GroupCallMessage>))]
// GetReplyMarkupClip and GetRoundedPolygon take IVector<IVector<Rect>>. The outer list marshals on
// its own, because the generator can see it at the call site; the inner one it never sees, since
// that CCW is only needed when the native side calls GetAt. So the failure waits for a message with
// an inline keyboard to be arranged and then throws out of ArrangeOverride.
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Windows.Foundation.Rect>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<System.Collections.Generic.IList<Windows.Foundation.Rect>>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<System.Collections.Generic.List<Windows.Foundation.Rect>>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<string>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.NameColor>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.ProfileColor>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.MvxObservableCollection<Telegram.Td.Api.PremiumFeature>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.MvxObservableCollection<Telegram.ViewModels.Drawers.StickerViewModel>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.MvxObservableCollection<Telegram.ViewModels.Drawers.StickerSetViewModel>))]
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

    // Likewise. Marks a cast to a WinRT type whose metadata nothing else keeps alive, so that
    // trimming leaves it in place - without it the cast throws at runtime even though the object
    // really is that type.
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    public partial class DynamicWindowsRuntimeCastAttribute : Attribute
    {
        public DynamicWindowsRuntimeCastAttribute(Type type)
        {

        }
    }
}
#endif
