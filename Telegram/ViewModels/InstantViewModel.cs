//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views;
using Windows.UI.Xaml.Navigation;
using WinRT;

namespace Telegram.ViewModels
{
    // Title is resolved by name, by the tab header binding TLNavigationService.NavigateToInstant
    // builds in code. Nothing else here is.
    [GeneratedBindableCustomProperty(new[] { "Title" }, new Type[] { })]
    public partial class InstantViewModel : ViewModelBase
    {
        private readonly ITranslateService _translateService;

        private readonly IMessageDelegate _messageDelegate;

        public InstantViewModel(IClientService clientService, ISettingsService settingsService, IStorageService storageService, ITranslateService translateService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _translateService = translateService;
            _messageDelegate = new InstantMessageDelegate(this);
        }

        public ITranslateService TranslateService => _translateService;

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is InstantPageArgs args)
            {
                if (args.Url != null)
                {
                    var response = await ClientService.SendAsync(new GetLinkPreview(args.Url.AsFormattedText(false), null));
                    if (response is LinkPreview linkPreview)
                    {
                        Title = linkPreview.SiteName;
                        return;
                    }

                    if (Uri.TryCreate(args.Url, UriKind.Absolute, out Uri uri))
                    {
                        Title = uri.Host;
                    }
                }
                else
                {

                }
            }
        }

        public Uri ShareLink { get; set; }
        public string ShareTitle { get; set; }

        /// <summary>
        /// The blocks currently on screen, set by the page as it renders. The gallery is
        /// built from these on demand (see MessageDelegate.OpenPageBlockMedia) rather than
        /// collected while rendering, so nothing has to be kept in sync.
        /// </summary>
        public Vector<PageBlock> Blocks { get; set; }

        public MessageViewModel CreateMessage(Message message)
        {
            if (message == null)
            {
                return null;
            }

            return new MessageViewModel(ClientService, _messageDelegate, null, null, null, message, false);
        }

        private string _title;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        public async void Feedback(INavigationService service)
        {
            var response = await ClientService.SendAsync(new SearchPublicChat("previews"));
            if (response is Chat chat)
            {
                service.NavigateToChat(chat);
            }
        }
    }
}
