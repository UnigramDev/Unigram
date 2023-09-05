//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;

namespace Telegram.Controls
{
    public class IdentityIcon : Control
    {
        private AnimatedImage Status;
        private FontIcon Icon;

        private bool _templateApplied;

        private IClientService _clientService;
        private object _parameter;

        public IdentityIcon()
        {
            DefaultStyleKey = typeof(IdentityIcon);
        }

        protected override void OnApplyTemplate()
        {
            _templateApplied = true;

            if (_parameter is Chat chat)
            {
                SetStatus(_clientService, chat);
            }
            else if (_parameter is User user)
            {
                SetStatus(_clientService, user);
            }
            else if (_parameter is ForumTopicIcon icon)
            {
                SetStatus(_clientService, icon);
            }
            else if (_parameter is Supergroup supergroup)
            {
                SetStatus(supergroup);
            }
            else if (_parameter is ChatInviteLinkInfo chatInviteLinkInfo)
            {
                SetStatus(chatInviteLinkInfo);
            }

            _clientService = null;
            _parameter = null;
        }

        public void SetStatus(IClientService clientService, Chat chat)
        {
            if (!_templateApplied)
            {
                _clientService = clientService;
                _parameter = chat;
                return;
            }

            if (clientService.TryGetUser(chat, out User user))
            {
                SetStatus(clientService, user, true);
            }
            else if (clientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                SetStatus(supergroup);
            }
            else
            {
                UnloadObject(ref Icon);
                UnloadObject(ref Status);
            }
        }

        public void SetStatus(IClientService clientService, User user, bool chatList = false)
        {
            if (!_templateApplied)
            {
                _clientService = clientService;
                _parameter = user;
                return;
            }

            if (clientService.IsPremiumAvailable && user.EmojiStatus != null && (!chatList || user.Id != clientService.Options.MyId))
            {
                LoadObject(ref Status, nameof(Status));
                Status.Source = new CustomEmojiFileSource(clientService, user.EmojiStatus.CustomEmojiId);

                UnloadObject(ref Icon);
            }
            else
            {
                var verified = user.IsVerified;
                var premium = user.IsPremium && clientService.IsPremiumAvailable && user.Id != clientService.Options.MyId;

                if (premium || verified)
                {
                    LoadObject(ref Icon, nameof(Icon));
                    Icon.Glyph = premium ? Icons.Premium16 : Icons.Verified16;
                }
                else
                {
                    UnloadObject(ref Icon);
                }

                UnloadObject(ref Status);
            }
        }

        public void SetStatus(ChatInviteLinkInfo chat)
        {
            if (!_templateApplied)
            {
                _parameter = chat;
                return;
            }

            if (chat.IsFake || chat.IsScam || chat.IsVerified)
            {
                LoadObject(ref Icon, nameof(Icon));
                Icon.Glyph = chat.IsFake
                    ? Icons.Fake16
                    : chat.IsScam
                    ? Icons.Scam16
                    : Icons.Verified16;
            }
            else
            {
                UnloadObject(ref Icon);
            }

            UnloadObject(ref Status);
        }

        public void SetStatus(IClientService clientService, ForumTopicIcon icon)
        {
            if (!_templateApplied)
            {
                _clientService = clientService;
                _parameter = icon;
                return;
            }

            if (icon.CustomEmojiId != 0)
            {
                LoadObject(ref Status, nameof(Status));
                Status.Source = new CustomEmojiFileSource(clientService, icon.CustomEmojiId);

                UnloadObject(ref Icon);
            }
            else
            {
                //var verified = user.IsVerified;
                //var premium = user.IsPremium && clientService.IsPremiumAvailable && user.Id != clientService.Options.MyId;

                //if (premium || verified)
                {
                    LoadObject(ref Icon, nameof(Icon));
                    Icon.Glyph = /*premium ? Icons.Premium16 :*/ Icons.NumberSymbolFilled16;
                }
                //else
                //{
                //    UnloadObject(ref Icon);
                //}

                UnloadObject(ref Status);
            }
        }

        public void SetStatus(Supergroup supergroup)
        {
            if (!_templateApplied)
            {
                _parameter = supergroup;
                return;
            }

            if (supergroup.IsFake || supergroup.IsScam || supergroup.IsVerified)
            {
                LoadObject(ref Icon, nameof(Icon));
                Icon.Glyph = supergroup.IsFake
                    ? Icons.Fake16
                    : supergroup.IsScam
                    ? Icons.Scam16
                    : Icons.Verified16;
            }
            else
            {
                UnloadObject(ref Icon);
            }

            UnloadObject(ref Status);
        }

        public void ClearStatus()
        {
            UnloadObject(ref Icon);
            UnloadObject(ref Status);
        }

        private void LoadObject<T>(ref T element, /*[CallerArgumentExpression("element")]*/string name)
            where T : DependencyObject
        {
            element ??= GetTemplateChild(name) as T;
        }

        private void UnloadObject<T>(ref T element)
            where T : DependencyObject
        {
            if (element != null)
            {
                XamlMarkupHelper.UnloadObject(element);
                element = null;
            }
        }
    }
}
