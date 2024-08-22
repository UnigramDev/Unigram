//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls.Drawers;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Telegram.Views
{
    public partial class ChatView
    {
        private void Sticker_ContextRequested(object sender, ItemContextRequestedEventArgs<Sticker> args)
        {
            var element = sender as FrameworkElement;
            var sticker = args.Item;

            if (sticker == null)
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

            if (ViewModel.Type == ViewModels.DialogType.History)
            {
                var chat = ViewModel.Chat;
                if (chat == null)
                {
                    return;
                }

                var self = ViewModel.ClientService.IsSavedMessages(chat);

                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(anim => ViewModel.SendSticker(anim, null, true), sticker, Strings.SendWithoutSound, Icons.AlertOff);
                flyout.CreateFlyoutItem(anim => ViewModel.SendSticker(anim, true, null), sticker, self ? Strings.SetReminder : Strings.ScheduleMessage, Icons.CalendarClock);
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

            if (ViewModel.Type == ViewModels.DialogType.History)
            {
                var chat = ViewModel.Chat;
                if (chat == null)
                {
                    return;
                }

                var self = ViewModel.ClientService.IsSavedMessages(chat);

                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(anim => ViewModel.SendAnimation(anim, null, true), animation, Strings.SendWithoutSound, Icons.AlertOff);
                flyout.CreateFlyoutItem(anim => ViewModel.SendAnimation(anim, true, null), animation, self ? Strings.SetReminder : Strings.ScheduleMessage, Icons.CalendarClock);
            }

            args.ShowAt(flyout, element);
        }
    }
}
