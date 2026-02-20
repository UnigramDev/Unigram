//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Runtime.CompilerServices;
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

    public partial class IdentityIcon : Control
    {
        private AnimatedImage Particles;
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
                SetStatus(_clientService, chatInviteLinkInfo);
            }

            _clientService = null;
            _parameter = null;
        }

        public void SetStatus(IClientService clientService, MessageSender sender)
        {
            if (clientService.TryGetChat(sender, out Chat chat))
            {
                SetStatus(clientService, chat);
            }
            else if (clientService.TryGetUser(sender, out User user))
            {
                SetStatus(clientService, user);
            }
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
                var status = supergroup.VerificationStatus;

                if (clientService.IsPremiumAvailable && chat.EmojiStatus != null && status.IsFalse())
                {
                    CurrentType = IdentityIconType.None;
                    UnloadTemplateChild(ref Icon);

                    LoadTemplateChild(ref Status);
                    Status.Source = new CustomEmojiFileSource(clientService, chat.EmojiStatus.Type);

                    if (chat.EmojiStatus.Type is EmojiStatusTypeUpgradedGift upgraded)
                    {
                        LoadTemplateChild(ref Particles);
                        Particles.Source = new ParticlesImageSource(upgraded.BackdropColors);
                    }
                    else
                    {
                        UnloadTemplateChild(ref Particles);
                    }
                }
                else
                {
                    SetStatus(supergroup);
                }
            }
            else
            {
                CurrentType = IdentityIconType.None;

                UnloadTemplateChild(ref Icon);
                UnloadTemplateChild(ref Status);
                UnloadTemplateChild(ref Particles);
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

            var status = user.VerificationStatus;

            if (clientService.IsPremiumAvailable && user.EmojiStatus != null && status.IsFalse() && (!chatList || user.Id != clientService.Options.MyId))
            {
                CurrentType = IdentityIconType.Premium;
                UnloadTemplateChild(ref Icon);

                LoadTemplateChild(ref Status);
                Status.Source = new CustomEmojiFileSource(clientService, user.EmojiStatus.Type);

                if (user.EmojiStatus.Type is EmojiStatusTypeUpgradedGift upgraded)
                {
                    LoadTemplateChild(ref Particles);
                    Particles.Source = new ParticlesImageSource(upgraded.BackdropColors);
                }
                else
                {
                    UnloadTemplateChild(ref Particles);
                }
            }
            else
            {
                var premium = user.IsPremium && clientService.IsPremiumAvailable && (!chatList || user.Id != clientService.Options.MyId);

                if (premium || (status != null && (status.IsFake || status.IsScam || status.IsVerified)))
                {
                    CurrentType = status?.IsFake is true
                        ? IdentityIconType.Fake
                        : status?.IsScam is true
                        ? IdentityIconType.Scam
                        : premium
                        ? IdentityIconType.Premium
                        : IdentityIconType.Verified;

                    LoadTemplateChild(ref Icon);
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
                    UnloadTemplateChild(ref Icon);
                }

                UnloadTemplateChild(ref Status);
                UnloadTemplateChild(ref Particles);
            }
        }

        public void SetStatus(IClientService clientService, ChatInviteLinkInfo chat, CustomEmojiIcon botVerified)
        {
            SetStatus(clientService, chat);

            if (chat.VerificationStatus?.BotVerificationIconCustomEmojiId is not null and not 0)
            {
                botVerified.Source = new CustomEmojiFileSource(clientService, chat.VerificationStatus.BotVerificationIconCustomEmojiId);
                botVerified.Visibility = Visibility.Visible;
            }
            else
            {
                botVerified.Source = null;
                botVerified.Visibility = Visibility.Collapsed;
            }
        }

        public void SetStatus(IClientService clientService, ChatInviteLinkInfo chat)
        {
            if (!_templateApplied)
            {
                _clientService = clientService;
                _parameter = chat;
                return;
            }

            var status = chat.VerificationStatus;
            if (status != null && (status.IsFake || status.IsScam || status.IsVerified))
            {
                CurrentType = status.IsFake
                    ? IdentityIconType.Fake
                    : status.IsScam
                    ? IdentityIconType.Scam
                    : IdentityIconType.Verified;

                LoadTemplateChild(ref Icon);
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
                UnloadTemplateChild(ref Icon);
            }

            UnloadTemplateChild(ref Status);
            UnloadTemplateChild(ref Particles);
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
                LoadTemplateChild(ref Status);
                Status.Source = new CustomEmojiFileSource(clientService, icon.CustomEmojiId);

                UnloadTemplateChild(ref Icon);
            }
            else
            {
                //var verified = user.IsVerified;
                //var premium = user.IsPremium && clientService.IsPremiumAvailable && user.Id != clientService.Options.MyId;

                //if (premium || verified)
                {
                    LoadTemplateChild(ref Icon);
                    Icon.Glyph = /*premium ? Icons.Premium16 :*/ Icons.NumberSymbolFilled16;
                }
                //else
                //{
                //    UnloadObject(ref Icon);
                //}

                UnloadTemplateChild(ref Status);
            }

            UnloadTemplateChild(ref Particles);
        }

        public void SetStatus(Supergroup supergroup)
        {
            if (!_templateApplied)
            {
                _parameter = supergroup;
                return;
            }

            var status = supergroup.VerificationStatus;
            if (status != null && (status.IsFake || status.IsScam || status.IsVerified))
            {
                CurrentType = status.IsFake
                    ? IdentityIconType.Fake
                    : status.IsScam
                    ? IdentityIconType.Scam
                    : IdentityIconType.Verified;

                LoadTemplateChild(ref Icon);
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
                UnloadTemplateChild(ref Icon);
            }

            UnloadTemplateChild(ref Status);
            UnloadTemplateChild(ref Particles);
        }

        public void ClearStatus()
        {
            CurrentType = IdentityIconType.None;
            UnloadTemplateChild(ref Icon);
            UnloadTemplateChild(ref Status);
            UnloadTemplateChild(ref Particles);
        }

        #region Helpers

        public void SetStatus(IClientService clientService, User user, CustomEmojiIcon botVerified, bool chatList = false)
        {
            SetStatus(clientService, user, chatList);

            if (user.VerificationStatus?.BotVerificationIconCustomEmojiId is not null and not 0)
            {
                botVerified.Source = new CustomEmojiFileSource(clientService, user.VerificationStatus.BotVerificationIconCustomEmojiId);
                botVerified.Visibility = Visibility.Visible;
            }
            else
            {
                botVerified.Source = null;
                botVerified.Visibility = Visibility.Collapsed;
            }
        }

        public void SetStatus(IClientService clientService, Chat chat, CustomEmojiIcon botVerified)
        {
            long? verification;
            if (clientService.TryGetUser(chat, out User user) && user.Id != clientService.Options.MyId)
            {
                verification = user.VerificationStatus?.BotVerificationIconCustomEmojiId;
                SetStatus(clientService, user, true);
            }
            else if (clientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                verification = supergroup.VerificationStatus?.BotVerificationIconCustomEmojiId;
                SetStatus(clientService, chat);
            }
            else
            {
                verification = null;
                ClearStatus();
            }

            if (verification is not null and not 0)
            {
                botVerified.Source = new CustomEmojiFileSource(clientService, verification.Value);
                botVerified.Visibility = Visibility.Visible;
            }
            else
            {
                botVerified.Source = null;
                botVerified.Visibility = Visibility.Collapsed;
            }
        }

        public void ClearStatus(CustomEmojiIcon botVerified)
        {
            ClearStatus();

            botVerified.Source = null;
            botVerified.Visibility = Visibility.Collapsed;
        }

        #endregion

        private void LoadTemplateChild<T>(ref T element, [CallerArgumentExpression("element")] string name = null)
            where T : DependencyObject
        {
            element ??= GetTemplateChild(name) as T;
        }

        private void UnloadTemplateChild<T>(ref T element)
            where T : DependencyObject
        {
            if (element != null)
            {
                XamlMarkupHelper.UnloadObject(element);
                element = null;
            }
        }
    }

    public partial class IdentityIconAutomationPeer : FrameworkElementAutomationPeer
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
