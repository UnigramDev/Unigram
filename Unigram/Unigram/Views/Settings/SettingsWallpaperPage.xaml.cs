using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
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
    public sealed partial class SettingsWallPaperPage : Page
    {
        public SettingsWallPaperViewModel ViewModel => DataContext as SettingsWallPaperViewModel;

        public SettingsWallPaperPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsWallPaperViewModel>();
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
                content.Fill = new SolidColorBrush(Color.FromArgb(0xFF, (byte)((wallpaper.Color >> 16) & 0xFF), (byte)((wallpaper.Color >> 8) & 0xFF), (byte)((wallpaper.Color & 0xFF))));
            }
        }
    }
}
