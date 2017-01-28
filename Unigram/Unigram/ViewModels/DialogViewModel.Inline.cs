using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
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

        private TLMessagesBotResults _inlineBotResults;
        public TLMessagesBotResults InlineBotResults
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
                return _inlineBotResults != null && ((_inlineBotResults.HasSwitchPm && _inlineBotResults.SwitchPm != null) || (_inlineBotResults.Results != null && _inlineBotResults.Results.Count > 0)) ? Visibility.Visible : Visibility.Collapsed;
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

        public async void GetInlineBotResults(string text)
        {
            Debug.WriteLine($"@{CurrentInlineBot.Username}: {CurrentInlineBot.BotInlinePlaceholder}, {text}");

            // TODO: cache

            if (false)
            {

            }
            else
            {
                var response = await ProtoService.GetInlineBotResultsAsync(CurrentInlineBot.ToInputUser(), Peer, null, text, string.Empty);
                if (response.IsSucceeded)
                {
                    foreach (var item in response.Result.Results)
                    {
                        item.QueryId = response.Result.QueryId;
                    }

                    InlineBotResults = response.Result;
                    Debug.WriteLine(response.Result.Results.Count.ToString());
                }
            }
        }

        public void SendBotInlineResult(TLBotInlineResultBase result)
        {
            var currentInlineBot = CurrentInlineBot;
            if (currentInlineBot == null)
            {
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

            ProcessBotInlineResult(message, result, currentInlineBot.Id);

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

            Text = string.Empty;

            //this.Text = string.Empty;
            var previousMessage = InsertSendingMessage(message, false);
            //this.IsEmptyDialog = (base.Items.get_Count() == 0 && this.LazyItems.get_Count() == 0);
            var user = With as TLUser;
            if (user != null && user.IsBot && Messages.Count == 1)
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

        private void ProcessBotInlineResult(TLMessage message, TLBotInlineResultBase resultBase, int botId)
        {
            message.InlineBotResultId = resultBase.Id;
            message.InlineBotResultQueryId = resultBase.QueryId;
            message.ViaBotId = botId;
            message.HasViaBotId = true;

            var venueMedia = resultBase.SendMessage as TLBotInlineMessageMediaVenue;
            if (venueMedia != null)
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

            var geoMedia = resultBase.SendMessage as TLBotInlineMessageMediaGeo;
            if (geoMedia != null)
            {
                message.Media = new TLMessageMediaGeo
                {
                    Geo = geoMedia.Geo
                };
            }

            var contactMedia = resultBase.SendMessage as TLBotInlineMessageMediaContact;
            if (contactMedia != null)
            {
                message.Media = new TLMessageMediaContact
                {
                    PhoneNumber = contactMedia.PhoneNumber,
                    FirstName = contactMedia.FirstName,
                    LastName = contactMedia.LastName,
                    UserId = 0
                };
            }

            var mediaResult = resultBase as TLBotInlineMediaResult;
            if (mediaResult != null)
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
                message.HasEntities = true;
                //bool arg_878_0 = sendText.NoWebpage;
            }

            var sendMedia = resultBase.SendMessage as TLBotInlineMessageMediaAuto;
            if (sendMedia != null)
            {
                var mediaCaption = message.Media as ITLMediaCaption;
                if (mediaCaption != null)
                {
                    mediaCaption.Caption = sendMedia.Caption;
                }
            }

            if (resultBase.SendMessage != null && resultBase.SendMessage.ReplyMarkup != null)
            {
                message.ReplyMarkup = resultBase.SendMessage.ReplyMarkup;
            }
        }
    }
}
