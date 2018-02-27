using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Telegram.Api.Services.DeviceInfo;
using Windows.ApplicationModel.Background;
using Windows.Storage;

namespace Unigram.Tasks
{
    public sealed class NotificationTask : IBackgroundTask
    {
        private ProtoService _protoService;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            if (_protoService != null)
            {
                return;
            }

            _protoService = new ProtoService(taskInstance.GetDeferral(), new DeviceInfoService());
        }
    }

    internal class ProtoService : ClientResultHandler
    {
        private readonly BackgroundTaskDeferral _deferral;

        private readonly Client _client;
        private readonly IDeviceInfoService _deviceInfoService;

        private readonly Dictionary<string, object> _options = new Dictionary<string, object>();

        private readonly Dictionary<long, Chat> _chats = new Dictionary<long, Chat>();

        private readonly Dictionary<int, SecretChat> _secretChats = new Dictionary<int, SecretChat>();
        private readonly Dictionary<int, User> _users = new Dictionary<int, User>();
        private readonly Dictionary<int, BasicGroup> _basicGroups = new Dictionary<int, BasicGroup>();
        private readonly Dictionary<int, Supergroup> _supergroups = new Dictionary<int, Supergroup>();

        private AuthorizationState _authorizationState;

        public ProtoService(BackgroundTaskDeferral deferral, IDeviceInfoService deviceInfoService)
        {
            Log.SetFilePath(Path.Combine(ApplicationData.Current.LocalFolder.Path, "log"));

            _deferral = deferral;

            _client = Client.Create(this);
            _deviceInfoService = deviceInfoService;

            var parameters = new TdlibParameters
            {
                DatabaseDirectory = ApplicationData.Current.LocalFolder.Path,
                UseSecretChats = true,
                UseMessageDatabase = true,
                ApiId = Telegram.Api.Constants.ApiId,
                ApiHash = Telegram.Api.Constants.ApiHash,
                SystemLanguageCode = "en",
                DeviceModel = _deviceInfoService.DeviceModel,
                SystemVersion = _deviceInfoService.SystemVersion,
                ApplicationVersion = _deviceInfoService.AppVersion
            };

            Task.Run(() =>
            {
                _client.Send(new SetTdlibParameters(parameters));
                _client.Send(new CheckDatabaseEncryptionKey(new byte[0]));
                _client.Run();
            });
        }



        public BaseObject Execute(Function function)
        {
            return _client.Execute(function);
        }



        public void Send(Function function)
        {
            _client.Send(function);
        }

        public void Send(Function function, ClientResultHandler handler)
        {
            _client.Send(function, handler);
        }

        public void Send(Function function, Action<BaseObject> handler)
        {
            _client.Send(function, handler);
        }

        public Task<BaseObject> SendAsync(Function function)
        {
            return _client.SendAsync(function);
        }

        #region Cache

        public AuthorizationState GetAuthorizationState()
        {
            return _authorizationState;
        }

        public int GetMyId()
        {
            var option = GetOption<OptionValueInteger>("my_id");
            if (option != null)
            {
                return option.Value;
            }

            return 0;
        }

        public T GetOption<T>(string key) where T : OptionValue
        {
            if (_options.TryGetValue(key, out object value))
            {
                if (value is OptionValueEmpty)
                {
                    return default(T);
                }

                return (T)value;
            }

            return default(T);
        }

        public string GetTitle(Chat chat)
        {
            if (chat == null)
            {
                return string.Empty;
            }

            //var user = GetUser(chat);
            //if (user != null)
            //{
            //    if (user.Type is UserTypeDeleted)
            //    {
            //        return Strings.Android.HiddenName;
            //    }
            //    else if (user.Id == GetMyId())
            //    {
            //        return Strings.Android.SavedMessages;
            //    }
            //}

            return chat.Title;
        }

        public Chat GetChat(long id)
        {
            if (_chats.TryGetValue(id, out Chat value))
            {
                return value;
            }

            return null;
        }

        public IList<Chat> GetChats(IList<long> ids)
        {
            var result = new List<Chat>(ids.Count);

            foreach (var id in ids)
            {
                var chat = GetChat(id);
                if (chat != null)
                {
                    result.Add(chat);
                }
            }

            return result;
        }

        public IList<User> GetUsers(IList<int> ids)
        {
            var result = new List<User>(ids.Count);

            foreach (var id in ids)
            {
                var user = GetUser(id);
                if (user != null)
                {
                    result.Add(user);
                }
            }

            return result;
        }

        public IList<Chat> GetChats(int count)
        {
            return _chats.Values.Where(x => x.Order != 0).OrderByDescending(x => x.Order).Take(count).ToList();
        }

        public SecretChat GetSecretChat(int id)
        {
            if (_secretChats.TryGetValue(id, out SecretChat value))
            {
                return value;
            }

            return null;
        }

        public SecretChat GetSecretChatForUser(int id)
        {
            return _secretChats.FirstOrDefault(x => x.Value.UserId == id).Value;
        }

        public User GetUser(Chat chat)
        {
            if (chat?.Type is ChatTypePrivate privata)
            {
                return GetUser(privata.UserId);
            }
            else if (chat?.Type is ChatTypeSecret secret)
            {
                return GetUser(secret.UserId);
            }

            return null;
        }

        public User GetUser(int id)
        {
            if (_users.TryGetValue(id, out User value))
            {
                return value;
            }

            return null;
        }

        public BasicGroup GetBasicGroup(int id)
        {
            if (_basicGroups.TryGetValue(id, out BasicGroup value))
            {
                return value;
            }

            return null;
        }

        public Supergroup GetSupergroup(int id)
        {
            if (_supergroups.TryGetValue(id, out Supergroup value))
            {
                return value;
            }

            return null;
        }

        #endregion


        public void OnResult(BaseObject update)
        {
            if (update is UpdateBasicGroup updateBasicGroup)
            {
                _basicGroups[updateBasicGroup.BasicGroup.Id] = updateBasicGroup.BasicGroup;
            }
            else if (update is UpdateCall updateCall)
            {

            }
            else if (update is UpdateChatReadInbox updateChatReadInbox)
            {
                if (_chats.TryGetValue(updateChatReadInbox.ChatId, out Chat value))
                {
                    value.UnreadCount = updateChatReadInbox.UnreadCount;
                    value.LastReadInboxMessageId = updateChatReadInbox.LastReadInboxMessageId;
                }
            }
            else if (update is UpdateConnectionState updateConnectionState)
            {
                switch (updateConnectionState.State)
                {
                    case ConnectionStateWaitingForNetwork waitingForNetwork:
                        break;
                    case ConnectionStateConnecting connecting:
                        break;
                    case ConnectionStateConnectingToProxy connectingToProxy:
                        break;
                    case ConnectionStateUpdating updating:
                        break;
                    case ConnectionStateReady ready:
                        return;
                }
            }
            else if (update is UpdateDeleteMessages updateDeleteMessages)
            {

            }
            else if (update is UpdateNewChat updateNewChat)
            {
                _chats[updateNewChat.Chat.Id] = updateNewChat.Chat;
            }
            else if (update is UpdateNewMessage updateNewMessage)
            {
                if (updateNewMessage.DisableNotification)
                {
                    return;
                }

                var chat = GetChat(updateNewMessage.Message.ChatId);
                if (chat == null)
                {
                    return;
                }

                Native.Tasks.NotificationTask.UpdateToast(chat.Title, updateNewMessage.Message.Content.ToString(), "Default", "Yolo", updateNewMessage.Message.Id.ToString(), updateNewMessage.Message.ChatId.ToString(), string.Empty, DateTime.Now.ToString("o"), "CHANNEL");
            }
            else if (update is UpdateOption updateOption)
            {
                _options[updateOption.Name] = updateOption.Value;
            }
            else if (update is UpdateSecretChat updateSecretChat)
            {
                _secretChats[updateSecretChat.SecretChat.Id] = updateSecretChat.SecretChat;
            }
            else if (update is UpdateSupergroup updateSupergroup)
            {
                _supergroups[updateSupergroup.Supergroup.Id] = updateSupergroup.Supergroup;
            }
            else if (update is UpdateUser updateUser)
            {
                _users[updateUser.User.Id] = updateUser.User;
            }
        }
    }

    internal static class TdExtensions
    {
        public static void Send(this Client client, Function function, Action<BaseObject> handler)
        {
            client.Send(function, new TdHandler(handler));
        }

        public static void Send(this Client client, Function function)
        {
            client.Send(function, null);
        }

        public static Task<BaseObject> SendAsync(this Client client, Function function)
        {
            var tsc = new TdCompletionSource();
            client.Send(function, tsc);

            return tsc.Task;
        }
    }

    internal class TdCompletionSource : TaskCompletionSource<BaseObject>, ClientResultHandler
    {
        public void OnResult(BaseObject result)
        {
            SetResult(result);
        }
    }

    internal class TdHandler : ClientResultHandler
    {
        private Action<BaseObject> _callback;

        public TdHandler(Action<BaseObject> callback)
        {
            _callback = callback;
        }

        public void OnResult(BaseObject result)
        {
            _callback(result);
        }
    }
}
