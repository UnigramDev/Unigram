using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Telegram.Api.TL.Messages;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Windows.UI.Xaml;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel
    {
        private TLUser _currentInlineBot;
        public TLUser CurrentInlineBot
        {
            get
            {
                return _currentInlineBot;
            }
            set
            {
                Set(ref _currentInlineBot, value);
            }
        }

        private BotResultsCollection _inlineBotResults;
        public BotResultsCollection InlineBotResults
        {
            get
            {
                return _inlineBotResults;
            }
            set
            {
                Set(ref _inlineBotResults, value);
                RaisePropertyChanged(() => InlineBotResultsVisibility);
            }
        }

        public Visibility InlineBotResultsVisibility
        {
            get
            {
                return _inlineBotResults != null && ((_inlineBotResults.HasSwitchPM && _inlineBotResults.SwitchPM != null) || _inlineBotResults.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public async void ResolveInlineBot(string text, string command = null)
        {
            var username = text.TrimStart('@');
            var cached = CacheService.GetUsers();

            for (int i = 0; i < cached.Count; i++)
            {
                var user = cached[i] as TLUser;
                if (user != null && user.HasBotInlinePlaceholder && user.Username.Equals(text, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentInlineBot = user;
                    GetInlineBotResults(command ?? string.Empty);
                    return;
                }
            }

            if (CurrentInlineBot == null)
            {
                var response = await ProtoService.ResolveUsernameAsync(username);
                if (response.IsSucceeded)
                {
                    CurrentInlineBot = response.Result.Users.FirstOrDefault() as TLUser;
                    GetInlineBotResults(command ?? string.Empty);
                }
            }
        }

        public async void GetInlineBotResults(string query)
        {
            if (CurrentInlineBot == null)
            {
                InlineBotResults = null;
                return;
            }

            if (query != null)
            {
                query = query.Format();
            }

            Debug.WriteLine($"@{CurrentInlineBot.Username}: {CurrentInlineBot.BotInlinePlaceholder}, {query}");

            // TODO: cache

            if (false)
            {

            }
            else
            {
                var collection = new BotResultsCollection(ProtoService, CurrentInlineBot.ToInputUser(), Peer, null, query);
                var result = await collection.LoadMoreItemsAsync(0);

                if (collection.Results != null)
                {
                    InlineBotResults = collection;
                }

                //var response = await ProtoService.GetInlineBotResultsAsync(CurrentInlineBot.ToInputUser(), Peer, null, query, string.Empty);
                //if (response.IsSucceeded)
                //{
                //    foreach (var item in response.Result.Results)
                //    {
                //        item.QueryId = response.Result.QueryId;
                //    }

                //    InlineBotResults = response.Result;
                //    Debug.WriteLine(response.Result.Results.Count.ToString());
                //}
            }
        }

        public async void SendBotInlineResult(TLBotInlineResultBase result)
        {
            var currentInlineBot = CurrentInlineBot;
            if (currentInlineBot == null)
            {
                return;
            }

            var channel = With as TLChannel;
            if (channel != null && channel.HasBannedRights && channel.BannedRights.IsSendGames && result.Type.Equals("game", StringComparison.OrdinalIgnoreCase))
            {
                if (channel.BannedRights.IsForever())
                {
                    await TLMessageDialog.ShowAsync(Strings.Android.AttachMediaRestrictedForever, Strings.Android.AppName, Strings.Android.OK);
                }
                else
                {
                    await TLMessageDialog.ShowAsync(string.Format(Strings.Android.AttachMediaRestricted, BindConvert.Current.BannedUntil(channel.BannedRights.UntilDate)), Strings.Android.AppName, Strings.Android.OK);
                }

                return;
            }

            //var inlineBots = DialogDetailsViewModel.GetInlineBots();
            //if (!inlineBots.Contains(currentInlineBot))
            //{
            //    inlineBots.Insert(0, currentInlineBot);
            //    this._cachedUsernameResults.Clear();
            //}
            //else
            //{
            //    inlineBots.Remove(currentInlineBot);
            //    inlineBots.Insert(0, currentInlineBot);
            //    this._cachedUsernameResults.Clear();
            //}
            //DialogDetailsViewModel.SaveInlineBotsAsync();
            //if (_inlineBotResults == null)
            //{
            //    return;
            //}

            //TLLong arg_74_0 = this._currentInlineBotResults.QueryId;

            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
            var message = TLUtils.GetMessage(SettingsHelper.UserId, Peer.ToPeer(), TLMessageState.Sending, true, true, date, string.Empty, new TLMessageMediaEmpty(), TLLong.Random(), null);
            if (message == null)
            {
                return;
            }

            ProcessBotInlineResult(ref message, result, currentInlineBot.Id);

            //if (this.Reply != null && DialogDetailsViewModel.IsWebPagePreview(this.Reply))
            //{
            //    message._media = ((TLMessagesContainter)this.Reply).WebPageMedia;
            //    this.Reply = this._previousReply;
            //}

            if (Reply != null)
            {
                message.HasReplyToMsgId = true;
                message.ReplyToMsgId = Reply.Id;
                message.Reply = Reply;
                Reply = null;
            }

            SetText(string.Empty);

            //this.Text = string.Empty;
            var previousMessage = InsertSendingMessage(message, false);
            //this.IsEmptyDialog = (base.Items.get_Count() == 0 && this.LazyItems.get_Count() == 0);
            var user = With as TLUser;
            if (user != null && user.IsBot && Items.Count == 1)
            {
                RaisePropertyChanged(() => With);
            }

            CurrentInlineBot = null;
            InlineBotResults = null;

            Execute.BeginOnThreadPool(() =>
            {
                CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
                {
                    var response = await ProtoService.SendInlineBotResultAsync(message, () =>
                    {
                        message.State = TLMessageState.Confirmed;
                    });
                    if (response.IsSucceeded)
                    {
                        message.RaisePropertyChanged(() => message.Media);
                    }
                });
            });

            //base.BeginOnUIThread(delegate
            //{
            //    this.ProcessScroll();
            //    this.RaiseStartGifPlayer(new StartGifPlayerEventArgs(message));
            //});
            //base.BeginOnUIThread(delegate
            //{
            //    this.ClearInlineBotResults();
            //    this.CurrentInlineBot = null;
            //    base.NotifyOfPropertyChange<string>(() => this.BotInlinePlaceholder);
            //});
            //this._debugNotifyOfPropertyChanged = false;
            //base.BeginOnThreadPool(delegate
            //{
            //    this.CacheService.SyncSendingMessage(message, previousMessage, delegate (TLMessageCommon m)
            //    {
            //        DialogDetailsViewModel.SendInternal(message, this.MTProtoService, delegate
            //        {
            //        }, delegate
            //        {
            //            this.Status = string.Empty;
            //        });
            //    });
            //});
        }

        private void ProcessBotInlineResult(ref TLMessage message, TLBotInlineResultBase resultBase, int botId)
        {
            if (message == null || resultBase == null)
            {
                return;
            }

            message.InlineBotResultId = resultBase.Id;
            message.InlineBotResultQueryId = resultBase.QueryId;
            message.ViaBotId = botId;
            message.HasViaBotId = true;

            if (resultBase.SendMessage is TLBotInlineMessageMediaVenue venueMedia)
            {
                message.Media = new TLMessageMediaVenue
                {
                    Title = venueMedia.Title,
                    Address = venueMedia.Address,
                    Provider = venueMedia.Provider,
                    VenueId = venueMedia.VenueId,
                    Geo = venueMedia.Geo
                };
            }
            else if (resultBase.SendMessage is TLBotInlineMessageMediaGeo geoMedia)
            {
                if (geoMedia.Period > 0)
                {
                    message.Media = new TLMessageMediaGeoLive
                    {
                        Geo = geoMedia.Geo,
                        Period = geoMedia.Period
                    };
                }
                else
                {
                    message.Media = new TLMessageMediaGeo
                    {
                        Geo = geoMedia.Geo
                    };
                }
            }
            else if (resultBase.SendMessage is TLBotInlineMessageMediaContact contactMedia)
            {
                message.Media = new TLMessageMediaContact
                {
                    PhoneNumber = contactMedia.PhoneNumber,
                    FirstName = contactMedia.FirstName,
                    LastName = contactMedia.LastName,
                    UserId = 0
                };
            }
            else if (resultBase is TLBotInlineMediaResult mediaResult)
            {
                if (mediaResult.Type.Equals("voice", StringComparison.OrdinalIgnoreCase))
                {
                    message.Media = new TLMessageMediaDocument
                    {
                        Document = mediaResult.Document,
                        Caption = string.Empty,
                        //NotListened = !(this.With is TLChannel)
                    };
                    //message.NotListened = !(this.With is TLChannel);
                }
                else if (mediaResult.Type.Equals("audio", StringComparison.OrdinalIgnoreCase))
                {
                    message.Media = new TLMessageMediaDocument
                    {
                        Document = mediaResult.Document,
                        Caption = string.Empty
                    };
                }
                else if (mediaResult.Type.Equals("sticker", StringComparison.OrdinalIgnoreCase))
                {
                    message.Media = new TLMessageMediaDocument
                    {
                        Document = mediaResult.Document,
                        Caption = string.Empty
                    };
                }
                else if (mediaResult.Type.Equals("file", StringComparison.OrdinalIgnoreCase))
                {
                    message.Media = new TLMessageMediaDocument
                    {
                        Document = mediaResult.Document,
                        Caption = string.Empty
                    };
                }
                else if (mediaResult.Type.Equals("gif", StringComparison.OrdinalIgnoreCase))
                {
                    message.Media = new TLMessageMediaDocument
                    {
                        Document = mediaResult.Document,
                        Caption = string.Empty
                    };
                }
                else if (mediaResult.Type.Equals("photo", StringComparison.OrdinalIgnoreCase))
                {
                    message.Media = new TLMessageMediaPhoto
                    {
                        Photo = mediaResult.Photo,
                        Caption = string.Empty
                    };
                }
            }
            //else if (resultBase is TLBotInlineResult result)
            //{
            //    var file = result.Type.Equals("file", StringComparison.OrdinalIgnoreCase);
            //    var voice = result.Type.Equals("voice", StringComparison.OrdinalIgnoreCase);
            //    var audio = result.Type.Equals("audio", StringComparison.OrdinalIgnoreCase);
            //    if (file || voice || audio)
            //    {

            //    }
            //    else if (result.Type.Equals("gif", StringComparison.OrdinalIgnoreCase) && result.W is int gifWidth && result.H is int gifHeight)
            //    {
            //        var document = new TLDocument
            //        {
            //            MimeType = "video/mp4",
            //            Attributes = new TLVector<TLDocumentAttributeBase>
            //            {
            //                new TLDocumentAttributeAnimated(),
            //                new TLDocumentAttributeVideo
            //                {
            //                    W = gifWidth,
            //                    H = gifHeight,
            //                }
            //            },
            //            Thumb = new TLPhotoSize
            //            {
            //                W = gifWidth,
            //                H = gifHeight,
            //                Type = "s",
            //                Location = new TLFileLocationUnavailable()
            //            }
            //        };

            //        message.Media = new TLMessageMediaDocument { Document = document, HasDocument = true };
            //    }
            //    else if (result.Type.Equals("photo", StringComparison.OrdinalIgnoreCase) && result.W is int photoWidth && result.H is int photoHeight)
            //    {
            //        var photo = new TLPhoto
            //        {
            //            Sizes = new TLVector<TLPhotoSizeBase>
            //            {
            //                new TLPhotoSize
            //                {
            //                    W = photoWidth,
            //                    H = photoHeight,
            //                    Type = "s",
            //                    Location = new TLFileLocationUnavailable()
            //                }
            //            }
            //        };

            //        message.Media = new TLMessageMediaPhoto { Photo = photo, HasPhoto = true };
            //    }
            //}

            if (resultBase.SendMessage is TLBotInlineMessageText sendText)
            {
                message.Message = sendText.Message;
                message.Entities = sendText.Entities;
                message.HasEntities = sendText.HasEntities;
                //bool arg_878_0 = sendText.NoWebpage;
            }
            else if (resultBase.SendMessage is TLBotInlineMessageMediaAuto sendMedia)
            {
                var mediaCaption = message.Media as ITLMessageMediaCaption;
                if (mediaCaption != null)
                {
                    mediaCaption.Caption = sendMedia.Caption;
                }
            }

            if (resultBase.SendMessage != null && resultBase.SendMessage.ReplyMarkup != null)
            {
                message.ReplyMarkup = resultBase.SendMessage.ReplyMarkup;
                message.HasReplyMarkup = true;
            }
        }
    }

    public class BotResultsCollection : IncrementalCollection<TLBotInlineResultBase>
    {
        private readonly IMTProtoService _protoService;

        private readonly TLInputUserBase _bot;
        private readonly TLInputPeerBase _peer;
        private readonly TLInputGeoPointBase _geoPoint;
        private readonly string _query;

        private TLMessagesBotResults _results;
        private string _nextOffset;

        public BotResultsCollection(IMTProtoService protoService, TLInputUserBase bot, TLInputPeerBase peer, TLInputGeoPointBase geoPoint, string query)
        {
            _protoService = protoService;
            _bot = bot;
            _peer = peer;
            _geoPoint = geoPoint;
            _query = query;

            _nextOffset = string.Empty;
        }

        public TLMessagesBotResults Results => _results;

        public bool IsGallery => _results.IsGallery;
        public bool HasNextOffset => _results.HasNextOffset;
        public bool HasSwitchPM => _results.HasSwitchPM;

        public Int64 QueryId => _results.QueryId;
        public String NextOffset => _results.NextOffset;
        public TLInlineBotSwitchPM SwitchPM => _results.SwitchPM;
        public Int32 CacheTime => _results.CacheTime;

        public override async Task<IList<TLBotInlineResultBase>> LoadDataAsync()
        {
            if (_nextOffset != null)
            {
                var response = await _protoService.GetInlineBotResultsAsync(_bot, _peer, _geoPoint, _query, _nextOffset);
                if (response.IsSucceeded)
                {
                    _results = response.Result;
                    _nextOffset = response.Result.NextOffset;

                    foreach (var item in response.Result.Results)
                    {
                        item.QueryId = response.Result.QueryId;
                    }

                    return response.Result.Results;
                }
            }

            return new TLBotInlineResultBase[0];
        }
    }
}
