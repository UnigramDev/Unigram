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

// VoipVideoSourceGroup
// FreeformGradientSurface
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Vector<int>))]
// The Telegram.Td.Api arrays that used to be here are gone: both parsers materialise a vector as a
// List<T>, never an array, so nothing could reach these. TdDotNetApi.WinRT.g.cs now exposes the
// List instantiations from the schema, which is the only place that knows all of them - a binding
// assigns through the declared IList<T>, so no analyzer can see the concrete type.
//
// Boxed into a WinRT object somewhere - ItemsSource, SelectedItem, Content. A constructed generic
// or an array gets no CCW vtable of its own, so XAML fails the QI for IBindableIterable and
// set_ItemsSource returns E_INVALIDARG, which on the UI thread's DispatcherQueue is a fail-fast
// rather than an exception.
//
// This list is the TG1001 output, not a hand sweep: grepping for ItemsSource found the wrong type
// twice, because what the popups assign is a DiffObservableCollection and the List beside it is
// only the backing store. Rerun the analyzer rather than adding entries by hand.
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.DiffObservableCollection<Telegram.Entities.Country>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.DiffObservableCollection<Telegram.Services.CaptureSessionItem>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.DiffObservableCollection<Telegram.Services.PlaybackItem>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.DiffObservableCollection<Telegram.Td.Api.AvailableGift>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.DiffObservableCollection<Telegram.Td.Api.CountryInfo>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.DiffObservableCollection<Telegram.Td.Api.TimeZone>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.DiffObservableCollection<Telegram.Views.Popups.TranslateToLanguage>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Controls.StorageChartItem>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.BusinessFeature>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.Chat>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.ChatBoostLevelFeatures>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.ChatBoostSlot>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.LanguagePackInfo>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.PremiumGiftPaymentOption>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.User>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.ViewModels.Folders.FolderFlag>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.ViewModels.Settings.ChatThemeViewModel>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Views.Popups.PollResultViewModel>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Views.Popups.SettingsOptionItem<int>>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Windows.UI.Xaml.FrameworkElement>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.ObjectModel.ObservableCollection<Telegram.Td.Api.GroupCallMessage>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.ObjectModel.ObservableCollection<Telegram.ViewModels.Business.BusinessHoursRange>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.ObjectModel.ObservableCollection<Telegram.ViewModels.RevenueTabItem>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.ObjectModel.ObservableCollection<Telegram.Views.Chats.Popups.SelectionValue>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.ObjectModel.ObservableCollection<Telegram.Views.Premium.Popups.GiftGroup>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(object[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.PremiumGiftPaymentOption[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.IncrementalCollection<object>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.IncrementalCollection<Telegram.Td.Api.Chat>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.IncrementalCollection<Telegram.Td.Api.ChatInviteLinkMember>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.IncrementalCollection<Telegram.Td.Api.MessageSender>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.IncrementalCollection<Telegram.Td.Api.Passkey>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.IncrementalCollection<Telegram.ViewModels.Settings.ChatThemeViewModel>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.IncrementalCollection<Telegram.ViewModels.Stories.StoryViewModel>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.IncrementalCollectionView<Telegram.Td.Api.ReceivedGift, Telegram.ViewModels.Profile.ProfileGiftsTabViewModel.ReceivedGiftsCollection>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.RangeObservableCollection<object>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.SortedObservableCollection<Telegram.Td.Api.GroupCallMessage>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.SynchronizedList<Telegram.ViewModels.MessageViewModel>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Common.EmojiSkinData[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.ViewModels.ChatFolderIcon2[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Views.Popups.SettingsOptionItem<int>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Views.Stories.Popups.StealthPopup.StealthModeFeature[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.SortedObservableCollection<Telegram.Td.Api.ConnectedWebsite>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.SortedObservableCollection<Telegram.Td.Api.ChatMember>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.KeyedList<Telegram.ViewModels.Settings.KeyedGroup, Telegram.Td.Api.Chat>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.ViewModels.KeyedCollection<Telegram.Td.Api.Message>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.ObjectModel.ObservableCollection<Telegram.Td.Api.ReceivedGift>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.GroupCallMessage>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.QuickReplyShortcut>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.Dictionary<System.Type, Telegram.Td.Api.Animation>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.ObjectModel.ReadOnlyDictionary<System.Type, Telegram.Td.Api.Animation>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Windows.Foundation.Rect>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<System.Collections.Generic.IList<Windows.Foundation.Rect>>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<System.Collections.Generic.List<Windows.Foundation.Rect>>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<string>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.NameColor>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.ProfileColor>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Td.Api.PremiumFeature[]))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.RangeObservableCollection<Telegram.Td.Api.PremiumFeature>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.RangeObservableCollection<Telegram.ViewModels.Drawers.StickerViewModel>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.RangeObservableCollection<Telegram.ViewModels.Drawers.StickerSetViewModel>))]
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
