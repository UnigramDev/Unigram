//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class ChatInviteLinkCell : GridEx
    {
        private DispatcherTimer _expirationTimer;
        private ChatInviteLink _expirationLink;

        public ChatInviteLinkCell()
        {
            InitializeComponent();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_expirationTimer != null)
            {
                _expirationTimer.Tick -= OnTick;
                _expirationTimer.Stop();
            }
        }

        private void OnTick(object sender, object e)
        {
            if (UpdateProgress(_expirationLink, false))
            {
                StopProgress();
            }
        }

        private void StopProgress()
        {
            if (_expirationTimer != null)
            {
                _expirationTimer.Tick -= OnTick;
                _expirationTimer.Stop();
            }
        }

        private bool UpdateProgress(ChatInviteLink inviteLink, bool started)
        {
            var progress = GetProgress(inviteLink);
            var brush = new SolidColorBrush(GetColor(progress));

            Badge.BorderThickness = new Thickness(progress > 0 && progress < 1 ? 4 : 0);
            Badge.Background = brush;
            Progress.Foreground = brush;
            Progress.Value = progress;

            var diff = inviteLink.ExpirationDate - DateTime.Now.ToTimestamp();
            if (diff > 0)
            {
                int days = diff / 60 / 60 / 24;
                if (days > 0)
                {
                    StatusText.Text = Icons.BulletSpace + Locale.Declension(Strings.R.DaysLeft, days);
                }
                else
                {
                    StatusText.Text = Icons.BulletSpace + TimeSpan.FromSeconds(diff).ToDuration(true);

                    if (started)
                    {
                        if (_expirationTimer == null)
                        {
                            _expirationTimer = new()
                            {
                                Interval = TimeSpan.FromSeconds(1)
                            };

                            _expirationTimer.Tick += OnTick;
                        }

                        _expirationLink = inviteLink;
                        _expirationTimer.Start();
                    }

                    return false;
                }
            }
            else if (inviteLink.MemberCount < inviteLink.MemberLimit && inviteLink.ExpirationDate == 0)
            {
                StatusText.Text = string.Empty;
            }
            else
            {
                StatusText.Text = Icons.BulletSpace + Strings.Expired;
            }

            return true;
        }

        private float GetProgress(ChatInviteLink inviteLink)
        {
            float usageProgress = 1f;
            float timeProgress = 1f;

            if (inviteLink.ExpirationDate > 0)
            {
                long currentTime = DateTime.Now.ToTimestampMilliseconds();
                long expireTime = inviteLink.ExpirationDate * 1000L;
                long date = (inviteLink.EditDate <= 0 ? inviteLink.Date : inviteLink.EditDate) * 1000L;
                long from = currentTime - date;
                long to = expireTime - date;
                timeProgress = (1f - from / (float)to);
            }
            if (inviteLink.MemberLimit > 0)
            {
                usageProgress = (inviteLink.MemberLimit - inviteLink.MemberCount) / (float)inviteLink.MemberLimit;
            }

            return Math.Clamp(Math.Min(timeProgress, usageProgress), 0, 1);
        }

        private Color GetColor(float progress)
        {
            if (progress > 0.5)
            {
                float p = (progress - 0.5f) / 0.5f;
                return ColorsHelper.AlphaBlend(Color.FromArgb(0xff, 0x60, 0xc2, 0x55), Color.FromArgb(0xff, 0xf2, 0xc0, 0x4b), (1f - p));
            }
            else
            {
                float p = progress / 0.5f;
                return ColorsHelper.AlphaBlend(Color.FromArgb(0xff, 0xf2, 0xc0, 0x4b), Color.FromArgb(0xff, 0xeb, 0x60, 0x60), (1f - p));
            }
        }

        public void UpdateInviteLink(IClientService clientService, ChatInviteLink inviteLink)
        {
            TitleText.Text = inviteLink.Name.Length > 0
                ? inviteLink.Name
                : inviteLink.InviteLink.Replace("https://", string.Empty);

            var expired = false;

            if (inviteLink.IsRevoked)
            {
                Badge.Style = BootStrapper.Current.Resources["InfoCaptionBorderStyle"] as Style;
                BadgeIcon.Text = Icons.LinkDiagonalBroken;

                StatusText.Text = Icons.BulletSpace + Strings.Revoked;

                StopProgress();
                expired = true;
            }
            else if (inviteLink.ExpirationDate > 0 || inviteLink.MemberLimit > 0)
            {
                Badge.Style = null;
                BadgeIcon.Text = Icons.LinkDiagonal;

                if (UpdateProgress(inviteLink, true))
                {
                    StopProgress();
                }

                var diff = inviteLink.ExpirationDate - DateTime.Now.ToTimestamp();
                if (diff < 0 || inviteLink.MemberCount == inviteLink.MemberLimit)
                {
                    expired = true;
                }
            }
            else
            {
                Badge.Style = BootStrapper.Current.Resources["AccentCaptionBorderStyle"] as Style;
                BadgeIcon.Text = inviteLink.SubscriptionPricing != null
                    ? Icons.TicketDiagonal
                    : Icons.LinkDiagonal;

                StatusText.Text = string.Empty;

                StopProgress();
            }

            if (inviteLink.MemberCount > 0 && !expired)
            {
                if (inviteLink.MemberLimit > 0)
                {
                    SubtitleText.Text = string.Format("{0}, {1}", Locale.Declension(Strings.R.PeopleJoined, inviteLink.MemberCount), Locale.Declension(Strings.R.PeopleJoinedRemaining, inviteLink.MemberLimit - inviteLink.MemberCount));
                }
                else
                {
                    SubtitleText.Text = Locale.Declension(Strings.R.PeopleJoined, inviteLink.MemberCount);
                }
            }
            else if (inviteLink.MemberLimit > 0 && !expired)
            {
                SubtitleText.Text = Locale.Declension(Strings.R.CanJoin, inviteLink.MemberLimit);
            }
            else
            {
                SubtitleText.Text = Strings.NoOneJoined;
            }

            if (inviteLink.SubscriptionPricing != null)
            {
                PaidPeriod.Text = Strings.StarsParticipantSubscriptionPerMonth;
                PaidRoot.Visibility = Visibility.Visible;

                PaidStarCount.Text = inviteLink.SubscriptionPricing.StarCount.ToString("N0");
            }
            else
            {
                PaidPeriod.Text = string.Empty;
                PaidRoot.Visibility = Visibility.Collapsed;
            }

            Photo.Visibility = Visibility.Collapsed;
            Identity.ClearStatus();
        }

        public void UpdateInviteLinkCount(IClientService clientService, ChatInviteLinkCount inviteLinkCount)
        {
            var user = clientService.GetUser(inviteLinkCount.UserId);

            Badge.Visibility = Visibility.Collapsed;
            TitleText.Text = user.FullName();
            SubtitleText.Text = Locale.Declension(Strings.R.InviteLinkCount, inviteLinkCount.InviteLinkCount);
            StatusText.Text = string.Empty;

            Photo.SetUser(clientService, user, 36);
            Identity.SetStatus(clientService, user);
        }

        public void UpdateInviteLink(ChatFolderInviteLink inviteLink)
        {
            Badge.Style = BootStrapper.Current.Resources["AccentCaptionBorderStyle"] as Style;
            BadgeIcon.Text = Icons.LinkDiagonal;

            TitleText.Text = inviteLink.Name.Length > 0
                ? inviteLink.Name
                : inviteLink.InviteLink.Replace("https://", string.Empty);
            SubtitleText.Text = Locale.Declension(Strings.R.FilterInviteChats, inviteLink.ChatIds.Count);
        }
    }
}
