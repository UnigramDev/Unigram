//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;

namespace Telegram.Controls
{
    public enum IdentityIconType
    {
        None,
        Verified,
        Premium,
        Fake,
        Scam
    }

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

        public IdentityIconType CurrentType { get; private set; }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new IdentityIconAutomationPeer(this);
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
                if (clientService.IsPremiumAvailable && chat.EmojiStatus != null && !supergroup.IsFake && !supergroup.IsScam)
                {
                    CurrentType = IdentityIconType.None;
                    UnloadObject(ref Icon);

                    LoadObject(ref Status, nameof(Status));
                    Status.Source = new CustomEmojiFileSource(clientService, chat.EmojiStatus.CustomEmojiId);
                }
                else
                {
                    SetStatus(supergroup);
                }
            }
            else
            {
                CurrentType = IdentityIconType.None;

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

            if (clientService.IsPremiumAvailable && user.EmojiStatus != null && !user.IsFake && !user.IsScam && (!chatList || user.Id != clientService.Options.MyId))
            {
                CurrentType = IdentityIconType.Premium;
                UnloadObject(ref Icon);

                LoadObject(ref Status, nameof(Status));
                Status.Source = new CustomEmojiFileSource(clientService, user.EmojiStatus.CustomEmojiId);
            }
            else
            {
                var premium = user.IsPremium && clientService.IsPremiumAvailable && user.Id != clientService.Options.MyId;

                if (premium || user.IsFake || user.IsScam || user.IsVerified)
                {
                    CurrentType = user.IsFake
                        ? IdentityIconType.Fake
                        : user.IsScam
                        ? IdentityIconType.Scam
                        : premium
                        ? IdentityIconType.Premium
                        : IdentityIconType.Verified;

                    LoadObject(ref Icon, nameof(Icon));
                    Icon.Glyph = CurrentType switch
                    {
                        IdentityIconType.Fake => Icons.Fake16,
                        IdentityIconType.Scam => Icons.Scam16,
                        IdentityIconType.Premium => Icons.Premium16,
                        _ => Icons.Verified16
                    };
                }
                else
                {
                    CurrentType = IdentityIconType.None;
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
                CurrentType = chat.IsFake
                    ? IdentityIconType.Fake
                    : chat.IsScam
                    ? IdentityIconType.Scam
                    : IdentityIconType.Verified;

                LoadObject(ref Icon, nameof(Icon));
                Icon.Glyph = CurrentType switch
                {
                    IdentityIconType.Fake => Icons.Fake16,
                    IdentityIconType.Scam => Icons.Scam16,
                    _ => Icons.Verified16
                };
            }
            else
            {
                CurrentType = IdentityIconType.None;
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
                CurrentType = supergroup.IsFake
                    ? IdentityIconType.Fake
                    : supergroup.IsScam
                    ? IdentityIconType.Scam
                    : IdentityIconType.Verified;

                LoadObject(ref Icon, nameof(Icon));
                Icon.Glyph = CurrentType switch
                {
                    IdentityIconType.Fake => Icons.Fake16,
                    IdentityIconType.Scam => Icons.Scam16,
                    _ => Icons.Verified16
                };
            }
            else
            {
                CurrentType = IdentityIconType.None;
                UnloadObject(ref Icon);
            }

            UnloadObject(ref Status);
        }

        public void ClearStatus()
        {
            CurrentType = IdentityIconType.None;
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

    public class IdentityIconAutomationPeer : FrameworkElementAutomationPeer
    {
        private readonly IdentityIcon _owner;

        public IdentityIconAutomationPeer(IdentityIcon owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            return _owner.CurrentType switch
            {
                IdentityIconType.Fake => Strings.FakeMessage,
                IdentityIconType.Scam => Strings.ScamMessage,
                IdentityIconType.Premium => Strings.AccDescrPremium,
                IdentityIconType.Verified => Strings.AccDescrVerified,
                _ => string.Empty
            };
        }
    }
}
