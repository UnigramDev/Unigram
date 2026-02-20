//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

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
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<string>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.NameColor>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(System.Collections.Generic.List<Telegram.Td.Api.ProfileColor>))]
[assembly: GeneratedWinRTExposedExternalType(typeof(Telegram.Collections.MvxObservableCollection<Telegram.Td.Api.PremiumFeature>))]
#else
namespace WinRT
{
    // This attribute is just a dummy for making it easier to port the code to .NET 9 and Native AOT.
    public partial class GeneratedBindableCustomPropertyAttribute : Attribute
    {

    }
}
#endif
