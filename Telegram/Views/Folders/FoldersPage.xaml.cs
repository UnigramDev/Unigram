//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels.Folders;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Views.Folders
{
    public sealed partial class FoldersPage : HostedPage
    {
        public FoldersViewModel ViewModel => DataContext as FoldersViewModel;

        public FoldersPage()
        {
            InitializeComponent();
            Title = Strings.Filters;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.ShowTags))
            {
                for (int i = 0; i < ViewModel.Items.Count; i++)
                {
                    var container = ScrollingHost.ContainerFromIndex(i) as SelectorItem;
                    var content = container?.ContentTemplateRoot as Grid;

                    if (content == null)
                    {
                        continue;
                    }

                    var folder = ViewModel.Items[i];

                    var visual1 = ElementComposition.GetElementVisual(content.Children[3]);
                    var visual2 = ElementComposition.GetElementVisual(content.Children[4]);
                    var tags = ViewModel.ClientService.AreTagsEnabled
                        && ViewModel.ClientService.IsPremium
                        && folder.ColorId != -1;

                    var scale1 = visual1.Compositor.CreateVector3KeyFrameAnimation();
                    //scale.InsertKeyFrame(tags ? 0 : 1, new Vector3(0));
                    scale1.InsertKeyFrame(1, new Vector3(tags ? 1 : 0));
                    scale1.DelayTime = TimeSpan.FromMilliseconds((ViewModel.Items.Count - 1 - i) * 33);
                    scale1.Duration = Constants.FastAnimation;

                    var scale2 = visual1.Compositor.CreateVector3KeyFrameAnimation();
                    //scale.InsertKeyFrame(tags ? 0 : 1, new Vector3(0));
                    scale2.InsertKeyFrame(1, new Vector3(tags ? 0 : 1));
                    scale2.DelayTime = TimeSpan.FromMilliseconds((ViewModel.Items.Count - 1 - i) * 33);
                    scale2.Duration = Constants.FastAnimation;

                    visual1.StartAnimation("Scale", scale1);
                    visual2.StartAnimation("Scale", scale2);
                }
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.Edit(e.ClickedItem as ChatFolderInfo);
        }

        private void AddRecommended_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { CommandParameter: RecommendedChatFolder folder })
            {
                ViewModel.AddRecommended(folder);
            }
        }

        private void Item_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var chat = ScrollingHost.ItemFromContainer(sender) as ChatFolderInfo;
            if (chat.Id == 0)
            {
                return;
            }

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.Delete, chat, Strings.FilterDeleteItem, Icons.Delete);
            flyout.ShowAt(sender, args);
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Item_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content)
            {
                if (args.Item is ChatFolderInfo folder)
                {
                    AutomationProperties.SetName(args.ItemContainer, folder.Title);

                    var glyph = content.Children[0] as TextBlock;
                    var presenter = content.Children[1] as ContentPresenter;
                    var badge = content.Children[2] as ContentControl;
                    var plate = content.Children[3] as Ellipse;
                    var chevron = content.Children[4];

                    var icon = Icons.ParseFolder(folder.Icon);
                    var index = args.ItemIndex;

                    if (index > ViewModel.ClientService.MainChatListPosition)
                    {
                        index--;
                    }

                    plate.Fill = ViewModel.ClientService.GetAccentBrush(folder.ColorId);
                    glyph.Text = Icons.FolderToGlyph(icon).Item1;
                    presenter.Content = folder.Title;
                    badge.Content = index >= ViewModel.ClientService.Options.ChatFolderCountMax
                        ? Icons.LockClosed
                        : folder.HasMyInviteLinks ? Icons.Link : string.Empty;

                    var visual1 = ElementComposition.GetElementVisual(plate);
                    var visual2 = ElementComposition.GetElementVisual(chevron);
                    var tags = ViewModel.ClientService.AreTagsEnabled
                        && ViewModel.ClientService.IsPremium
                        && folder.ColorId != -1;

                    visual1.CenterPoint = new Vector3(6);
                    visual1.Scale = new Vector3(tags ? 1 : 0);

                    visual2.CenterPoint = new Vector3(8);
                    visual2.Scale = new Vector3(tags ? 0 : 1);
                }
                else if (args.Item is RecommendedChatFolder recommended)
                {
                    AutomationProperties.SetName(args.ItemContainer, recommended.Folder.Title + ", " + recommended.Description);

                    var icon = content.Children[0] as TextBlock;
                    var title = content.Children[1] as TextBlock;
                    var subtitle = content.Children[2] as TextBlock;
                    var add = content.Children[3] as Button;

                    icon.Text = Icons.FolderToGlyph(Icons.ParseFolder(recommended.Folder)).Item1;
                    title.Text = recommended.Folder.Title;
                    subtitle.Text = recommended.Description;

                    add.CommandParameter = recommended;
                }

                args.Handled = true;
            }
        }

        #endregion

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Count > 1)
            {
                ScrollingHost.CanReorderItems = false;
                e.Cancel = true;
            }
            else
            {
                var items = ViewModel?.Items;
                if (items == null || items.Count < 2)
                {
                    ScrollingHost.CanReorderItems = false;
                    e.Cancel = true;
                }
                else
                {
                    ScrollingHost.CanReorderItems = true;
                }
            }
        }

        private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            sender.CanReorderItems = false;

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is ChatFolderInfo folder)
            {
                var items = ViewModel?.Items;
                var index = items.IndexOf(folder);

                var compare = items[index > 0 ? index - 1 : index + 1];
                if (compare.Id == 0 && index > 0 && index < items.Count - 1 && !ViewModel.IsPremium)
                {
                    compare = items[index + 1];
                }

                if ((compare.Id == 0 || (folder.Id == 0 && index != 0)) && !ViewModel.IsPremium)
                {
                    ViewModel.Handle(new UpdateChatFolders(ViewModel.ClientService.ChatFolders, 0, false));

                    ToastPopup.ShowPromo(ViewModel.NavigationService, string.Format(Strings.LimitReachedReorderFolder, Strings.FilterAllChats), Strings.PremiumMore, new PremiumSourceLimitExceeded(new PremiumLimitTypeChatFolderCount()));
                }
                else
                {
                    var folders = items.Where(x => x.Id != 0).Select(x => x.Id).ToArray();
                    var main = ViewModel.IsPremium ? items.IndexOf(items.FirstOrDefault(x => x.Id == 0)) : 0;

                    ViewModel.ClientService.Send(new ReorderChatFolders(folders, main));
                }
            }
        }

        #region Binding

        private Visibility ConvertCreate(int count)
        {
            return count < 10 ? Visibility.Visible : Visibility.Collapsed;
        }

        private string ConvertShowTagsFooter(bool premium)
        {
            return premium ? Strings.FolderShowTagsInfo : Strings.FolderShowTagsInfoPremium;
        }

        #endregion
    }
}
