using Telegram.Controls.Cells;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Chats
{
    public sealed partial class ChatBoostsPage : HostedPage
    {
        public ChatBoostsViewModel ViewModel => DataContext as ChatBoostsViewModel;

        public ChatBoostsPage()
        {
            InitializeComponent();
            Title = Strings.Boosts;
        }

        #region Recycling

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is BoostCell content)
            {
                content.UpdateChatBoost(ViewModel.ClientService, args, OnContainerContentChanging);
            }
        }

        #endregion

        #region Binding

        private string ConvertCount(int count)
        {
            return $"~{count}";
        }

        private string ConvertPercentage(double value)
        {
            return (value / 100).ToString("P1");
        }

        private string ConvertDifference(int current, int next)
        {
            return (next - current).ToString();
        }

        private string ConvertLevel(int level, bool next)
        {
            return string.Format(Strings.BoostsLevel, next ? level + 1 : level);
        }

        #endregion

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.OpenProfile(e.ClickedItem as ChatBoost);
        }
    }
}
