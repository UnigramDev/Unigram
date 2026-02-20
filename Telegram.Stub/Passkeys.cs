//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Buffers.Text;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Data.Json;

namespace Telegram.Stub
{
    public class Passkeys
    {
        public record RelyingParty(string Id, string Name);

        public record User(byte[] Id, string Name, string DisplayName);

        public record CredentialParameter(string Type, int Algorithm);

        public record RegisterData(RelyingParty RelyingParty, User User, string Challenge, IList<CredentialParameter> PubKeyCredParams, int Timeout = 60000);

        public class RegisterResult
        {
            public required byte[] CredentialId { get; set; }
            public required byte[] AttestationObject { get; set; }
            public required string ClientDataJson { get; set; }
        }

        public record Credential(byte[] Id, string Type);

        public record LoginData(string Challenge, string RelyingPartyId, IList<Credential> AllowCredentials, string UserVerification, int Timeout = 60000);

        public class LoginResult
        {
            public required string ClientDataJson { get; set; }
            public required byte[] CredentialId { get; set; }
            public required byte[] AuthenticatorData { get; set; }
            public required byte[] Signature { get; set; }
            public required byte[] UserHandle { get; set; }
        }

        public static RegisterData? DeserializeRegisterData(string jsonData)
        {
            if (!JsonObject.TryParse(jsonData, out JsonObject root))
            {
                return null;
            }

            if (!root.TryGetObject("publicKey", out JsonObject? publicKey))
            {
                return null;
            }

            if (!publicKey.TryGetObject("rp", out JsonObject? rp))
            {
                return null;
            }

            if (!rp.TryGetString("id", out string? rpId) || !rp.TryGetString("name", out string? rpName))
            {
                return null;
            }

            if (!publicKey.TryGetObject("user", out JsonObject? user))
            {
                return null;
            }

            if (!user.TryGetString("id", out string? userId) || !user.TryGetString("name", out string? userName) || !user.TryGetString("displayName", out string? userDisplayName))
            {
                return null;
            }

            if (!publicKey.TryGetString("challenge", out string? challenge))
            {
                return null;
            }

            if (!publicKey.TryGetArray("pubKeyCredParams", out JsonArray? pubKeyCredParams))
            {
                return null;
            }

            var rpData = new RelyingParty(rpId, rpName);
            var userData = new User(Base64Url.DecodeFromChars(userId), userName, userDisplayName);

            var parameters = new List<CredentialParameter>(pubKeyCredParams.Count);

            foreach (var paramBoxed in pubKeyCredParams)
            {
                var param = paramBoxed.GetObject();
                if (param.TryGetString("type", out string? paramType) && param.TryGetInt32("alg", out int? paramAlg))
                {
                    parameters.Add(new CredentialParameter(paramType, paramAlg.Value));
                }
            }

            return new RegisterData(rpData, userData, challenge, parameters, publicKey.GetNamedInt32("timeout", 60000));
        }

        public static LoginData? DeserializeLoginData(string jsonData)
        {
            if (!JsonObject.TryParse(jsonData, out JsonObject root))
            {
                return null;
            }

            if (!root.TryGetObject("publicKey", out JsonObject? publicKey))
            {
                return null;
            }

            if (!publicKey.TryGetString("challenge", out string? challenge) || !publicKey.TryGetString("rpId", out string? rpId) || !publicKey.TryGetString("userVerification", out string? userVerification))
            {
                return null;
            }

            var allowCredentials = new List<Credential>();

            if (publicKey.TryGetArray("allowCredentials", out JsonArray? allowList))
            {
                foreach (var credBoxed in allowList)
                {
                    var cred = credBoxed.GetObject();
                    if (cred.TryGetString("id", out string? paramId) && cred.TryGetString("type", out string? paramType))
                    {
                        allowCredentials.Add(new Credential(Base64Url.DecodeFromChars(paramId), paramType));
                    }
                }
            }

            return new LoginData(challenge, rpId, allowCredentials, userVerification, publicKey.GetNamedInt32("timeout", 60000));
        }

        private static string SerializeClientData(string challenge, string type)
        {
            var obj = new JsonObject
            {
                ["challenge"] = JsonValue.CreateStringValue(challenge),
                ["crossOrigin"] = JsonValue.CreateBooleanValue(false),
                ["origin"] = JsonValue.CreateStringValue("https://telegram.org"),
                ["type"] = JsonValue.CreateStringValue(type),
            };
            return obj.Stringify();
        }

        private static string SerializeClientDataCreate(string challenge)
        {
            return SerializeClientData(challenge, "webauthn.create");
        }

        private static string SerializeClientDataGet(string challenge)
        {
            return SerializeClientData(challenge, "webauthn.get");
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFocus(IntPtr hWnd);

        [DllImport("webauthn.dll")]
        private static extern uint WebAuthNGetApiVersionNumber();

        /// <summary>
        /// Checks if WebAuthn is supported and a user-verifying platform authenticator is available.
        /// </summary>
        /// <returns>True if supported and available, otherwise false.</returns>
        public static bool IsSupported()
        {
            try
            {
                uint version = WebAuthNGetApiVersionNumber();
                return version != 0;
            }
            catch (DllNotFoundException)
            {
                // Handles cases where 'webauthn.dll' is missing (e.g., running on non-Windows OS or very old Windows).
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                // Handles cases where the DLL exists, but the function isn't found (e.g., older Windows version).
                return false;
            }
            catch (Exception)
            {
                // Catch any other unexpected P/Invoke errors and treat as unsupported.
                return false;
            }
        }

        private const uint WEBAUTHN_RP_ENTITY_INFORMATION_CURRENT_VERSION = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WEBAUTHN_RP_ENTITY_INFORMATION
        {
            // Version of this structure, to allow for modifications in the future.
            // This field is required and should be set to CURRENT_VERSION above.
            public uint dwVersion;

            // Identifier for the RP. This field is required.
            public string pwszId;

            // Contains the friendly name of the Relying Party, such as "Acme Corporation", "Widgets Inc" or "Awesome Site".
            // This field is required.
            public string pwszName;

            // Optional URL pointing to RP's logo. 
            public string pwszIcon;
        }

        private const uint WEBAUTHN_USER_ENTITY_INFORMATION_CURRENT_VERSION = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WEBAUTHN_USER_ENTITY_INFORMATION
        {
            // Version of this structure, to allow for modifications in the future.
            // This field is required and should be set to CURRENT_VERSION above.
            public uint dwVersion;

            // Identifier for the User. This field is required.
            public uint cbId;
            public IntPtr pbId;

            // Contains a detailed name for this account, such as "john.p.smith@example.com".
            public string pwszName;

            // Optional URL that can be used to retrieve an image containing the user's current avatar,
            // or a data URI that contains the image data.
            public string pwszIcon;

            // For User: Contains the friendly name associated with the user account by the Relying Party, such as "John P. Smith".
            public string pwszDisplayName;
        }

        private const uint WEBAUTHN_COSE_CREDENTIAL_PARAMETER_CURRENT_VERSION = 1;

        private const string WEBAUTHN_CREDENTIAL_TYPE_PUBLIC_KEY = "public-key";

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WEBAUTHN_COSE_CREDENTIAL_PARAMETER
        {
            public uint dwVersion;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pwszCredentialType;
            public int lAlg;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WEBAUTHN_COSE_CREDENTIAL_PARAMETERS
        {
            public uint cCredentialParameters;
            public IntPtr pCredentialParameters; // Pointer to array of WEBAUTHN_COSE_CREDENTIAL_PARAMETER
        }

        private const uint WEBAUTHN_CLIENT_DATA_CURRENT_VERSION = 1;

        private const string WEBAUTHN_HASH_ALGORITHM_SHA_256 = "SHA-256";

        [StructLayout(LayoutKind.Sequential)]
        private struct WEBAUTHN_CLIENT_DATA
        {
            public uint dwVersion;
            public uint cbClientDataJSON;
            public IntPtr pbClientDataJSON;
            public string pwszHashAlgId;
        }

        private const uint WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS_CURRENT_VERSION = 9;

        private const uint WEBAUTHN_AUTHENTICATOR_ATTACHMENT_ANY = 0;
        private const uint WEBAUTHN_USER_VERIFICATION_REQUIREMENT_PREFERRED = 2;
        private const uint WEBAUTHN_ATTESTATION_CONVEYANCE_PREFERENCE_NONE = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WEBAUTHN_CREDENTIALS
        {
            public uint cCredentials;
            public IntPtr pCredentials;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS
        {
            public uint dwVersion;
            public uint dwTimeoutMilliseconds;
            public WEBAUTHN_CREDENTIALS CredentialList;
            public WEBAUTHN_EXTENSIONS Extensions;
            public uint dwAuthenticatorAttachment;
            public bool bRequireResidentKey;
            public uint dwUserVerificationRequirement;
            public uint dwAttestationConveyancePreference;
            public uint dwFlags;
            public IntPtr pCancellationId;
            public IntPtr pExcludeCredentialList;
            public uint dwEnterpriseAttestation;
            public uint dwLargeBlobSupport;
            public bool bPreferResidentKey;
            public bool bBrowserInPrivateMode;
            public bool bEnablePrf;
            public IntPtr pLinkedDevice;
            public uint cbJsonExt;
            public IntPtr pbJsonExt;
            public IntPtr pPRFGlobalEval;
            public uint cCredentialHints;
            public IntPtr ppwszCredentialHints;
            public bool bThirdPartyPayment;
            public string pwszRemoteWebOrigin;
            public uint cbPublicKeyCredentialCreationOptionsJSON;
            public IntPtr pbPublicKeyCredentialCreationOptionsJSON;
            public uint cbAuthenticatorId;
            public IntPtr pbAuthenticatorId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WEBAUTHN_EXTENSIONS
        {
            public uint cExtensions;
            public IntPtr pExtensions;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WEBAUTHN_CREDENTIAL_ATTESTATION
        {
            public uint dwVersion;
            public string pwszFormatType;
            public uint cbAuthenticatorData;
            public IntPtr pbAuthenticatorData;
            public uint cbAttestation;
            public IntPtr pbAttestation;
            public uint dwAttestationDecodeType;
            public IntPtr pvAttestationDecode;
            public uint cbAttestationObject;
            public IntPtr pbAttestationObject;
            public uint cbCredentialId;
            public IntPtr pbCredentialId;
            public WEBAUTHN_EXTENSIONS Extensions;
            public uint dwUsedTransport;
            public bool bEpAtt;
            public bool bLargeBlobSupported;
            public bool bResidentKey;
            public bool bPrfEnabled;
            public uint cbUnsignedExtensionOutputs;
            public IntPtr pbUnsignedExtensionOutputs;
            public IntPtr pHmacSecret;
            public bool bThirdPartyPayment;
            public uint dwTransports;
            public uint cbClientDataJSON;
            public IntPtr pbClientDataJSON;
            public uint cbRegistrationResponseJSON;
            public IntPtr pbRegistrationResponseJSON;
        }

        [DllImport("webauthn.dll", CharSet = CharSet.Unicode)]
        private static extern int WebAuthNAuthenticatorMakeCredential(
            IntPtr hWnd,
            in WEBAUTHN_RP_ENTITY_INFORMATION pRpInformation,
            in WEBAUTHN_USER_ENTITY_INFORMATION pUserInformation,
            in WEBAUTHN_COSE_CREDENTIAL_PARAMETERS pPubKeyCredParams,
            in WEBAUTHN_CLIENT_DATA pWebAuthNClientData,
            in WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS pWebAuthNMakeCredentialOptions,
            out IntPtr ppWebAuthNCredentialAttestation);

        [DllImport("webauthn.dll", EntryPoint = "WebAuthNFreeCredentialAttestation")]
        private static extern void WebAuthNFreeCredentialAttestation(IntPtr rawCredentialAttestation);

        public static object? MakeCredential(IntPtr hWnd, RegisterData data)
        {
            if (!IsSupported())
            {
                return Marshal.GetExceptionForHR(-2147467263);
            }

            GCHandle userIdHandle = GCHandle.Alloc(data.User.Id, GCHandleType.Pinned);
            IntPtr pUserId = userIdHandle.AddrOfPinnedObject();

            var rpInfo = new WEBAUTHN_RP_ENTITY_INFORMATION
            {
                dwVersion = WEBAUTHN_RP_ENTITY_INFORMATION_CURRENT_VERSION,
                pwszId = data.RelyingParty.Id,
                pwszName = data.RelyingParty.Name
            };

            var userInfo = new WEBAUTHN_USER_ENTITY_INFORMATION
            {
                dwVersion = WEBAUTHN_USER_ENTITY_INFORMATION_CURRENT_VERSION,
                cbId = (uint)data.User.Id.Length,
                pbId = pUserId,
                pwszName = data.User.Name,
                pwszDisplayName = data.User.DisplayName
            };

            int credParamsSize = Marshal.SizeOf<WEBAUTHN_COSE_CREDENTIAL_PARAMETER>();
            IntPtr pCredentialParameters = Marshal.AllocHGlobal(credParamsSize * data.PubKeyCredParams.Count);

            for (int i = 0; i < data.PubKeyCredParams.Count; i++)
            {
                CredentialParameter? param = data.PubKeyCredParams[i];
                var cp = new WEBAUTHN_COSE_CREDENTIAL_PARAMETER
                {
                    dwVersion = WEBAUTHN_COSE_CREDENTIAL_PARAMETER_CURRENT_VERSION,
                    pwszCredentialType = param.Type == "public-key" ? WEBAUTHN_CREDENTIAL_TYPE_PUBLIC_KEY : "",
                    lAlg = param.Algorithm
                };

                IntPtr itemPtr = pCredentialParameters + i * credParamsSize;
                Marshal.StructureToPtr(cp, itemPtr, false);
            }

            var credParamsList = new WEBAUTHN_COSE_CREDENTIAL_PARAMETERS();
            credParamsList.cCredentialParameters = (uint)data.PubKeyCredParams.Count;
            credParamsList.pCredentialParameters = pCredentialParameters;

            var clientDataJson = SerializeClientDataCreate(data.Challenge);
            var clientDataJsonUtf8 = Encoding.UTF8.GetBytes(clientDataJson);
            GCHandle clientDataHandle = GCHandle.Alloc(clientDataJsonUtf8, GCHandleType.Pinned);
            IntPtr pbClientDataJSON = clientDataHandle.AddrOfPinnedObject();

            var clientData = new WEBAUTHN_CLIENT_DATA
            {
                dwVersion = WEBAUTHN_CLIENT_DATA_CURRENT_VERSION,
                cbClientDataJSON = (uint)clientDataJson.Length,
                pbClientDataJSON = pbClientDataJSON,
                pwszHashAlgId = WEBAUTHN_HASH_ALGORITHM_SHA_256
            };

            //auto cancellationId = GUID();
            //CoCreateGuid(&cancellationId);

            var options = new WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS
            {
                dwVersion = WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS_CURRENT_VERSION,
                dwTimeoutMilliseconds = (uint)data.Timeout,
                dwAuthenticatorAttachment = WEBAUTHN_AUTHENTICATOR_ATTACHMENT_ANY,
                bRequireResidentKey = false,
                dwUserVerificationRequirement = WEBAUTHN_USER_VERIFICATION_REQUIREMENT_PREFERRED,
                dwAttestationConveyancePreference = WEBAUTHN_ATTESTATION_CONVEYANCE_PREFERENCE_NONE,
                //options.pCancellationId = &cancellationId;

                //#if defined(WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS_VERSION_4) \
                //	|| defined(WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS_VERSION_5) \
                //	|| defined(WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS_VERSION_6) \
                //	|| defined(WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS_VERSION_7) \
                //	|| defined(WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS_VERSION_8) \
                //	|| defined(WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS_VERSION_9)
                bPreferResidentKey = false
                //#endif
            };

            SetForegroundWindow(hWnd);
            SetFocus(hWnd);

            var hr = WebAuthNAuthenticatorMakeCredential(
                hWnd,
                rpInfo,
                userInfo,
                credParamsList,
                clientData,
                options,
                out IntPtr ppAttestation);

            userIdHandle.Free();
            clientDataHandle.Free();
            Marshal.FreeHGlobal(pCredentialParameters);

            if (hr >= 0 && ppAttestation != IntPtr.Zero)
            {
                var attestation = Marshal.PtrToStructure<WEBAUTHN_CREDENTIAL_ATTESTATION>(ppAttestation);
                var result = new RegisterResult
                {
                    CredentialId = new byte[attestation.cbCredentialId],
                    AttestationObject = new byte[attestation.cbAttestationObject],
                    ClientDataJson = clientDataJson
                };

                Marshal.Copy(attestation.pbCredentialId, result.CredentialId, 0, (int)attestation.cbCredentialId);
                Marshal.Copy(attestation.pbAttestationObject, result.AttestationObject, 0, (int)attestation.cbAttestationObject);

                WebAuthNFreeCredentialAttestation(ppAttestation);

                return result;
            }

            return Marshal.GetExceptionForHR(hr);
        }

        private const uint WEBAUTHN_CREDENTIAL_CURRENT_VERSION = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WEBAUTHN_CREDENTIAL
        {
            public uint dwVersion;
            public uint cbId;
            public IntPtr pbId;
            public string pwszCredentialType;
        }

        private const uint WEBAUTHN_AUTHENTICATOR_GET_ASSERTION_OPTIONS_CURRENT_VERSION = 9;

        private const uint WEBAUTHN_USER_VERIFICATION_REQUIREMENT_REQUIRED = 1;
        private const uint WEBAUTHN_USER_VERIFICATION_REQUIREMENT_DISCOURAGED = 3;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WEBAUTHN_AUTHENTICATOR_GET_ASSERTION_OPTIONS
        {
            public uint dwVersion;
            public uint dwTimeoutMilliseconds;
            public WEBAUTHN_CREDENTIALS CredentialList;
            public WEBAUTHN_EXTENSIONS Extensions;
            public uint dwAuthenticatorAttachment;
            public uint dwUserVerificationRequirement;
            public uint dwFlags;
            public string pwszU2fAppId;
            public IntPtr pbU2fAppId;
            public IntPtr pCancellationId;
            public IntPtr pAllowCredentialList;
            public uint dwCredLargeBlobOperation;
            public uint cbCredLargeBlob;
            public IntPtr pbCredLargeBlob;
            public IntPtr PWEBAUTHN_HMAC_SECRET_SALT_VALUES;
            public bool bBrowserInPrivateMode;
            public IntPtr pLinkedDevice;
            public bool bAutoFill;
            public uint cbJsonExt;
            public IntPtr pbJsonExt;
            public uint cCredentialHints;
            public IntPtr ppwszCredentialHints;
            public string pwszRemoteWebOrigin;
            public uint cbPublicKeyCredentialRequestOptionsJSON;
            public IntPtr pbPublicKeyCredentialRequestOptionsJSON;
            public uint cbAuthenticatorId;
            public IntPtr pbAuthenticatorId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WEBAUTHN_ASSERTION
        {
            public uint dwVersion;
            public uint cbAuthenticatorData;
            public IntPtr pbAuthenticatorData;
            public uint cbSignature;
            public IntPtr pbSignature;
            public WEBAUTHN_CREDENTIAL Credential;
            public uint cbUserId;
            public IntPtr pbUserId;
            public WEBAUTHN_EXTENSIONS Extensions;
            public uint cbCredLargeBlob;
            public IntPtr pbCredLargeBlob;
            public uint dwCredLargeBlobStatus;
            public IntPtr pHmacSecret;
            public uint dwUsedTransport;
            public uint cbUnsignedExtensionOutputs;
            public IntPtr pbUnsignedExtensionOutputs;
            public uint cbClientDataJSON;
            public IntPtr pbClientDataJSON;
            public uint cbAuthenticationResponseJSON;
            public IntPtr pbAuthenticationResponseJSON;
        }

        [DllImport("webauthn.dll", CharSet = CharSet.Unicode)]
        private static extern int WebAuthNAuthenticatorGetAssertion(
          IntPtr hWnd,
          string pwszRpId,
          in WEBAUTHN_CLIENT_DATA pWebAuthNClientData,
          in WEBAUTHN_AUTHENTICATOR_GET_ASSERTION_OPTIONS pWebAuthNGetAssertionOptions,
          out IntPtr ppWebAuthNAssertion);

        [DllImport("webauthn.dll")]
        internal static extern void WebAuthNFreeAssertion(IntPtr pWebAuthNAssertion);

        public static object? GetAssertion(IntPtr hWnd, LoginData data)
        {
            if (!IsSupported())
            {
                return Marshal.GetExceptionForHR(-2147467263);
            }

            var clientDataJson = SerializeClientDataGet(data.Challenge);
            var clientDataJsonUtf8 = Encoding.UTF8.GetBytes(clientDataJson);
            GCHandle clientDataHandle = GCHandle.Alloc(clientDataJsonUtf8, GCHandleType.Pinned);
            IntPtr pbClientDataJSON = clientDataHandle.AddrOfPinnedObject();

            var clientData = new WEBAUTHN_CLIENT_DATA
            {
                dwVersion = WEBAUTHN_CLIENT_DATA_CURRENT_VERSION,
                cbClientDataJSON = (uint)clientDataJson.Length,
                pbClientDataJSON = pbClientDataJSON,
                pwszHashAlgId = WEBAUTHN_HASH_ALGORITHM_SHA_256
            };

            var credentialIds = new List<GCHandle>();
            int pCredentialsSize = Marshal.SizeOf<WEBAUTHN_CREDENTIAL>();
            IntPtr pCredentials = Marshal.AllocHGlobal(pCredentialsSize * data.AllowCredentials.Count);

            for (int i = 0; i < data.AllowCredentials.Count; i++)
            {
                Credential? cred = data.AllowCredentials[i];
                GCHandle pbIdHandle = GCHandle.Alloc(cred.Id, GCHandleType.Pinned);
                IntPtr pbId = pbIdHandle.AddrOfPinnedObject();
                credentialIds.Add(pbIdHandle);

                var credential = new WEBAUTHN_CREDENTIAL
                {
                    dwVersion = WEBAUTHN_CREDENTIAL_CURRENT_VERSION,
                    cbId = (uint)cred.Id.Length,
                    pbId = pbId,
                    pwszCredentialType = WEBAUTHN_CREDENTIAL_TYPE_PUBLIC_KEY
                };

                IntPtr itemPtr = pCredentials + i * pCredentialsSize;
                Marshal.StructureToPtr(credential, itemPtr, false);
            }

            //auto cancellationId = GUID();
            //CoCreateGuid(&cancellationId);

            var options = new WEBAUTHN_AUTHENTICATOR_GET_ASSERTION_OPTIONS
            {
                dwVersion = WEBAUTHN_AUTHENTICATOR_GET_ASSERTION_OPTIONS_CURRENT_VERSION,
                dwTimeoutMilliseconds = (uint)data.Timeout,
                dwUserVerificationRequirement = data.UserVerification switch
                {
                    "required" => WEBAUTHN_USER_VERIFICATION_REQUIREMENT_REQUIRED,
                    "preferred" => WEBAUTHN_USER_VERIFICATION_REQUIREMENT_PREFERRED,
                    _ => WEBAUTHN_USER_VERIFICATION_REQUIREMENT_DISCOURAGED
                },
                //pCancellationId = &cancellationId,
            };

            if (data.AllowCredentials.Count > 0)
            {
                options.CredentialList = new WEBAUTHN_CREDENTIALS
                {
                    cCredentials = (uint)data.AllowCredentials.Count,
                    pCredentials = pCredentials
                };
            }

            SetForegroundWindow(hWnd);
            SetFocus(hWnd);

            var hr = WebAuthNAuthenticatorGetAssertion(
                hWnd,
                data.RelyingPartyId,
                clientData,
                options,
                out IntPtr ppAssertion);

            clientDataHandle.Free();
            credentialIds.ForEach(x => x.Free());
            Marshal.FreeHGlobal(pCredentials);

            if (hr >= 0 && ppAssertion != IntPtr.Zero)
            {
                var assertion = Marshal.PtrToStructure<WEBAUTHN_ASSERTION>(ppAssertion);
                var result = new LoginResult
                {
                    ClientDataJson = clientDataJson,
                    CredentialId = new byte[assertion.Credential.cbId],
                    AuthenticatorData = new byte[assertion.cbAuthenticatorData],
                    Signature = new byte[assertion.cbSignature],
                    UserHandle = new byte[assertion.cbUserId]
                };

                Marshal.Copy(assertion.Credential.pbId, result.CredentialId, 0, (int)assertion.Credential.cbId);
                Marshal.Copy(assertion.pbAuthenticatorData, result.AuthenticatorData, 0, (int)assertion.cbAuthenticatorData);
                Marshal.Copy(assertion.pbSignature, result.Signature, 0, (int)assertion.cbSignature);
                Marshal.Copy(assertion.pbUserId, result.UserHandle, 0, (int)assertion.cbUserId);

                WebAuthNFreeAssertion(ppAssertion);

                return result;
            }

            return Marshal.GetExceptionForHR(hr);
        }

        // TODO: Currently unused due to Windows full screen modal on the first WebAuthN API call
        //private const uint WEBAUTHN_GET_CREDENTIALS_OPTIONS_CURRENT_VERSION = 1;

        //[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        //private struct WEBAUTHN_GET_CREDENTIALS_OPTIONS
        //{
        //    public uint dwVersion;
        //    [MarshalAs(UnmanagedType.LPWStr)]
        //    public string pwszRpId;
        //    public bool bBrowserInPrivateMode;
        //}

        //[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        //private struct WEBAUTHN_CREDENTIAL_DETAILS_LIST
        //{
        //    public uint cCredentialDetails;
        //    public IntPtr ppCredentialDetails;
        //}

        //[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        //private struct WEBAUTHN_CREDENTIAL_DETAILS
        //{
        //    public uint dwVersion;
        //    public uint cbCredentialID;
        //    public IntPtr pbCredentialID;
        //    // WEBAUTHN_RP_ENTITY_INFORMATION
        //    public IntPtr pRpInformation;
        //    // WEBAUTHN_USER_ENTITY_INFORMATION
        //    public IntPtr pUserInformation;
        //    public bool bRemovable;
        //    public bool bBackedUp;
        //}

        //[DllImport("webauthn.dll", CharSet = CharSet.Unicode)]
        //private static extern int WebAuthNGetPlatformCredentialList(WEBAUTHN_GET_CREDENTIALS_OPTIONS pGetCredentialsOptions, out IntPtr ppCredentialDetailsList);

        //[DllImport("webauthn.dll", CharSet = CharSet.Unicode)]
        //private static extern void WebAuthNFreePlatformCredentialList(IntPtr pCredentialDetailsList);

        //public static void Test()
        //{
        //    var options = new WEBAUTHN_GET_CREDENTIALS_OPTIONS();
        //    options.dwVersion = WEBAUTHN_GET_CREDENTIALS_OPTIONS_CURRENT_VERSION;
        //    options.pwszRpId = "telegram.org";

        //    var hr = WebAuthNGetPlatformCredentialList(options, out IntPtr ppCredentialDetailsList);

        //    if (hr >= 0 && ppCredentialDetailsList != IntPtr.Zero)
        //    {
        //        var credentialDetailsList = Marshal.PtrToStructure<WEBAUTHN_CREDENTIAL_DETAILS_LIST>(ppCredentialDetailsList);

        //        int count = (int)credentialDetailsList.cCredentialDetails;
        //        IntPtr basePtr = credentialDetailsList.ppCredentialDetails;

        //        var items = new WEBAUTHN_CREDENTIAL_DETAILS[count];

        //        for (int i = 0; i < count; i++)
        //        {
        //            // Read pointer at index i
        //            IntPtr ptr = Marshal.ReadIntPtr(basePtr, i * IntPtr.Size);

        //            // Convert to managed struct
        //            items[i] = Marshal.PtrToStructure<WEBAUTHN_CREDENTIAL_DETAILS>(ptr);
        //        }

        //        WebAuthNFreePlatformCredentialList(ppCredentialDetailsList);
        //    }
        //}
    }
}
