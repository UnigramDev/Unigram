using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TdWindows;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.ViewModels;
using Unigram.ViewModels.BasicGroups;
using Unigram.ViewModels.Chats;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.BasicGroups
{
    public sealed partial class BasicGroupEditPage : Page, IBasicGroupDelegate
    {
        public BasicGroupEditViewModel ViewModel => DataContext as BasicGroupEditViewModel;

        public BasicGroupEditPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<BasicGroupEditViewModel>();
            ViewModel.Delegate = this;
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

        #region Binding

        public void UpdateChat(Chat chat)
        {
            //UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
        }

        public void UpdateChatTitle(Chat chat)
        {
            Title.Text = ViewModel.ProtoService.GetTitle(chat);
        }

        public void UpdateChatPhoto(Chat chat)
        {
            Photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 64, 64);
        }

        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            DeletePanel.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            
        }

        #endregion
    }
}