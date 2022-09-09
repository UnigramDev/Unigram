using Telegram.Td.Api;
using Unigram.Converters;
using Unigram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;

namespace Unigram.Controls
{
    internal class IdentityIcon : Control
    {
        private CustomEmojiIcon Status;
        private FontIcon Icon;

        private bool _templateApplied;

        private IProtoService _protoService;
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
                SetStatus(_protoService, chat);
            }
            else if (_parameter is User user)
            {
                SetStatus(_protoService, user);
            }
            else if (_parameter is Supergroup supergroup)
            {
                SetStatus(supergroup);
            }

            _protoService = null;
            _parameter = null;
        }

        public void SetStatus(IProtoService protoService, Chat chat)
        {
            if (!_templateApplied)
            {
                _protoService = protoService;
                _parameter = chat;
                return;
            }

            if (protoService.TryGetUser(chat, out User user))
            {
                SetStatus(protoService, user, true);
            }
            else if (protoService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                SetStatus(supergroup);
            }
            else
            {
                UnloadObject(ref Icon);
                UnloadObject(ref Status);
            }
        }

        public void SetStatus(IProtoService protoService, User user, bool chatList = false)
        {
            if (!_templateApplied)
            {
                _protoService = protoService;
                _parameter = user;
                return;
            }

            if (user.EmojiStatus != null && (!chatList || user.Id != protoService.Options.MyId))
            {
                LoadObject(ref Status, nameof(Status));
                Status.UpdateEntities(protoService, user.EmojiStatus.CustomEmojiId);

                UnloadObject(ref Icon);
            }
            else
            {
                var verified = user.IsVerified;
                var premium = user.IsPremium && protoService.IsPremiumAvailable && user.Id != protoService.Options.MyId;

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
