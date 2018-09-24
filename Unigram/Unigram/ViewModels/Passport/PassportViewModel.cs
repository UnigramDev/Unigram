using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;
using Unigram.Core.Common;
using Unigram.Controls;
using Unigram.Common;
using Unigram.Views.Passport;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Controls;
using Unigram.Core.Services;

namespace Unigram.ViewModels.Passport
{
    public enum PassportState
    {
        Password,
        Manage,
        Form,
    }

    public class PassportViewModel : TLViewModelBase
    {
        public PassportViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<PassportElement>();

            PasswordCommand = new RelayCommand(PasswordExecute);

            HelpCommand = new RelayCommand(HelpExecute);
            AddCommand = new RelayCommand(AddExecute);
            NavigateCommand = new RelayCommand<PassportElement>(NavigateExecute);
        }

        private PassportState _state;
        public PassportState State
        {
            get { return _state; }
            set { Set(ref _state, value); }
        }

        private PassportAuthorizationForm _authorizationForm;
        public PassportAuthorizationForm AuthorizationForm
        {
            get { return _authorizationForm; }
            set { Set(ref _authorizationForm, value); }
        }

        private int _botId;
        public int BotId
        {
            get { return _botId; }
            set { Set(ref _botId, value); }
        }

        public MvxObservableCollection<PassportElement> Items { get; private set; }

        private bool IsPersonalDocument(PassportElementType type)
        {
            return type is PassportElementTypeDriverLicense ||
                    type is PassportElementTypePassport ||
                    type is PassportElementTypeInternalPassport ||
                    type is PassportElementTypeIdentityCard;
        }

        private bool IsAddressDocument(PassportElementType type)
        {
            return type is PassportElementTypeUtilityBill ||
                    type is PassportElementTypeBankStatement ||
                    type is PassportElementTypePassportRegistration ||
                    type is PassportElementTypeTemporaryRegistration ||
                    type is PassportElementTypeRentalAgreement;
        }

        #region Password

        private GetPassportAuthorizationForm _authorizationRequest;

        private string _passwordHint;
        public string PasswordHint
        {
            get { return _passwordHint; }
            set { Set(ref _passwordHint, value); }
        }

        private string _password;
        public string Password
        {
            get { return _password; }
            set { Set(ref _password, value); }
        }

        public RelayCommand PasswordCommand { get; }
        private async void PasswordExecute()
        {
            Error error;
            if (_authorizationRequest != null)
            {
                error = await InitializeFormAsync(_password);
            }
            else
            {
                error = await InitializeManageAsync(_password);
            }
        }

        #endregion

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            State = PassportState.Password;

            if (state.Count > 0)
            {
                var botId = (int)state["bot_id"];
                var scope = state["scope"] as string;
                var publicKey = state["public_key"] as string;
                var nonce = state["nonce"] as string;

                state.Clear();
                _authorizationRequest = new GetPassportAuthorizationForm(botId, scope, publicKey, nonce, string.Empty);
            }

            var response = await ProtoService.SendAsync(new GetPasswordState());
            if (response is PasswordState passwordState)
            {
                PasswordHint = passwordState.PasswordHint;
            }
        }

        private async Task<Error> InitializeFormAsync(string password)
        {
            var request = _authorizationRequest;
            if (request != null)
            {
                _authorizationRequest = null;
                request.Password = password;
            }

            var response = await ProtoService.SendAsync(request);
            if (response is PassportAuthorizationForm authorizationForm)
            {
                State = PassportState.Form;

                BotId = request.BotUserId;
                AuthorizationForm = authorizationForm;

                int size = authorizationForm.RequiredElements.Count;
                List<PassportSuitableElement> personalDocuments = new List<PassportSuitableElement>();
                List<PassportSuitableElement> addressDocuments = new List<PassportSuitableElement>();
                int personalCount = 0;
                int addressCount = 0;
                bool hasPersonalInfo = false;
                bool hasAddressInfo = false;

                foreach (var secureRequiredType in authorizationForm.RequiredElements)
                {
                    if (secureRequiredType.SuitableElements.Count > 1)
                    {
                        var requiredType = secureRequiredType.SuitableElements[0];

                        if (IsPersonalDocument(requiredType.Type))
                        {
                            foreach (var innerType in secureRequiredType.SuitableElements)
                            {
                                personalDocuments.Add(innerType);
                            }
                            personalCount++;
                        }
                        else if (IsAddressDocument(requiredType.Type))
                        {
                            foreach (var innerType in secureRequiredType.SuitableElements)
                            {
                                addressDocuments.Add(innerType);
                            }
                            addressCount++;
                        }
                    }
                    else if (secureRequiredType.SuitableElements.Count > 0)
                    {
                        var requiredType = secureRequiredType.SuitableElements[0];
                        if (IsPersonalDocument(requiredType.Type))
                        {
                            personalDocuments.Add(requiredType);
                            personalCount++;
                        }
                        else if (IsAddressDocument(requiredType.Type))
                        {
                            addressDocuments.Add(requiredType);
                            addressCount++;
                        }
                        else if (requiredType.Type is PassportElementTypePersonalDetails)
                        {
                            hasPersonalInfo = true;
                        }
                        else if (requiredType.Type is PassportElementTypeAddress)
                        {
                            hasAddressInfo = true;
                        }
                    }
                }

                bool separatePersonal = !hasPersonalInfo || personalCount > 1;
                bool separateAddress = !hasAddressInfo || addressCount > 1;

                foreach (var secureRequiredType in authorizationForm.RequiredElements)
                {
                    List<PassportSuitableElement> documentTypes;
                    PassportSuitableElement requiredType;
                    bool documentOnly;
                    if (secureRequiredType.SuitableElements.Count > 1)
                    {
                        requiredType = secureRequiredType.SuitableElements[0];

                        if (separatePersonal && IsPersonalDocument(requiredType.Type) || separateAddress && IsAddressDocument(requiredType.Type))
                        {
                            documentTypes = new List<PassportSuitableElement>();
                            foreach (var innerType in secureRequiredType.SuitableElements)
                            {
                                documentTypes.Add(innerType);
                            }
                            if (IsPersonalDocument(requiredType.Type))
                            {
                                requiredType = new PassportSuitableElement();
                                requiredType.Type = new PassportElementTypePersonalDetails();
                            }
                            else
                            {
                                requiredType = new PassportSuitableElement();
                                requiredType.Type = new PassportElementTypeAddress();
                            }

                            documentOnly = true;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else if (secureRequiredType.SuitableElements.Count > 0)
                    {
                        requiredType = secureRequiredType.SuitableElements[0];
                        if (requiredType.Type is PassportElementTypePhoneNumber || requiredType.Type is PassportElementTypeEmailAddress)
                        {
                            documentTypes = null;
                            documentOnly = false;
                        }
                        else if (requiredType.Type is PassportElementTypePersonalDetails)
                        {
                            if (separatePersonal)
                            {
                                documentTypes = null;
                            }
                            else
                            {
                                documentTypes = personalDocuments;
                            }
                            documentOnly = false;
                        }
                        else if (requiredType.Type is PassportElementTypeAddress)
                        {
                            if (separateAddress)
                            {
                                documentTypes = null;
                            }
                            else
                            {
                                documentTypes = addressDocuments;
                            }
                            documentOnly = false;
                        }
                        else if (separatePersonal && IsPersonalDocument(requiredType.Type))
                        {
                            documentTypes = new List<PassportSuitableElement>();
                            documentTypes.Add(requiredType);
                            requiredType = new PassportSuitableElement();
                            requiredType.Type = new PassportElementTypePersonalDetails();
                            documentOnly = true;
                        }
                        else if (separateAddress && IsAddressDocument(requiredType.Type))
                        {
                            documentTypes = new List<PassportSuitableElement>();
                            documentTypes.Add(requiredType);
                            requiredType = new PassportSuitableElement();
                            requiredType.Type = new PassportElementTypeAddress();
                            documentOnly = true;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }

                    addField(authorizationForm, requiredType, documentTypes, documentOnly, false);
                }
            }
            else if (response is Error error)
            {
                return error;
            }

            return null;
        }

        private async Task<Error> InitializeManageAsync(string password)
        {
            var response = await ProtoService.SendAsync(new GetAllPassportElements(password));
            if (response is PassportElements elements)
            {
                State = PassportState.Manage;

                Items.ReplaceWith(elements.Elements);
            }
            else if (response is Error error)
            {
                return error;
            }

            return null;
        }

        class ErrorSourceComparator : IComparer<PassportElementErrorSource>
        {
            int getErrorValue(PassportElementErrorSource error)
            {
                /*if (error is TLRPC.TL_secureValueError)
                {
                    return 0;
                }
                else*/
                if (error is PassportElementErrorSourceFrontSide)
                {
                    return 1;
                }
                else if (error is PassportElementErrorSourceReverseSide)
                {
                    return 2;
                }
                else if (error is PassportElementErrorSourceSelfie)
                {
                    return 3;
                }
                else if (error is PassportElementErrorSourceTranslationFile)
                {
                    return 4;
                }
                else if (error is PassportElementErrorSourceTranslationFiles)
                {
                    return 5;
                }
                else if (error is PassportElementErrorSourceFile)
                {
                    return 6;
                }
                else if (error is PassportElementErrorSourceFiles)
                {
                    return 7;
                }
                else if (error is PassportElementErrorSourceDataField dataField)
                {
                    return getFieldCost(dataField.FieldName);
                }
                return 100;
            }

            private int getFieldCost(String key)
            {
                switch (key)
                {
                    case "first_name":
                    case "first_name_native":
                        return 20;
                    case "middle_name":
                    case "middle_name_native":
                        return 21;
                    case "last_name":
                    case "last_name_native":
                        return 22;
                    case "birth_date":
                        return 23;
                    case "gender":
                        return 24;
                    case "country_code":
                        return 25;
                    case "residence_country_code":
                        return 26;
                    case "document_no":
                        return 27;
                    case "expiry_date":
                        return 28;
                    case "street_line1":
                        return 29;
                    case "street_line2":
                        return 30;
                    case "post_code":
                        return 31;
                    case "city":
                        return 32;
                    case "state":
                        return 33;
                }
                return 100;
            }

            public int Compare(PassportElementErrorSource x, PassportElementErrorSource y)
            {
                int val1 = getErrorValue(x);
                int val2 = getErrorValue(y);
                if (val1 < val2)
                {
                    return -1;
                }
                else if (val1 > val2)
                {
                    return 1;
                }
                return 0;
            }
        }

        private String getNameForType(PassportElementType type)
        {
            if (type is PassportElementTypePersonalDetails)
            {
                return "personal_details";
            }
            else if (type is PassportElementTypePassport)
            {
                return "passport";
            }
            else if (type is PassportElementTypeInternalPassport)
            {
                return "internal_passport";
            }
            else if (type is PassportElementTypeDriverLicense)
            {
                return "driver_license";
            }
            else if (type is PassportElementTypeIdentityCard)
            {
                return "identity_card";
            }
            else if (type is PassportElementTypeUtilityBill)
            {
                return "utility_bill";
            }
            else if (type is PassportElementTypeAddress)
            {
                return "address";
            }
            else if (type is PassportElementTypeBankStatement)
            {
                return "bank_statement";
            }
            else if (type is PassportElementTypeRentalAgreement)
            {
                return "rental_agreement";
            }
            else if (type is PassportElementTypeTemporaryRegistration)
            {
                return "temporary_registration";
            }
            else if (type is PassportElementTypePassportRegistration)
            {
                return "passport_registration";
            }
            else if (type is PassportElementTypeEmailAddress)
            {
                return "email";
            }
            else if (type is PassportElementTypePhoneNumber)
            {
                return "phone";
            }
            return "";
        }

        //private Dictionary<string, string> GenerateErrorsMap(PassportAuthorizationForm form)
        //{
        //    try
        //    {
        //        var errors = form.Errors.OrderBy(x => x.Source, new ErrorSourceComparator()).ToList();
        //        for (int a = 0, size = errors.Count; a < size; a++)
        //        {
        //            var secureValueError = errors[a];
        //            String key;
        //            String description;
        //            String target;

        //            String field = null;
        //            byte[] file_hash = null;

        //            if (secureValueError.Source is PassportElementErrorSourceFrontSide)
        //            {
        //                TLRPC.TL_secureValueErrorFrontSide secureValueErrorFrontSide = (TLRPC.TL_secureValueErrorFrontSide)secureValueError;
        //                key = getNameForType(secureValueError.Type);
        //                description = secureValueError.Message;
        //                file_hash = secureValueErrorFrontSide.file_hash;
        //                target = "front";
        //            }
        //            else if (secureValueError.Source is PassportElementErrorSourceReverseSide)
        //            {
        //                TLRPC.TL_secureValueErrorReverseSide secureValueErrorReverseSide = (TLRPC.TL_secureValueErrorReverseSide)secureValueError;
        //                key = getNameForType(secureValueError.Type);
        //                description = secureValueError.Message;
        //                file_hash = secureValueErrorReverseSide.file_hash;
        //                target = "reverse";
        //            }
        //            else if (secureValueError.Source is PassportElementErrorSourceSelfie)
        //            {
        //                TLRPC.TL_secureValueErrorSelfie secureValueErrorSelfie = (TLRPC.TL_secureValueErrorSelfie)secureValueError;
        //                key = getNameForType(secureValueError.Type);
        //                description = secureValueError.Message;
        //                file_hash = secureValueErrorSelfie.file_hash;
        //                target = "selfie";
        //            }
        //            else if (secureValueError.Source is PassportElementErrorSourceTranslationFile)
        //            {
        //                TLRPC.TL_secureValueErrorTranslationFile secureValueErrorTranslationFile = (TLRPC.TL_secureValueErrorTranslationFile)secureValueError;
        //                key = getNameForType(secureValueError.Type);
        //                description = secureValueError.Message;
        //                file_hash = secureValueErrorTranslationFile.file_hash;
        //                target = "translation";
        //            }
        //            else if (secureValueError.Source is PassportElementErrorSourceTranslationFiles)
        //            {
        //                TLRPC.TL_secureValueErrorTranslationFiles secureValueErrorTranslationFiles = (TLRPC.TL_secureValueErrorTranslationFiles)secureValueError;
        //                key = getNameForType(secureValueError.Type);
        //                description = secureValueError.Message;
        //                target = "translation";
        //            }
        //            else if (secureValueError.Source is PassportElementErrorSourceFile)
        //            {
        //                TLRPC.TL_secureValueErrorFile secureValueErrorFile = (TLRPC.TL_secureValueErrorFile)secureValueError;
        //                key = getNameForType(secureValueError.Type);
        //                description = secureValueError.Message;
        //                file_hash = secureValueErrorFile.file_hash;
        //                target = "files";
        //            }
        //            else if (secureValueError.Source is PassportElementErrorSourceFiles)
        //            {
        //                TLRPC.TL_secureValueErrorFiles secureValueErrorFiles = (TLRPC.TL_secureValueErrorFiles)secureValueError;
        //                key = getNameForType(secureValueError.Type);
        //                description = secureValueError.Message;
        //                target = "files";
        //            }
        //            else if (secureValueError is TLRPC.TL_secureValueError)
        //            {
        //                TLRPC.TL_secureValueError secureValueErrorAll = (TLRPC.TL_secureValueError)secureValueError;
        //                key = getNameForType(secureValueError.Type);
        //                description = secureValueError.Message;
        //                file_hash = secureValueErrorAll.hash;
        //                target = "error_all";
        //            }
        //            else if (secureValueError.Source is PassportElementErrorSourceDataField)
        //            {
        //                TLRPC.TL_secureValueErrorData secureValueErrorData = (TLRPC.TL_secureValueErrorData)secureValueError;
        //                boolean found = false;
        //                for (int b = 0; b < form.values.size(); b++)
        //                {
        //                    TLRPC.TL_secureValue value = form.values.get(b);
        //                    if (value.data != null && Arrays.equals(value.data.data_hash, secureValueErrorData.data_hash))
        //                    {
        //                        found = true;
        //                        break;
        //                    }
        //                }
        //                if (!found)
        //                {
        //                    continue;
        //                }
        //                key = getNameForType(secureValueErrorData.type);
        //                description = secureValueErrorData.text;
        //                field = secureValueErrorData.field;
        //                file_hash = secureValueErrorData.data_hash;
        //                target = "data";
        //            }
        //            else
        //            {
        //                continue;
        //            }
        //            HashMap<String, String> vals = errorsMap.get(key);
        //            if (vals == null)
        //            {
        //                vals = new HashMap<>();
        //                errorsMap.put(key, vals);
        //                mainErrorsMap.put(key, description);
        //            }
        //            String hash;
        //            if (file_hash != null)
        //            {
        //                hash = Base64.encodeToString(file_hash, Base64.NO_WRAP);
        //            }
        //            else
        //            {
        //                hash = "";
        //            }
        //            if ("data".Equals(target))
        //            {
        //                if (field != null)
        //                {
        //                    vals.put(field, description);
        //                }
        //            }
        //            else if ("files".Equals(target))
        //            {
        //                if (file_hash != null)
        //                {
        //                    vals.put("files" + hash, description);
        //                }
        //                else
        //                {
        //                    vals.put("files_all", description);
        //                }
        //            }
        //            else if ("selfie".Equals(target))
        //            {
        //                vals.put("selfie" + hash, description);
        //            }
        //            else if ("translation".Equals(target))
        //            {
        //                if (file_hash != null)
        //                {
        //                    vals.put("translation" + hash, description);
        //                }
        //                else
        //                {
        //                    vals.put("translation_all", description);
        //                }
        //            }
        //            else if ("front".Equals(target))
        //            {
        //                vals.put("front" + hash, description);
        //            }
        //            else if ("reverse".Equals(target))
        //            {
        //                vals.put("reverse" + hash, description);
        //            }
        //            else if ("error_all".Equals(target))
        //            {
        //                vals.put("error_all", description);
        //            }
        //        }
        //    }
        //    catch (Exception ignore)
        //    {

        //    }
        //}

        private void addField(PassportAuthorizationForm form, PassportSuitableElement requiredType, List<PassportSuitableElement> documentTypes, bool documentOnly, bool last)
        {
            Items.Add(new PassportFormField(form, requiredType, documentTypes, documentOnly));
        }

        public RelayCommand<PassportElement> NavigateCommand { get; }
        private void NavigateExecute(PassportElement element)
        {
            switch (element)
            {
                case PassportElementAddress address:
                    NavigationService.Navigate(typeof(PassportAddressPage), address);
                    break;
                case PassportElementPersonalDetails personalDetails:
                    break;
                case PassportElementEmailAddress emailAddress:
                    break;
                case PassportElementPhoneNumber phoneNumber:
                    break;
                case PassportElementBankStatement bankStatement:
                case PassportElementPassportRegistration passportRegistration:
                case PassportElementRentalAgreement rentalAgreement:
                case PassportElementTemporaryRegistration temporaryRegistration:
                case PassportElementUtilityBill utilityBill:
                    NavigationService.Navigate(typeof(PassportDocumentPage), state: new Dictionary<string, object>{ { "json", element } });
                    break;
                case PassportElementDriverLicense driverLicense:
                case PassportElementIdentityCard identityCard:
                case PassportElementInternalPassport internalPassport:
                case PassportElementPassport passport:
                    break;
            }
        }

        public RelayCommand AddCommand { get; }
        private async void AddExecute()
        {
            var types = new List<PassportElementType>();
            types.Add(new PassportElementTypePhoneNumber());
            types.Add(new PassportElementTypeEmailAddress());
            types.Add(new PassportElementTypePersonalDetails());
            types.Add(new PassportElementTypePassport());
            types.Add(new PassportElementTypeInternalPassport());
            types.Add(new PassportElementTypePassportRegistration());
            types.Add(new PassportElementTypeTemporaryRegistration());
            types.Add(new PassportElementTypeIdentityCard());
            types.Add(new PassportElementTypeDriverLicense());
            types.Add(new PassportElementTypeAddress());
            types.Add(new PassportElementTypeUtilityBill());
            types.Add(new PassportElementTypeBankStatement());
            types.Add(new PassportElementTypeRentalAgreement());

            foreach (var element in Items)
            {
                var type = element.ToElementType();
                var already = types.FirstOrDefault(x => x.TypeEquals(type));

                if (already !=null)
                {
                    types.Remove(already);
                }
            }

            var combo = new ComboBox();
            combo.ItemsSource = types;
            //combo.DisplayMemberPath = "Name";

            var dialog = new ContentDialog();
            dialog.Content = combo;

            await dialog.ShowQueuedAsync();
        }

        public RelayCommand HelpCommand { get; }
        private async void HelpExecute()
        {
            await TLMessageDialog.ShowAsync(Strings.Resources.PassportInfo, Strings.Resources.PassportInfoTitle, Strings.Resources.Close);
        }
    }

    public class PassportFormField : PassportElement
    {
        public PassportAuthorizationForm AuthorizationForm { get; set; }
        public PassportSuitableElement RequiredType { get; set; }
        public List<PassportSuitableElement> DocumentTypes { get; set; }
        public bool IsDocumentOnly { get; set; }

        public PassportFormField(PassportAuthorizationForm form, PassportSuitableElement requiredType, List<PassportSuitableElement> documentTypes, bool documentOnly)
        {
            AuthorizationForm = form;
            RequiredType = requiredType;
            DocumentTypes = documentTypes;
            IsDocumentOnly = documentOnly;
        }

        #region BaseObject

        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
