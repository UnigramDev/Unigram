using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Views;
using Unigram.ViewModels.Chats;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Controls;

namespace Unigram.Views.Chats
{
    public sealed partial class CreateChatStep1Page : Page
    {
        public CreateChatStep1ViewModel ViewModel => DataContext as CreateChatStep1ViewModel;

        public CreateChatStep1Page()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<CreateChatStep1ViewModel>();
        }

        private async void EditPhoto_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.AddRange(Constants.PhotoTypes);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var dialog = new EditYourPhotoView(file)
                {
                    CroppingProportions = ImageCroppingProportions.Square,
                    IsCropEnabled = false
                };

                var confirm = await dialog.ShowAsync();
                if (confirm == ContentDialogBaseResult.OK)
                {
                    ViewModel.EditPhotoCommand.Execute(dialog.Result);
                }
            }
        }
    }
}
