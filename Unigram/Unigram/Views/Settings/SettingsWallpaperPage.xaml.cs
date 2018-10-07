using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsWallpaperPage : Page, IHandle<UpdateFile>
    {
        public SettingsWallpaperViewModel ViewModel => DataContext as SettingsWallpaperViewModel;

        public SettingsWallpaperPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsWallpaperViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.Aggregator.Subscribe(this);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.Aggregator.Unsubscribe(this);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var wallpaper = args.Item as Wallpaper;
            if (wallpaper.Id == 1000001)
            {
                return;
            }
            else if (wallpaper.Sizes.Count > 0)
            {
                var small = wallpaper.GetSmall();
                if (small == null)
                {
                    return;
                }

                var content = args.ItemContainer.ContentTemplateRoot as Image;
                content.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, small.Photo, 64, 64);                
            }
            else
            {
                var content = args.ItemContainer.ContentTemplateRoot as Rectangle;
                content.Fill = new SolidColorBrush(Color.FromArgb(0xFF, (byte)((wallpaper.Color >> 16) & 0xFF), (byte)((wallpaper.Color >> 8) & 0xFF), (byte)(wallpaper.Color & 0xFF)));
            }
        }

        private void Image_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var wallpaper = args.NewValue as Wallpaper;
            if (wallpaper == null)
            {
                return;
            }

            var big = wallpaper.GetBig();
            if (big == null)
            {
                return;
            }

            var content = sender as Image;
            content.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, big.Photo, big.Width, big.Height);
        }

        private void Rectangle_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var wallpaper = args.NewValue as Wallpaper;
            if (wallpaper == null)
            {
                return;
            }

            var content = sender as Rectangle;
            content.Fill = new SolidColorBrush(Color.FromArgb(0xFF, (byte)((wallpaper.Color >> 16) & 0xFF), (byte)((wallpaper.Color >> 8) & 0xFF), (byte)(wallpaper.Color & 0xFF)));
        }

        public void Handle(UpdateFile update)
        {
            this.BeginOnUIThread(() =>
            {
                foreach (var item in ViewModel.Items)
                {
                    if (item.UpdateFile(update.File))
                    {
                        var container = List.ContainerFromItem(item) as SelectorItem;
                        if (container == null)
                        {
                            continue;
                        }

                        var content = container.ContentTemplateRoot as Image;
                        if (content == null)
                        {
                            continue;
                        }

                        var small = item.GetSmall();
                        if (small == null)
                        {
                            return;
                        }

                        content.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, small.Photo, 64, 64);
                    }
                }

                if (Presenter.Content is Wallpaper wallpaper && wallpaper.UpdateFile(update.File))
                {
                    var big = wallpaper.GetBig();
                    if (big == null)
                    {
                        return;
                    }

                    var content = Presenter.ContentTemplateRoot as Image;
                    content.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, big.Photo, big.Width, big.Height);
                }
            });
        }
    }
}
