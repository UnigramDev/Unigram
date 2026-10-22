//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
{
    public sealed partial class ActiveStoriesCell : UserControl
    {
        public ActiveStoriesCell()
        {
            InitializeComponent();
        }

        private ActiveStoriesViewModel _viewModel;
        public ActiveStoriesViewModel ViewModel => _viewModel;

        private string _name;

        private string _automationName;
        public string GetAutomationName()
        {
            return _automationName;
        }

        public void Update(ActiveStoriesViewModel activeStories)
        {
            _viewModel = activeStories;

            var chat = activeStories.Chat;
            if (activeStories.ClientService.TryGetUser(chat, out User user))
            {
                _name = activeStories.IsMyStory ? Strings.MyStory : user.FirstName;
                _automationName = activeStories.IsMyStory ? Strings.MyStory : string.Format(Strings.AccDescrStoryBy, user.FirstName);
            }
            else
            {
                _name = chat.Title;
                _automationName = string.Format(Strings.AccDescrStoryBy, chat.Title);
            }

            Avatar.Update(activeStories);
            UpdateTitle();
        }

        public ChatActiveStories Trigger
        {
            set
            {
                Avatar.UpdateSegments(value);

                // The binding can run before Update hands over a recycled cell's new item.
                if (_viewModel != null && _viewModel.ChatId == value?.ChatId)
                {
                    UpdateTitle();
                }
            }
        }

        // Your own posts, while any is in flight or failed, take over the title.
        private void UpdateTitle()
        {
            Title.Text = Avatar.IsPostFailed
                ? Strings.FailedStory
                : Avatar.IsPosting
                ? Strings.UploadingStory
                : _name;
        }
    }
}
