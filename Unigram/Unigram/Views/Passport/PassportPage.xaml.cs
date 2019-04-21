using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Entities;
using Unigram.ViewModels.Passport;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Passport
{
    public sealed partial class PassportPage : Page
    {
        public PassportViewModel ViewModel => DataContext as PassportViewModel;

        public PassportPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<PassportViewModel>();

            NavigationCacheMode = NavigationCacheMode.Required;
        }

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var element = args.Item as PassportElement;

            var title = content.Children[0] as TextBlock;
            var subtitle = content.Children[1] as TextBlock;

            switch (element)
            {
                case PassportElementAddress address:
                    title.Text = Strings.Resources.PassportAddress;
                    subtitle.Text = PrintAddress(address);
                    break;
                case PassportElementBankStatement bankStatement:
                    title.Text = Strings.Resources.ActionBotDocumentBankStatement;
                    subtitle.Text = PrintBankStatement(bankStatement);
                    break;
                case PassportElementDriverLicense driverLicense:
                    title.Text = Strings.Resources.ActionBotDocumentDriverLicence;
                    subtitle.Text = PrintDriverLicense(driverLicense);
                    break;
                case PassportElementEmailAddress emailAddress:
                    title.Text = Strings.Resources.PassportEmail;
                    subtitle.Text = PrintEmailAddress(emailAddress);
                    break;
                case PassportElementIdentityCard identityCard:
                    title.Text = Strings.Resources.ActionBotDocumentIdentityCard;
                    subtitle.Text = PrintIdentityCard(identityCard);
                    break;
                case PassportElementInternalPassport internalPassport:
                    title.Text = Strings.Resources.ActionBotDocumentInternalPassport;
                    subtitle.Text = PrintInternalPassport(internalPassport);
                    break;
                case PassportElementPassport passport:
                    title.Text = Strings.Resources.ActionBotDocumentPassport;
                    subtitle.Text = PrintPassport(passport);
                    break;
                case PassportElementPassportRegistration passportRegistration:
                    title.Text = Strings.Resources.ActionBotDocumentPassportRegistration;
                    subtitle.Text = PrintPassportRegistration(passportRegistration);
                    break;
                case PassportElementPersonalDetails personalDetails:
                    title.Text = Strings.Resources.PassportPersonalDetails;
                    subtitle.Text = PrintPersonalDetails(personalDetails);
                    break;
                case PassportElementPhoneNumber phoneNumber:
                    title.Text = Strings.Resources.PassportPhone;
                    subtitle.Text = PrintPhoneNumber(phoneNumber);
                    break;
                case PassportElementRentalAgreement rentalAgreement:
                    title.Text = Strings.Resources.ActionBotDocumentRentalAgreement;
                    subtitle.Text = PrintRentalAgreement(rentalAgreement);
                    break;
                case PassportElementTemporaryRegistration temporaryRegistration:
                    title.Text = Strings.Resources.ActionBotDocumentTemporaryRegistration;
                    subtitle.Text = PrintTemporaryRegistration(temporaryRegistration);
                    break;
                case PassportElementUtilityBill utilityBill:
                    title.Text = Strings.Resources.ActionBotDocumentUtilityBill;
                    subtitle.Text = PrintUtilityBill(utilityBill);
                    break;

                // Local types
                case PassportFormField formField:
                    SetupFormField(title, subtitle, formField);
                    break;
            }
        }

        private void SetupFormField(TextBlock title, TextBlock subtitle, PassportFormField field)
        {
            int availableDocumentTypesCount = field.DocumentTypes != null ? field.DocumentTypes.Count : 0;
            //TextDetailSecureCell view = new TextDetailSecureCell(context);
            //view.setBackgroundDrawable(Theme.getSelectorDrawable(true));
            if (field.RequiredType.Type is PassportElementTypePersonalDetails)
            {
                String label;
                if (field.DocumentTypes == null || field.DocumentTypes.IsEmpty())
                {
                    label = Strings.Resources.PassportPersonalDetails;
                }
                else if (field.IsDocumentOnly && field.DocumentTypes.Count == 1)
                {
                    label = GetTextForType(field.DocumentTypes[0].Type);
                }
                else if (field.IsDocumentOnly && field.DocumentTypes.Count == 2)
                {
                    label = string.Format(Strings.Resources.PassportTwoDocuments, GetTextForType(field.DocumentTypes[0].Type), GetTextForType(field.DocumentTypes[1].Type));
                }
                else
                {
                    label = Strings.Resources.PassportIdentityDocument;
                }

                title.Text = label;
            }
            else if (field.RequiredType.Type is PassportElementTypeAddress)
            {
                String label;
                if (field.DocumentTypes == null || field.DocumentTypes.IsEmpty())
                {
                    label = Strings.Resources.PassportAddress;
                }
                else if (field.IsDocumentOnly && field.DocumentTypes.Count == 1)
                {
                    label = GetTextForType(field.DocumentTypes[0].Type);
                }
                else if (field.IsDocumentOnly && field.DocumentTypes.Count == 2)
                {
                    label = string.Format(Strings.Resources.PassportTwoDocuments, GetTextForType(field.DocumentTypes[0].Type), GetTextForType(field.DocumentTypes[1].Type));
                }
                else
                {
                    label = Strings.Resources.PassportResidentialAddress;
                }

                title.Text = label;
            }
            else if (field.RequiredType.Type is PassportElementTypePhoneNumber)
            {
                title.Text = Strings.Resources.PassportPhone;
            }
            else if (field.RequiredType.Type is PassportElementTypeEmailAddress)
            {
                title.Text = Strings.Resources.PassportEmail;
            }

            PassportSuitableElement documentsType = null;
            if (field.DocumentTypes != null && field.DocumentTypes.Count > 0)
            {
                bool found = false;
                for (int a = 0, count = field.DocumentTypes.Count; a < count; a++)
                {
                    var documentType = field.DocumentTypes[a];
                    //typesValues.put(documentType, new HashMap<>());
                    //documentsToTypesLink.put(documentType, requiredType);
                    if (!found)
                    {
                        var documentValue = field.AvailableElements.GetElementForType(documentType.Type);
                        if (documentValue != null)
                        {
                            //if (documentValue.data != null)
                            //{
                            //    documentJson = decryptData(documentValue.data.data, decryptValueSecret(documentValue.data.secret, documentValue.data.data_hash), documentValue.data.data_hash);
                            //}
                            documentsType = documentType;
                            found = true;
                        }
                    }
                }
                if (documentsType == null)
                {
                    documentsType = field.DocumentTypes[0];
                }
            }

            subtitle.Text = getSubtitle(field, documentsType, string.Empty, availableDocumentTypesCount);
        }

        private string getTextForElement(PassportElement element)
        {
            switch (element)
            {
                case PassportElementAddress address:
                    return PrintAddress(address);
                case PassportElementBankStatement bankStatement:
                    return PrintBankStatement(bankStatement);
                case PassportElementDriverLicense driverLicense:
                    return PrintDriverLicense(driverLicense);
                case PassportElementEmailAddress emailAddress:
                    return PrintEmailAddress(emailAddress);
                case PassportElementIdentityCard identityCard:
                    return PrintIdentityCard(identityCard);
                case PassportElementInternalPassport internalPassport:
                    return PrintInternalPassport(internalPassport);
                case PassportElementPassport passport:
                    return PrintPassport(passport);
                case PassportElementPassportRegistration passportRegistration:
                    return PrintPassportRegistration(passportRegistration);
                case PassportElementPersonalDetails personalDetails:
                    return PrintPersonalDetails(personalDetails);
                case PassportElementPhoneNumber phoneNumber:
                    return PrintPhoneNumber(phoneNumber);
                case PassportElementRentalAgreement rentalAgreement:
                    return PrintRentalAgreement(rentalAgreement);
                case PassportElementTemporaryRegistration temporaryRegistration:
                    return PrintTemporaryRegistration(temporaryRegistration);
                case PassportElementUtilityBill utilityBill:
                    return PrintUtilityBill(utilityBill);
                default:
                    return null;
            }
        }

        private string GetTextForType(PassportElementType type)
        {
            switch (type)
            {
                case PassportElementTypePassport passport:
                    return Strings.Resources.ActionBotDocumentPassport;
                case PassportElementTypeDriverLicense driverLicense:
                    return Strings.Resources.ActionBotDocumentDriverLicence;
                case PassportElementTypeIdentityCard identityCard:
                    return Strings.Resources.ActionBotDocumentIdentityCard;
                case PassportElementTypeUtilityBill utilityBill:
                    return Strings.Resources.ActionBotDocumentUtilityBill;
                case PassportElementTypeBankStatement bankStatement:
                    return Strings.Resources.ActionBotDocumentBankStatement;
                case PassportElementTypeRentalAgreement rentalAgreement:
                    return Strings.Resources.ActionBotDocumentRentalAgreement;
                case PassportElementTypeInternalPassport internalPassport:
                    return Strings.Resources.ActionBotDocumentInternalPassport;
                case PassportElementTypePassportRegistration passportRegistration:
                    return Strings.Resources.ActionBotDocumentPassportRegistration;
                case PassportElementTypeTemporaryRegistration temporaryRegistration:
                    return Strings.Resources.ActionBotDocumentTemporaryRegistration;
                case PassportElementTypePhoneNumber phoneNumber:
                    return Strings.Resources.ActionBotDocumentPhone;
                case PassportElementTypeEmailAddress emailAddress:
                    return Strings.Resources.ActionBotDocumentEmail;
                default:
                    return null;
            }
        }

        private string getSubtitle(PassportFormField field, PassportSuitableElement documentRequiredType, string value, int availableDocumentTypesCount)
        {
            if (field.RequiredType.Type is PassportElementTypePhoneNumber)
            {
                var element = field.AvailableElements.GetElementForType(field.RequiredType.Type);
                value = getTextForElement(element);
            }
            else if (field.RequiredType.Type is PassportElementTypeEmailAddress)
            {
                var element = field.AvailableElements.GetElementForType(field.RequiredType.Type);
                value = getTextForElement(element);
            }
            else
            {
                var builder = new StringBuilder();

                if (documentRequiredType != null)
                {
                    var documentRequiredTypeValue = field.AvailableElements.GetElementForType(documentRequiredType?.Type);
                    if (availableDocumentTypesCount > 1)
                    {
                        builder.Append(GetTextForType(documentRequiredType.Type));
                    }
                    else if (documentRequiredTypeValue == null)
                    {
                        builder.Append(Strings.Resources.PassportDocuments);
                    }
                }

                if (field.RequiredType.Type is PassportElementTypePersonalDetails)
                {
                    if (!field.IsDocumentOnly)
                    {
                        var element = field.AvailableElements.GetElementForType(field.RequiredType.Type);
                        if (builder.Length > 0)
                        {
                            builder.Append(", ");
                        }

                        builder.Append(getTextForElement(element));
                    }
                    if (documentRequiredType != null)
                    {
                        var element = field.AvailableElements.GetElementForType(documentRequiredType.Type);
                        if (builder.Length > 0)
                        {
                            builder.Append(", ");
                        }

                        builder.Append(getTextForElement(element));
                    }
                }
                else if (field.RequiredType.Type is PassportElementTypeAddress)
                {
                    if (!field.IsDocumentOnly)
                    {
                        var element = field.AvailableElements.GetElementForType(field.RequiredType.Type);
                        if (builder.Length > 0)
                        {
                            builder.Append(", ");
                        }

                        builder.Append(getTextForElement(element));
                    }
                }

                value = builder.ToString();
            }











            bool isError = false;

            var errors = !field.IsDocumentOnly ? field.AvailableElements.GetErrorsForType(field.RequiredType.Type).ToList() : null;
            var documentsErrors = documentRequiredType != null ? field.AvailableElements.GetErrorsForType(documentRequiredType.Type).ToList() : null;

            if (errors != null && errors.Count > 0 || documentsErrors != null && documentsErrors.Count > 0)
            {
                value = null;
                if (!field.IsDocumentOnly)
                {
                    value = errors[0].Message;
                }
                if (value == null)
                {
                    value = documentsErrors[0].Message;
                }
                isError = true;
            }

            //Dictionary<String, String> errors = !field.GetHashCode ? errorsMap.get(getNameForType(requiredType.type)) : null;
            //Dictionary<String, String> documentsErrors = documentRequiredType != null ? errorsMap.get(getNameForType(documentRequiredType.type)) : null;
            //if (errors != null && errors.size() > 0 || documentsErrors != null && documentsErrors.size() > 0)
            //{
            //    value = null;
            //    if (!documentOnly)
            //    {
            //        value = mainErrorsMap.get(getNameForType(requiredType.type));
            //    }
            //    if (value == null)
            //    {
            //        value = mainErrorsMap.get(getNameForType(documentRequiredType.type));
            //    }
            //    isError = true;
            //}
            else
            {
                if (field.RequiredType.Type is PassportElementTypePersonalDetails)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        if (documentRequiredType == null)
                        {
                            value = Strings.Resources.PassportPersonalDetailsInfo;
                        }
                        else
                        {
                            //if (currentActivityType == TYPE_MANAGE)
                            //{
                            //    value = Strings.Resources.PassportDocuments;
                            //}
                            //else
                            {
                                if (availableDocumentTypesCount == 1)
                                {
                                    if (documentRequiredType.Type is PassportElementTypePassport)
                                    {
                                        value = Strings.Resources.PassportIdentityPassport;
                                    }
                                    else if (documentRequiredType.Type is PassportElementTypeInternalPassport)
                                    {
                                        value = Strings.Resources.PassportIdentityInternalPassport;
                                    }
                                    else if (documentRequiredType.Type is PassportElementTypeDriverLicense)
                                    {
                                        value = Strings.Resources.PassportIdentityDriverLicence;
                                    }
                                    else if (documentRequiredType.Type is PassportElementTypeIdentityCard)
                                    {
                                        value = Strings.Resources.PassportIdentityID;
                                    }
                                }
                                else
                                {
                                    value = Strings.Resources.PassportIdentityDocumentInfo;
                                }
                            }
                        }
                    }
                }
                else if (field.RequiredType.Type is PassportElementTypeAddress)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        if (documentRequiredType == null)
                        {
                            value = Strings.Resources.PassportAddressNoUploadInfo;
                        }
                        else
                        {
                            //if (currentActivityType == TYPE_MANAGE)
                            //{
                            //    value = Strings.Resources.PassportDocuments;
                            //}
                            //else
                            {
                                if (availableDocumentTypesCount == 1)
                                {
                                    if (documentRequiredType.Type is PassportElementTypeRentalAgreement)
                                    {
                                        value = Strings.Resources.PassportAddAgreementInfo;
                                    }
                                    else if (documentRequiredType.Type is PassportElementTypeUtilityBill)
                                    {
                                        value = Strings.Resources.PassportAddBillInfo;
                                    }
                                    else if (documentRequiredType.Type is PassportElementTypePassportRegistration)
                                    {
                                        value = Strings.Resources.PassportAddPassportRegistrationInfo;
                                    }
                                    else if (documentRequiredType.Type is PassportElementTypeTemporaryRegistration)
                                    {
                                        value = Strings.Resources.PassportAddTemporaryRegistrationInfo;
                                    }
                                    else if (documentRequiredType.Type is PassportElementTypeBankStatement)
                                    {
                                        value = Strings.Resources.PassportAddBankInfo;
                                    }
                                }
                                else
                                {
                                    value = Strings.Resources.PassportAddressInfo;
                                }
                            }
                        }
                    }
                }
                else if (field.RequiredType.Type is PassportElementTypePhoneNumber)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        value = Strings.Resources.PassportPhoneInfo;
                    }
                }
                else if (field.RequiredType.Type is PassportElementTypeEmailAddress)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        value = Strings.Resources.PassportEmailInfo;
                    }
                }
            }

            return value;
        }

        #region Infos

        private string PrintAddress(PassportElementAddress address)
        {
            var result = string.Empty;
            if (address == null)
            {
                return result;
            }

            if (!string.IsNullOrEmpty(address.Address.StreetLine1))
            {
                result += address.Address.StreetLine1 + ", ";
            }
            if (!string.IsNullOrEmpty(address.Address.StreetLine2))
            {
                result += address.Address.StreetLine2 + ", ";
            }
            if (!string.IsNullOrEmpty(address.Address.City))
            {
                result += address.Address.City + ", ";
            }
            if (!string.IsNullOrEmpty(address.Address.State))
            {
                result += address.Address.State + ", ";
            }
            if (!string.IsNullOrEmpty(address.Address.CountryCode))
            {
                result += address.Address.CountryCode + ", ";
            }
            if (!string.IsNullOrEmpty(address.Address.PostalCode))
            {
                result += address.Address.PostalCode + ", ";
            }

            return result.Trim(',', ' ');
        }

        private string PrintPersonalDetails(PassportElementPersonalDetails personalDetails)
        {
            var details = personalDetails.PersonalDetails;
            var result = $"{details.FirstName} ";

            if (!string.IsNullOrEmpty(details.MiddleName))
            {
                result += $"{details.MiddleName} ";
            }

            var birthdate = BindConvert.Current.ShortDate.Format(new DateTime(details.Birthdate.Year, details.Birthdate.Month, details.Birthdate.Day));
            var gender = string.Equals(details.Gender, "male", StringComparison.OrdinalIgnoreCase) ? Strings.Resources.PassportMale : Strings.Resources.PassportFemale;
            var country = Country.Countries.FirstOrDefault(x => string.Equals(details.CountryCode, x.Code, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? details.CountryCode;
            var residence = Country.Countries.FirstOrDefault(x => string.Equals(details.ResidenceCountryCode, x.Code, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? details.ResidenceCountryCode;

            return result + $"{details.LastName}, {birthdate}, {gender}, {country}, {residence}";
        }

        private string PrintEmailAddress(PassportElementEmailAddress emailAddress)
        {
            return emailAddress.EmailAddress;
        }

        private string PrintPhoneNumber(PassportElementPhoneNumber phoneNumber)
        {
            return PhoneNumber.Format(phoneNumber.PhoneNumber);
        }

        #endregion

        #region PersonalDocument

        private string PrintBankStatement(PassportElementBankStatement bankStatement)
        {
            return PrintPersonalDocument(bankStatement.BankStatement);
        }

        private string PrintPassportRegistration(PassportElementPassportRegistration passportRegistration)
        {
            return PrintPersonalDocument(passportRegistration.PassportRegistration);
        }

        private string PrintRentalAgreement(PassportElementRentalAgreement rentalAgreement)
        {
            return PrintPersonalDocument(rentalAgreement.RentalAgreement);
        }

        private string PrintTemporaryRegistration(PassportElementTemporaryRegistration temporaryRegistration)
        {
            return PrintPersonalDocument(temporaryRegistration.TemporaryRegistration);
        }

        private string PrintUtilityBill(PassportElementUtilityBill utilityBill)
        {
            return PrintPersonalDocument(utilityBill.UtilityBill);
        }

        private string PrintPersonalDocument(PersonalDocument document)
        {
            return Strings.Resources.PassportDocuments;
        }

        #endregion

        #region IdentityDocument

        private string PrintDriverLicense(PassportElementDriverLicense driverLicense)
        {
            return PrintIdentityDocument(driverLicense.DriverLicense);
        }

        private string PrintIdentityCard(PassportElementIdentityCard identityCard)
        {
            return PrintIdentityDocument(identityCard.IdentityCard);
        }

        private string PrintInternalPassport(PassportElementInternalPassport internalPassport)
        {
            return PrintIdentityDocument(internalPassport.InternalPassport);
        }

        private string PrintPassport(PassportElementPassport passport)
        {
            return PrintIdentityDocument(passport.Passport);
        }

        private string PrintIdentityDocument(IdentityDocument document)
        {
            var result = document.Number;

            if (document.ExpiryDate != null)
            {
                var expiryDate = BindConvert.Current.ShortDate.Format(new DateTime(document.ExpiryDate.Year, document.ExpiryDate.Month, document.ExpiryDate.Day));
                result += $", {expiryDate}";
            }

            return result;
        }

        #endregion

        #endregion

        #region Binding

        private string ConvertRequest(PassportAuthorizationForm form, int botId)
        {
            if (form == null)
            {
                return Strings.Resources.PassportSelfRequest;
            }

            var user = ViewModel.CacheService.GetUser(botId);
            if (user == null)
            {
                return null;
            }

            return String.Format(Strings.Resources.PassportRequest, user.GetFullName());
        }

        private FormattedText ConvertPolicy(PassportAuthorizationForm form, int botId)
        {
            if (form == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(form.PrivacyPolicyUrl))
            {
                return new FormattedText(Strings.Resources.PassportNoPolicy, new TextEntity[0]);
            }

            var user = ViewModel.CacheService.GetUser(botId);
            if (user == null)
            {
                return null;
            }

            var text = string.Format(Strings.Resources.PassportPolicy, form.PrivacyPolicyUrl, user.Username);
            var first = text.IndexOf('*');
            var last = text.LastIndexOf('*');

            if (first != -1 && last != -1)
            {
                var builder = new StringBuilder(text);
                builder.Remove(last, 1);
                builder.Remove(first, 1);

                text = builder.ToString();

                return new FormattedText(text, new[] { new TextEntity { Offset = first, Length = last - first - 1, Type = new TextEntityTypeTextUrl(form.PrivacyPolicyUrl) } });
            }

            return new FormattedText(text, new TextEntity[0]);
        }

        #endregion

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.NavigateCommand.Execute(e.ClickedItem);
        }
    }
}
