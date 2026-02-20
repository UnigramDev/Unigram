//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Telegram.Services;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Telegram.Common
{
    public class WebAppStorage
    {
        private const string STORAGE_FOLDER_NAME = "apps_storage";
        private const string CONFIG_FILE_NAME = "secure_config.json";
        private const string KEY_ALIAS = "MiniAppsKey";
        private const int MAX_STORAGE_SIZE = 5 * 1024 * 1024; // 5MB
        private const int MAX_SECURED_KEYS = 10;

        private readonly IClientService _clientService;

        public long UserId { get; private set; }
        public long BotId { get; private set; }
        public bool Secured { get; private set; }
        public string StorageId { get; private set; }

        public WebAppStorage(IClientService clientService, long botId, bool secured)
        {
            _clientService = clientService;

            UserId = clientService.Options.MyId;
            BotId = botId;
            Secured = secured;
        }

        private async Task<StorageFolder> GetStorageFolderAsync()
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                return await localFolder.CreateFolderAsync(STORAGE_FOLDER_NAME, CreationCollisionOption.OpenIfExists);
            }
            catch
            {
                return null;
            }
        }

        private async Task<StorageFile> GetFileAsync(string storageId = null)
        {
            var folder = await GetStorageFolderAsync();
            if (folder == null) return null;

            var actualStorageId = storageId ?? StorageId;
            var fileName = Secured
                ? $"{actualStorageId}_{BotId}_s"
                : $"{UserId}_{BotId}";

            try
            {
                return await folder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
            }
            catch
            {
                return null;
            }
        }

        public async Task<StorageFile> GetFileAsync()
        {
            if (Secured && string.IsNullOrEmpty(StorageId))
            {
                var config = await ReadConfigAsync();
                var userConfig = config.Values.FirstOrDefault(c => c.UserId == UserId);

                if (userConfig != null)
                {
                    StorageId = userConfig.StorageId;
                }
                else
                {
                    StorageId = Guid.NewGuid().ToString();
                    var newConfig = new WebAppStorageConfig
                    {
                        StorageId = StorageId,
                        UserId = UserId,
                        UserName = _clientService.GetTitle(_clientService.MyId),
                        CreatedAt = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                        EditedAt = DateTimeOffset.Now.ToUnixTimeMilliseconds()
                    };
                    config[StorageId] = newConfig;
                    await SaveConfigAsync(config);
                }
            }

            return await GetFileAsync(StorageId);
        }

        private async Task<StorageFile> GetConfigFileAsync()
        {
            var folder = await GetStorageFolderAsync();
            if (folder == null) return null;

            try
            {
                return await folder.CreateFileAsync(CONFIG_FILE_NAME, CreationCollisionOption.OpenIfExists);
            }
            catch
            {
                return null;
            }
        }

        private IBuffer GetSecretKey()
        {
            try
            {
                // Generate or retrieve encryption key using Windows Credential Locker
                var vault = new Windows.Security.Credentials.PasswordVault();
                Windows.Security.Credentials.PasswordCredential credential = null;

                try
                {
                    credential = vault.Retrieve("TelegramBotStorage", KEY_ALIAS);
                }
                catch
                {
                    // Key doesn't exist, create new one
                    var keyMaterial = CryptographicBuffer.GenerateRandom(32); // 256-bit key
                    var keyString = CryptographicBuffer.EncodeToBase64String(keyMaterial);
                    credential = new Windows.Security.Credentials.PasswordCredential("TelegramBotStorage", KEY_ALIAS, keyString);
                    vault.Add(credential);
                    return keyMaterial;
                }

                credential.RetrievePassword();
                return CryptographicBuffer.DecodeFromBase64String(credential.Password);
            }
            catch
            {
                throw new InvalidOperationException("UNKNOWN_ERROR");
            }
        }

        private async Task<byte[]> GetBytesAsync(StorageFile file)
        {
            try
            {
                var buffer = await FileIO.ReadBufferAsync(file);
                var bytes = new byte[buffer.Length];
                using (var dataReader = DataReader.FromBuffer(buffer))
                {
                    dataReader.ReadBytes(bytes);
                }

                if (Secured)
                {
                    return await DecryptDataAsync(bytes);
                }

                return bytes;
            }
            catch (OutOfMemoryException)
            {
                throw new InvalidOperationException("QUOTA_EXCEEDED");
            }
            catch
            {
                throw new InvalidOperationException("UNKNOWN_ERROR");
            }
        }

        private async Task SetBytesAsync(StorageFile file, byte[] bytes)
        {
            try
            {
                if (Secured)
                {
                    bytes = EncryptData(bytes);
                }

                var buffer = CryptographicBuffer.CreateFromByteArray(bytes);
                await FileIO.WriteBufferAsync(file, buffer);
            }
            catch
            {
                throw new InvalidOperationException("UNKNOWN_ERROR");
            }
        }

        private byte[] EncryptData(byte[] data)
        {
            try
            {
                var key = GetSecretKey();
                var algorithm = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesGcm);
                var cryptoKey = algorithm.CreateSymmetricKey(key);

                // Generate random 96-bit IV
                var iv = CryptographicBuffer.GenerateRandom(12);
                var dataBuffer = CryptographicBuffer.CreateFromByteArray(data);

                // Encrypt with authentication
                var encryptedBuffer = CryptographicEngine.EncryptAndAuthenticate(
                    cryptoKey, dataBuffer, iv, null);

                // Convert buffers to byte[]
                CryptographicBuffer.CopyToByteArray(iv, out byte[] ivBytes);
                CryptographicBuffer.CopyToByteArray(encryptedBuffer.EncryptedData, out byte[] cipherBytes);
                CryptographicBuffer.CopyToByteArray(encryptedBuffer.AuthenticationTag, out byte[] tagBytes);

                // Store as: [ivLength][tagLength][IV][TAG][CIPHERTEXT]
                var result = new byte[2 + ivBytes.Length + tagBytes.Length + cipherBytes.Length];
                result[0] = (byte)ivBytes.Length;
                result[1] = (byte)tagBytes.Length;

                Array.Copy(ivBytes, 0, result, 2, ivBytes.Length);
                Array.Copy(tagBytes, 0, result, 2 + ivBytes.Length, tagBytes.Length);
                Array.Copy(cipherBytes, 0, result, 2 + ivBytes.Length + tagBytes.Length, cipherBytes.Length);

                return result;
            }
            catch
            {
                throw new InvalidOperationException("UNKNOWN_ERROR");
            }
        }

        private async Task<byte[]> DecryptDataAsync(byte[] encryptedData)
        {
            try
            {
                var key = GetSecretKey();
                var algorithm = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesGcm);
                var cryptoKey = algorithm.CreateSymmetricKey(key);

                // Extract lengths
                var ivLength = encryptedData[0];
                var tagLength = encryptedData[1];

                // Extract IV, TAG, and CIPHERTEXT
                var iv = new byte[ivLength];
                Array.Copy(encryptedData, 2, iv, 0, ivLength);

                var tag = new byte[tagLength];
                Array.Copy(encryptedData, 2 + ivLength, tag, 0, tagLength);

                var ciphertext = new byte[encryptedData.Length - 2 - ivLength - tagLength];
                Array.Copy(encryptedData, 2 + ivLength + tagLength, ciphertext, 0, ciphertext.Length);

                // Convert to buffers
                var ivBuffer = CryptographicBuffer.CreateFromByteArray(iv);
                var tagBuffer = CryptographicBuffer.CreateFromByteArray(tag);
                var ciphertextBuffer = CryptographicBuffer.CreateFromByteArray(ciphertext);

                // Decrypt
                var decryptedBuffer = CryptographicEngine.DecryptAndAuthenticate(
                    cryptoKey, ciphertextBuffer, ivBuffer, tagBuffer, null);

                CryptographicBuffer.CopyToByteArray(decryptedBuffer, out byte[] result);
                return result;
            }
            catch
            {
                // Reset if decryption fails
                var file = await GetFileAsync();
                if (file != null)
                {
                    await SetBytesAsync(file, Encoding.UTF8.GetBytes("{}"));
                }
                throw new InvalidOperationException("UNKNOWN_ERROR");
            }
        }

        private async Task<Dictionary<string, string>> GetJsonAsync()
        {
            var file = await GetFileAsync();
            if (file == null) return new Dictionary<string, string>();

            try
            {
                var properties = await file.GetBasicPropertiesAsync();
                if (properties.Size > MAX_STORAGE_SIZE)
                    return new Dictionary<string, string>();

                var bytes = await GetBytesAsync(file);
                var json = Encoding.UTF8.GetString(bytes);
                return JsonSerializer.Deserialize(json, WebAppStorageConfigJsonContext.Default.DictionaryStringString);
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        private async Task SetJsonAsync(Dictionary<string, string> obj)
        {
            try
            {
                var json = JsonSerializer.Serialize(obj, WebAppStorageConfigJsonContext.Default.DictionaryStringString);
                var bytes = Encoding.UTF8.GetBytes(json);

                if (bytes.Length > MAX_STORAGE_SIZE)
                {
                    throw new InvalidOperationException("QUOTA_EXCEEDED");
                }

                var file = await GetFileAsync();
                if (file != null)
                {
                    await SetBytesAsync(file, bytes);
                }
            }
            catch (OutOfMemoryException)
            {
                throw new InvalidOperationException("QUOTA_EXCEEDED");
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                throw new InvalidOperationException("UNKNOWN_ERROR");
            }
        }

        public async Task SetKeyAsync(string key, string value)
        {
            if (key.Length + (value?.Length ?? 0) > MAX_STORAGE_SIZE)
                throw new InvalidOperationException("QUOTA_EXCEEDED");

            var obj = await GetJsonAsync();

            if (value == null)
            {
                obj.Remove(key);
            }
            else
            {
                obj[key] = value;
            }

            if (obj.Count > MAX_SECURED_KEYS && Secured)
                throw new InvalidOperationException("QUOTA_EXCEEDED");

            await SetJsonAsync(obj);

            if (Secured)
            {
                try
                {
                    var config = await ReadConfigAsync();
                    if (config.TryGetValue(StorageId, out var storageConfig))
                    {
                        storageConfig.EditedAt = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                        await SaveConfigAsync(config);
                    }
                }
                catch { }
            }
        }

        public async Task<(string Value, bool CanRestore)> GetKeyAsync(string key)
        {
            var thisJson = await GetJsonAsync();
            var canRestore = false;

            thisJson.TryGetValue(key, out string value);

            if (Secured && value == null && thisJson.Count == 0)
            {
                var activeUsers = GetActiveUsers();
                var config = await ReadConfigAsync();
                var lostConfigs = config.Values.Where(c => !activeUsers.Contains(c.UserId)).ToList();

                foreach (var c in lostConfigs)
                {
                    try
                    {
                        var file = await GetFileAsync(c.StorageId);
                        if (file != null)
                        {
                            var json = await GetJsonFromFileAsync(file);
                            if (json != null && json.ContainsKey(key))
                            {
                                canRestore = true;
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }

            return (value, canRestore);
        }

        public async Task<List<WebAppStorageConfig>> GetStoragesWithKeyAsync(string key)
        {
            var thisJson = await GetJsonAsync();
            if (thisJson.Count > 0)
                throw new InvalidOperationException("STORAGE_NOT_EMPTY");

            var result = new List<WebAppStorageConfig>();
            var activeUsers = GetActiveUsers();
            var config = await ReadConfigAsync();
            var lostConfigs = config.Values.Where(c => !activeUsers.Contains(c.UserId)).ToList();

            foreach (var c in lostConfigs)
            {
                try
                {
                    var file = await GetFileAsync(c.StorageId);
                    if (file != null)
                    {
                        var json = await GetJsonFromFileAsync(file);
                        if (json != null && json.ContainsKey(key))
                        {
                            result.Add(c);
                        }
                    }
                }
                catch { }
            }

            return result;
        }

        public async Task RestoreFromAsync(string id)
        {
            var thisJson = await GetJsonAsync();
            if (thisJson.Count > 0)
                throw new InvalidOperationException("STORAGE_NOT_EMPTY");

            var config = await ReadConfigAsync();
            if (!config.TryGetValue(id, out var storageConfig))
                throw new InvalidOperationException("STORAGE_NOT_FOUND");

            storageConfig.UserId = UserId;
            storageConfig.UserName = _clientService.GetTitle(_clientService.MyId);
            storageConfig.EditedAt = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            await SaveConfigAsync(config);
            StorageId = storageConfig.StorageId;
        }

        public async Task ClearAsync()
        {
            await SetJsonAsync(new Dictionary<string, string>());
        }

        private async Task<Dictionary<string, string>> GetJsonFromFileAsync(StorageFile file)
        {
            try
            {
                var bytes = await GetBytesAsync(file);
                var json = Encoding.UTF8.GetString(bytes);
                return JsonSerializer.Deserialize(json, WebAppStorageConfigJsonContext.Default.DictionaryStringString);
            }
            catch
            {
                return null;
            }
        }

        private HashSet<long> GetActiveUsers()
        {
            var userIds = new HashSet<long>();

            foreach (var session in LifetimeService.Current.Items)
            {
                if (_clientService.Options.TestMode == session.Settings.UseTestDC && session.UserId != 0)
                {
                    userIds.Add(session.UserId);
                }
            }

            return userIds;
        }

        private async Task<Dictionary<string, WebAppStorageConfig>> ReadConfigAsync()
        {
            var config = new Dictionary<string, WebAppStorageConfig>();
            try
            {
                var file = await GetConfigFileAsync();
                if (file == null) return config;

                var bytes = await GetRawBytesAsync(file);
                var json = Encoding.UTF8.GetString(bytes);
                var obj = JsonSerializer.Deserialize(json, WebAppStorageConfigJsonContext.Default.DictionaryStringWebAppStorageConfig);

                config = obj;
            }
            catch { }

            return config;
        }

        private async Task SaveConfigAsync(Dictionary<string, WebAppStorageConfig> config)
        {
            try
            {
                var obj = JsonSerializer.Serialize(config, WebAppStorageConfigJsonContext.Default.DictionaryStringWebAppStorageConfig);

                var file = await GetConfigFileAsync();
                if (file != null)
                {
                    await SaveRawBytesAsync(file, Encoding.UTF8.GetBytes(obj.ToString()));
                }
            }
            catch { }
        }

        private async Task<byte[]> GetRawBytesAsync(StorageFile file)
        {
            try
            {
                var buffer = await FileIO.ReadBufferAsync(file);
                var bytes = new byte[buffer.Length];
                using (var dataReader = DataReader.FromBuffer(buffer))
                {
                    dataReader.ReadBytes(bytes);
                }
                return bytes;
            }
            catch (OutOfMemoryException)
            {
                throw new InvalidOperationException("QUOTA_EXCEEDED");
            }
        }

        private async Task SaveRawBytesAsync(StorageFile file, byte[] bytes)
        {
            var buffer = CryptographicBuffer.CreateFromByteArray(bytes);
            await FileIO.WriteBufferAsync(file, buffer);
        }
    }

    [JsonSerializable(typeof(WebAppStorageConfig))]
    [JsonSerializable(typeof(Dictionary<string, WebAppStorageConfig>))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    public partial class WebAppStorageConfigJsonContext : JsonSerializerContext
    {

    }

    public class WebAppStorageConfig
    {
        [JsonIgnore]
        public string StorageId { get; set; }

        [JsonPropertyName("user_id")]
        public long UserId { get; set; }

        [JsonPropertyName("user_name")]
        public string UserName { get; set; }

        [JsonPropertyName("created_at")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("edited_at")]
        public long EditedAt { get; set; }
    }
}