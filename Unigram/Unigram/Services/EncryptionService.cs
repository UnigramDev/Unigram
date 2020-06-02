using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Views.Popups;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;

namespace Unigram.Services
{
    public interface IEncryptionService
    {
        Task<object> GenerateLocalPasswordAsync();

        Task<bool> EncryptAsync(string publicKey, IList<byte> data, IList<byte> localPassword);
        Task<ByteTuple> DecryptAsync(string publicKey);

        void Delete(string publicKey);
    }

    public class ByteTuple : Tuple<IList<byte>, IList<byte>>
    {
        public ByteTuple(IList<byte> item1, IList<byte> item2)
            : base(item1, item2)
        {
        }
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

        public async Task<object> GenerateLocalPasswordAsync()
        {
            //var response = await _protoService.SendAsync(new GetTonWalletPasswordSalt());
            //if (response is TonWalletPasswordSalt passwordSalt)
            //{
            //    var passwordBuffer = CryptographicBuffer.GenerateRandom(64);
            //    var saltBuffer = CryptographicBuffer.GenerateRandom(32);

            //    CryptographicBuffer.CopyToByteArray(passwordBuffer, out byte[] password);
            //    CryptographicBuffer.CopyToByteArray(saltBuffer, out byte[] salt);

            //    System.Buffer.BlockCopy(passwordSalt.Salt.ToArray(), 0, password, 32, 32);

            //    return new ByteTuple(password, salt);
            //}
            //else if (response is Error error)
            //{
            //    return new Ton.Tonlib.Api.Error(error.Code, error.Message);
            //}

            //return null;

            var passwordBuffer = CryptographicBuffer.GenerateRandom(64);
            var saltBuffer = CryptographicBuffer.GenerateRandom(32);

            CryptographicBuffer.CopyToByteArray(passwordBuffer, out byte[] password);
            CryptographicBuffer.CopyToByteArray(saltBuffer, out byte[] salt);

            return new ByteTuple(password, salt);
        }

        public async Task<bool> EncryptAsync(string publicKey, IList<byte> dataArray, IList<byte> localPassword)
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
                var dialog = new SettingsPasscodeInputPopup();

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

                var localPasswordBuffer = CryptographicBuffer.CreateFromByteArray(localPassword.ToArray());
                var localPasswordString = CryptographicBuffer.EncodeToHexString(localPasswordBuffer);

                var vault = new PasswordVault();
                var password = $"{saltString};{dataString};{localPasswordString};{(dialog.IsSimple ? 1 : 2)}";
                vault.Add(new PasswordCredential($"{_session}", publicKey, password));
            }

            return true;
        }

        public async Task<ByteTuple> DecryptAsync(string publicKey)
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve($"{_session}", publicKey);

            var split = credential.Password.Split(';');

            var saltString = split[0];
            var dataString = split[1];
            var localPasswordString = split[2];
            var typeString = split[3];

            var salt = CryptographicBuffer.DecodeFromHexString(saltString);
            var data = CryptographicBuffer.DecodeFromHexString(dataString);
            var localPassword = CryptographicBuffer.DecodeFromHexString(localPasswordString);

            var dialog = new SettingsPasscodeConfirmPopup(passcode => Task.FromResult(false), true);

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
            CryptographicBuffer.CopyToByteArray(localPassword, out byte[] local);


            return new ByteTuple(result, local);
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
