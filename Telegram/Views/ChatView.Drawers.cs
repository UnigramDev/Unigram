//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls.Drawers;
using Telegram.Controls.Media;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views
{
    public partial class ChatView
    {
        private void Sticker_ContextRequested(object sender, ItemContextRequestedEventArgs<Sticker> args)
        {
            var element = sender as FrameworkElement;
            var sticker = args.Item;

            if (sticker?.StickerValue == null)
            {
                return;
            }

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.ViewSticker, sticker, Strings.ViewPackPreview, Icons.Sticker);

            if (ViewModel.ClientService.IsStickerFavorite(sticker.StickerValue.Id))
            {
                flyout.CreateFlyoutItem(ViewModel.RemoveFavoriteSticker, sticker, Strings.DeleteFromFavorites, Icons.StarOff);
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.AddFavoriteSticker, sticker, Strings.AddToFavorites, Icons.Star);
            }

            if (ViewModel.ClientService.IsStickerRecent(sticker.StickerValue.Id))
            {
                flyout.CreateFlyoutItem(ViewModel.RemoveRecentSticker, sticker, Strings.DeleteFromRecent, Icons.Delete, destructive: true);
            }

            if (ViewModel.Type is DialogType.History or DialogType.Thread && !ViewModel.ClientService.IsPaid(ViewModel.Chat) && !ViewModel.ClientService.IsDirectMessagesGroup(ViewModel.Chat))
            {
                var chat = ViewModel.Chat;
                if (chat == null)
                {
                    return;
                }

                var self = ViewModel.ClientService.IsSavedMessages(chat);

                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(anim => ViewModel.SendSticker(anim, SchedulingState.Auto, true), sticker, Strings.SendWithoutSound, Icons.AlertOff);
                flyout.CreateFlyoutItem(anim => ViewModel.SendSticker(anim, SchedulingState.Schedule, null), sticker, self ? Strings.SetReminder : Strings.ScheduleMessage, Icons.CalendarClock);
            }

            args.ShowAt(flyout, element);
        }

        private void Animation_ContextRequested(object sender, ItemContextRequestedEventArgs<Animation> args)
        {
            var element = sender as FrameworkElement;
            var animation = args.Item;

            if (animation == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            if (ViewModel.ClientService.IsAnimationSaved(animation.AnimationValue.Id))
            {
                flyout.CreateFlyoutItem(ViewModel.DeleteAnimation, animation, Strings.Delete, Icons.Delete, destructive: true);
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.SaveAnimation, animation, Strings.SaveToGIFs, Icons.Gif);
            }

            if (ViewModel.Type is DialogType.History or DialogType.Thread && !ViewModel.ClientService.IsPaid(ViewModel.Chat) && !ViewModel.ClientService.IsDirectMessagesGroup(ViewModel.Chat))
            {
                var chat = ViewModel.Chat;
                if (chat == null)
                {
                    return;
                }

                var self = ViewModel.ClientService.IsSavedMessages(chat);

                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(anim => ViewModel.SendAnimation(anim, SchedulingState.Auto, true), animation, Strings.SendWithoutSound, Icons.AlertOff);
                flyout.CreateFlyoutItem(anim => ViewModel.SendAnimation(anim, SchedulingState.Schedule, null), animation, self ? Strings.SetReminder : Strings.ScheduleMessage, Icons.CalendarClock);
            }

            args.ShowAt(flyout, element);
        }

        private void Emoji_ContextRequested(object sender, ItemContextRequestedEventArgs<StickerViewModel> args)
        {
            var element = sender as FrameworkElement;
            var sticker = args.Item;

            if (sticker?.StickerValue == null || sticker.FullType is not StickerFullTypeCustomEmoji customEmoji)
            {
                return;
            }

            var flyout = new MenuFlyout();

            void Send(Sticker sticker)
            {
                ViewModel.SendMessageAsync(sticker.ToFormattedText());
            }

            void Copy(Sticker sticker)
            {
                MessageHelper.CopyText(XamlRoot, sticker.ToFormattedText());
            }

            void SetAsStatus(Sticker sticker)
            {
                ViewModel.ClientService.Send(new SetEmojiStatus(new EmojiStatus(new EmojiStatusTypeCustomEmoji(customEmoji.CustomEmojiId), 0)));
                ViewModel.ShowToast(Strings.SetAsEmojiStatusInfo, DelayedFileSource.FromSticker(ViewModel.ClientService, sticker));
            }

            if (ViewModel.Type is DialogType.History or DialogType.Thread && ViewModel.IsPremium)
            {
                flyout.CreateFlyoutItem(Send, (Sticker)sticker, Strings.SendEmojiPreview, Icons.Send);
            }

            flyout.CreateFlyoutItem(Copy, (Sticker)sticker, Strings.CopyEmojiPreview, Icons.Copy);

            if (ViewModel.Type is DialogType.History or DialogType.Thread && ViewModel.IsPremium)
            {
                flyout.CreateFlyoutItem(SetAsStatus, (Sticker)sticker, Strings.SetAsEmojiStatus, Icons.Emoji);
            }

            args.ShowAt(flyout, element);
        }
    }
}
