using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls.Views;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;

namespace Unigram.Services
{
    public interface IEncryptionService
    {
        Task<bool> EncryptAsync(string publicKey, IList<byte> data);
        Task<IList<byte>> DecryptAsync(string publicKey);

        void Delete(string publicKey);
    }

    public class EncryptionService : IEncryptionService
    {
        private readonly int _session;

        private readonly IProtoService _protoService;

        public EncryptionService(int session, IProtoService protoService)
        {
            _session = session;

            _protoService = protoService;
        }

        public async Task<bool> EncryptAsync(string publicKey, IList<byte> dataArray)
        {
            IBuffer keyMaterial;

            //if (await KeyCredentialManager.IsSupportedAsync())
            //{
            //    var boh = await KeyCredentialManager.RequestCreateAsync(publicKey, KeyCredentialCreationOption.ReplaceExisting);
            //    if (boh.Status == KeyCredentialStatus.Success)
            //    {
            //        var boh2 = await boh.Credential.RequestSignAsync()
            //    }
            //}
            //else
            {
                var dialog = new SettingsPasscodeInputView();

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm != ContentDialogResult.Primary)
                {
                    return false;
                }

                var secret = CryptographicBuffer.ConvertStringToBinary(dialog.Passcode, BinaryStringEncoding.Utf8);
                var salt = CryptographicBuffer.GenerateRandom(32);
                var material = PBKDF2(secret, salt);

                var data = CryptographicBuffer.CreateFromByteArray(dataArray.ToArray());

                var objAlg = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesEcbPkcs7);
                var key = objAlg.CreateSymmetricKey(material);

                var encrypt = CryptographicEngine.Encrypt(key, data, null);
                var saltString = CryptographicBuffer.EncodeToHexString(salt);
                var dataString = CryptographicBuffer.EncodeToHexString(encrypt);

                var vault = new PasswordVault();
                vault.Add(new PasswordCredential($"{_session}", publicKey, $"{saltString};{dataString}"));
            }

            return true;
        }

        public async Task<IList<byte>> DecryptAsync(string publicKey)
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve($"{_session}", publicKey);

            var saltString = credential.Password.Split(';')[0];
            var dataString = credential.Password.Split(';')[1];

            var salt = CryptographicBuffer.DecodeFromHexString(saltString);
            var data = CryptographicBuffer.DecodeFromHexString(dataString);

            var dialog = new SettingsPasscodeConfirmView(passcode => Task.FromResult(false), true);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            var secret = CryptographicBuffer.ConvertStringToBinary(dialog.Passcode, BinaryStringEncoding.Utf8);
            var material = PBKDF2(secret, salt);

            var objAlg = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesEcbPkcs7);
            var key = objAlg.CreateSymmetricKey(material);

            var decrypt = CryptographicEngine.Decrypt(key, data, null);
            CryptographicBuffer.CopyToByteArray(decrypt, out byte[] result);

            return result;
        }

        private IBuffer PBKDF2(IBuffer buffSecret, IBuffer buffSalt)
        {
            var algorithm = KeyDerivationAlgorithmNames.Pbkdf2Sha256;
            var iterationCountIn = 100000u;
            var targetSize = 32u;

            var provider = KeyDerivationAlgorithmProvider.OpenAlgorithm(algorithm);
            var pbkdf2Params = KeyDerivationParameters.BuildForPbkdf2(buffSalt, iterationCountIn);
            var keyOriginal = provider.CreateKey(buffSecret);

            var keyDerived = CryptographicEngine.DeriveKeyMaterial(
                keyOriginal,
                pbkdf2Params,
                targetSize
            );

            return keyDerived;
        }



        public void Delete(string publicKey)
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve($"{_session}", publicKey);

            vault.Remove(credential);
        }
    }
}
