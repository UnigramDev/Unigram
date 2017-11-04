﻿using System;
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

                InlineBotResults = collection;

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
                await TLMessageDialog.ShowAsync("The admins of this group restricted you from posting media content here.", "Warning", "OK");
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

            var result = resultBase as TLBotInlineResult;
            if (result != null)
            {

            }

            //var result = resultBase as TLBotInlineResult;
            //if (result != null)
            //{
            //    var isFile = result.Type.Equals("file", StringComparison.OrdinalIgnoreCase);
            //    var isVoice = result.Type.Equals("voice", StringComparison.OrdinalIgnoreCase);
            //    var isAudio = result.Type.Equals("audio", StringComparison.OrdinalIgnoreCase);
            //    if (isFile || isAudio || isVoice)
            //    {
            //        var tLDocument = result.Document as TLDocument;
            //        if (tLDocument == null)
            //        {
            //            string text = null;
            //            if (result.ContentUrl != null)
            //            {
            //                Uri uri = new Uri(result.ContentUrl);
            //                try
            //                {
            //                    text = Path.GetFileName(uri.LocalPath);
            //                }
            //                catch (Exception)
            //                {
            //                }
            //                if (text == null)
            //                {
            //                    text = "file.ext";
            //                }
            //            }
            //            tLDocument = new TLDocument
            //            {
            //                Id = 0,
            //                AccessHash = 0,
            //                Date = 0,
            //                MimeType = result.ContentType ?? string.Empty,
            //                Size = 0,
            //                Thumb = new TLPhotoSizeEmpty
            //                {
            //                    Type = string.Empty
            //                },
            //                DCId = 0,
            //                Attributes = new TLVector<TLDocumentAttributeBase>
            //                {
            //                    new TLDocumentAttributeFilename
            //                    {
            //                        FileName = text
            //                    }
            //                }
            //            };

            //            if (isVoice || isAudio)
            //            {
            //                tLDocument.Attributes.Add(new TLDocumentAttributeAudio
            //                {
            //                    Duration = result.Duration ?? 0,
            //                    Title = result.Title,
            //                    Performer = null,
            //                    IsVoice = isVoice
            //                });
            //            }
            //        }

            //        var documentMedia = new TLMessageMediaDocument
            //        {
            //            Document = tLDocument,
            //            Caption = string.Empty
            //        };
            //        message.Media = documentMedia;
            //        documentMedia.NotListened = (isVoice && !(this.With is TLChannel));
            //        message.NotListened = (isVoice && !(this.With is TLChannel));
            //    }
            //    else if (result.Type.Equals("gif", StringComparison.OrdinalIgnoreCase))
            //    {
            //        TLDocumentBase document = result.Document;
            //        if (document != null)
            //        {
            //            TLMessageMediaDocument media = new TLMessageMediaDocument
            //            {
            //                Document = document,
            //                Caption = string.Empty
            //            };
            //            message.Media = media;
            //        }
            //    }
            //    else if (result.Type.Equals("photo", StringComparison.OrdinalIgnoreCase))
            //    {
            //        Telegram.Api.Helpers.Execute.ShowDebugMessage(string.Format("w={0} h={1}\nthumb_url={2}\ncontent_url={3}", new object[]
            //        {
            //            result.W,
            //            result.H,
            //            result.ThumbUrl,
            //            result.ContentUrl
            //        }));
            //        TLFileLocation location = new TLFileLocation
            //        {
            //            DCId = 1,
            //            VolumeId = TLLong.Random(),
            //            LocalId = TLInt.Random(),
            //            Secret = TLLong.Random()
            //        };
            //        TLPhotoCachedSize item2 = new TLPhotoCachedSize
            //        {
            //            Type = "s",
            //            W = result.W ?? 0,
            //            H = result.H ?? 0,
            //            Location = location,
            //            Bytes = new byte[0],
            //            TempUrl = (result.ThumbUrlString ?? result.ContentUrlString)
            //        };
            //        TLPhotoSize item3 = new TLPhotoSize
            //        {
            //            Type = "m",
            //            W = result.W ?? 0,
            //            H = result.H ?? 0,
            //            Location = location,
            //            TempUrl = result.ContentUrlString,
            //            Size = 0
            //        };
            //        if (!string.IsNullOrEmpty(result.ThumbUrl))
            //        {
            //            WebClient webClient = new WebClient();
            //            webClient.OpenReadAsync(new Uri(result.ThumbUrlString, 1));
            //            webClient.add_OpenReadCompleted(delegate (object sender, OpenReadCompletedEventArgs args)
            //            {
            //                if (args.get_Cancelled())
            //                {
            //                    return;
            //                }
            //                if (args.get_Error() != null)
            //                {
            //                    return;
            //                }
            //                string fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);
            //                Stream result = args.get_Result();
            //                try
            //                {
            //                    using (IsolatedStorageFile userStoreForApplication = IsolatedStorageFile.GetUserStoreForApplication())
            //                    {
            //                        if (userStoreForApplication.FileExists(fileName))
            //                        {
            //                            return;
            //                        }
            //                        using (IsolatedStorageFileStream isolatedStorageFileStream = userStoreForApplication.OpenFile(fileName, 4, 2, 1))
            //                        {
            //                            byte[] array = new byte[131072];
            //                            int num;
            //                            while ((num = result.Read(array, 0, 131072)) > 0)
            //                            {
            //                                long position = result.get_Position();
            //                                result.set_Position(position - 10L);
            //                                byte[] array2 = new byte[10];
            //                                result.Read(array2, 0, array2.Length);
            //                                isolatedStorageFileStream.Write(array, 0, num);
            //                            }
            //                        }
            //                    }
            //                }
            //                finally
            //                {
            //                    if (result != null)
            //                    {
            //                        result.Dispose();
            //                    }
            //                }
            //                if (!string.IsNullOrEmpty(result.ContentUrlString))
            //                {
            //                    webClient.OpenReadAsync(new Uri(result.ContentUrlString, 1));
            //                    webClient.add_OpenReadCompleted(delegate (object sender2, OpenReadCompletedEventArgs args2)
            //                    {
            //                        if (args2.get_Cancelled())
            //                        {
            //                            return;
            //                        }
            //                        if (args2.get_Error() != null)
            //                        {
            //                            return;
            //                        }
            //                        using (Stream result2 = args2.get_Result())
            //                        {
            //                            using (IsolatedStorageFile userStoreForApplication2 = IsolatedStorageFile.GetUserStoreForApplication())
            //                            {
            //                                if (!userStoreForApplication2.FileExists(fileName))
            //                                {
            //                                    using (IsolatedStorageFileStream isolatedStorageFileStream2 = userStoreForApplication2.OpenFile(fileName, 4, 2, 1))
            //                                    {
            //                                        byte[] array3 = new byte[131072];
            //                                        int num2;
            //                                        while ((num2 = result2.Read(array3, 0, 131072)) > 0)
            //                                        {
            //                                            isolatedStorageFileStream2.Write(array3, 0, num2);
            //                                        }
            //                                    }
            //                                }
            //                            }
            //                        }
            //                    });
            //                }
            //            });
            //        }

            //        var photo = new TLPhoto
            //        {
            //            Id = TLLong.Random(),
            //            AccessHash = TLLong.Random(),
            //            Date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now),
            //            Sizes = new TLVector<TLPhotoSizeBase>
            //            {
            //                item2,
            //                item3
            //            }
            //        };

            //        var media2 = new TLMessageMediaPhoto
            //        {
            //            Photo = photo,
            //            Caption = string.Empty
            //        };
            //        message.Media = media2;
            //    }
            //}

            var sendText = resultBase.SendMessage as TLBotInlineMessageText;
            if (sendText != null)
            {
                message.Message = sendText.Message;
                message.Entities = sendText.Entities;
                message.HasEntities = sendText.HasEntities;
                //bool arg_878_0 = sendText.NoWebpage;
            }

            var sendMedia = resultBase.SendMessage as TLBotInlineMessageMediaAuto;
            if (sendMedia != null)
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
