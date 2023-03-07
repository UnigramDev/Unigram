//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Cells;
using Unigram.Navigation;
using Unigram.ViewModels.Settings;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsStoragePage : HostedPage
    {
        public SettingsStorageViewModel ViewModel => DataContext as SettingsStorageViewModel;

        public SettingsStoragePage()
        {
            InitializeComponent();
            Title = Strings.Resources.StorageUsage;

            InitializeKeepMediaTicks();
        }

        private void InitializeKeepMediaTicks()
        {
            int j = 0;
            for (int i = 0; i < 4; i++)
            {
                var label = new TextBlock { Text = ConvertKeepMediaTick(i), TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch, Style = BootStrapper.Current.Resources["InfoCaptionTextBlockStyle"] as Style };
                Grid.SetColumn(label, j);

                KeepMediaTicks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

                if (i < 3)
                {
                    KeepMediaTicks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }

                KeepMediaTicks.Children.Add(label);
                j += 2;
            }

            Grid.SetColumnSpan(KeepMedia, KeepMediaTicks.ColumnDefinitions.Count);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is UserCell content)
            {
                content.UpdateStatisticsByChat(ViewModel.ClientService, args, OnContainerContentChanging);
            }
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.Clear(e.ClickedItem as StorageStatisticsByChat);
        }

        #region Binding

        private string ConvertTtl(int days)
        {
            if (days < 1)
            {
                return Strings.Resources.KeepMediaForever;
            }
            else if (days < 7)
            {
                return Locale.Declension("Days", days);
            }
            else if (days < 30)
            {
                return Locale.Declension("Weeks", 1);
            }

            return Locale.Declension("Months", 1);
        }

        private bool ConvertEnabled(object value)
        {
            return value != null;
        }

        private int ConvertKeepMedia(int value)
        {
            switch (Math.Max(0, Math.Min(30, value)))
            {
                case 0:
                default:
                    return 3;
                case 3:
                    return 0;
                case 7:
                    return 1;
                case 30:
                    return 2;
            }
        }

        private void ConvertKeepMediaBack(double value)
        {
            switch (value)
            {
                case 0:
                    ViewModel.KeepMedia = 3;
                    break;
                case 1:
                    ViewModel.KeepMedia = 7;
                    break;
                case 2:
                    ViewModel.KeepMedia = 30;
                    break;
                case 3:
                    ViewModel.KeepMedia = 0;
                    break;
            }
        }

        private string ConvertKeepMediaTick(double value)
        {
            var days = 0;
            switch (value)
            {
                case 0:
                    days = 3;
                    break;
                case 1:
                    days = 7;
                    break;
                case 2:
                    days = 30;
                    break;
                case 3:
                    days = 0;
                    break;
            }

            if (days < 1)
            {
                return Strings.Resources.KeepMediaForever;
            }
            else if (days < 7)
            {
                return Locale.Declension("Days", days);
            }
            else if (days < 30)
            {
                return Locale.Declension("Weeks", 1);
            }

            return Locale.Declension("Months", 1);
        }

        #endregion
    }
}
