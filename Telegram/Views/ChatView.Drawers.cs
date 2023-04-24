//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls.Drawers;
using Telegram.Converters;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views
{
    public partial class ChatView
    {
        private void Sticker_ContextRequested(UIElement sender, ItemContextRequestedEventArgs<Sticker> args)
        {
            var element = sender as FrameworkElement;
            var sticker = args.Item;

            if (sticker == null)
            {
                return;
            }

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.ViewSticker, sticker, Strings.ViewPackPreview, new FontIcon { Glyph = Icons.Sticker });

            if (ViewModel.ClientService.IsStickerFavorite(sticker.StickerValue.Id))
            {
                flyout.CreateFlyoutItem(ViewModel.RemoveFavoriteSticker, sticker, Strings.DeleteFromFavorites, new FontIcon { Glyph = Icons.StarOff });
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.AddFavoriteSticker, sticker, Strings.AddToFavorites, new FontIcon { Glyph = Icons.Star });
            }

            if (ViewModel.ClientService.IsStickerRecent(sticker.StickerValue.Id))
            {
                flyout.CreateFlyoutItem(ViewModel.RemoveRecentSticker, sticker, Strings.DeleteFromRecent, new FontIcon { Glyph = Icons.Delete });
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
                flyout.CreateFlyoutItem(anim => ViewModel.SendSticker(anim, null, true), sticker, Strings.SendWithoutSound, new FontIcon { Glyph = Icons.AlertOff });
                flyout.CreateFlyoutItem(anim => ViewModel.SendSticker(anim, true, null), sticker, self ? Strings.SetReminder : Strings.ScheduleMessage, new FontIcon { Glyph = Icons.CalendarClock });
            }

            args.ShowAt(flyout, element);
        }

        private void Animation_ContextRequested(UIElement sender, ItemContextRequestedEventArgs<Animation> args)
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
                flyout.CreateFlyoutItem(ViewModel.DeleteAnimation, animation, Strings.Delete, new FontIcon { Glyph = Icons.Delete });
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.SaveAnimation, animation, Strings.SaveToGIFs, new FontIcon { Glyph = Icons.Gif });
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
                flyout.CreateFlyoutItem(anim => ViewModel.SendAnimation(anim, null, true), animation, Strings.SendWithoutSound, new FontIcon { Glyph = Icons.AlertOff });
                flyout.CreateFlyoutItem(anim => ViewModel.SendAnimation(anim, true, null), animation, self ? Strings.SetReminder : Strings.ScheduleMessage, new FontIcon { Glyph = Icons.CalendarClock });
            }

            args.ShowAt(flyout, element);
        }
    }
}
