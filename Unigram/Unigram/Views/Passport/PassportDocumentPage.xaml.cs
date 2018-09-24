using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Passport;
using Unigram.Converters;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Passport;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Passport
{
    public sealed partial class PassportDocumentPage : Page, IFileDelegate
    {
        public PassportDocumentViewModelBase ViewModel => DataContext as PassportDocumentViewModelBase;

        public PassportDocumentPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<PassportDocumentViewModelBase, IFileDelegate>(this);

            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "FILES_INVALID":
                    VisualUtilities.ShakeView(FilesButton);
                    break;
                case "TRANSLATION_INVALID":
                    VisualUtilities.ShakeView(TranslationButton);
                    break;
            }
        }

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as PassportDocumentCell;
            var datedFile = args.Item as DatedFile;

            content.UpdateFile(ViewModel.ProtoService, datedFile);

            args.Handled = true;
        }

        #endregion

        #region Binding

        private string ConvertHeader(PassportElement element)
        {
            switch (element)
            {
                case PassportElementPassport passport:
                    return Strings.Resources.ActionBotDocumentPassport;
                case PassportElementDriverLicense driverLicense:
                    return Strings.Resources.ActionBotDocumentDriverLicence;
                case PassportElementIdentityCard identityCard:
                    return Strings.Resources.ActionBotDocumentIdentityCard;
                case PassportElementUtilityBill utilityBill:
                    return Strings.Resources.ActionBotDocumentUtilityBill;
                case PassportElementBankStatement bankStatement:
                    return Strings.Resources.ActionBotDocumentBankStatement;
                case PassportElementRentalAgreement rentalAgreement:
                    return Strings.Resources.ActionBotDocumentRentalAgreement;
                case PassportElementInternalPassport internalPassport:
                    return Strings.Resources.ActionBotDocumentInternalPassport;
                case PassportElementPassportRegistration passportRegistration:
                    return Strings.Resources.ActionBotDocumentPassportRegistration;
                case PassportElementTemporaryRegistration temporaryRegistration:
                    return Strings.Resources.ActionBotDocumentTemporaryRegistration;
                case PassportElementPhoneNumber phoneNumber:
                    return Strings.Resources.ActionBotDocumentPhone;
                case PassportElementEmailAddress emailAddress:
                    return Strings.Resources.ActionBotDocumentEmail;
                default:
                    return null;
            }
        }

        private string ConvertAdd(int count)
        {
            return count > 0 ? Strings.Resources.PassportUploadAdditinalDocument : Strings.Resources.PassportUploadDocument;
        }

        private string ConvertInfo(PassportElement element, bool translation)
        {
            switch (element)
            {
                case PassportElementUtilityBill utilityBill:
                    return translation ? Strings.Resources.PassportAddTranslationBillInfo : Strings.Resources.PassportAddBillInfo;
                case PassportElementBankStatement bankStatement:
                    return translation ? Strings.Resources.PassportAddTranslationBankInfo : Strings.Resources.PassportAddTranslationBankInfo;
                case PassportElementPassportRegistration passportRegistration:
                    return translation ? Strings.Resources.PassportAddTranslationPassportRegistrationInfo : Strings.Resources.PassportAddPassportRegistrationInfo;
                case PassportElementTemporaryRegistration temporaryRegistration:
                    return translation ? Strings.Resources.PassportAddTranslationTemporaryRegistrationInfo : Strings.Resources.PassportAddTemporaryRegistrationInfo;
                case PassportElementRentalAgreement rentalAgreement:
                    return translation ? Strings.Resources.PassportAddTranslationAgreementInfo : Strings.Resources.PassportAddAgreementInfo;
                default:
                    return null;
            }
        }

        #endregion

        #region Delegate

        public void UpdateFile(File file)
        {
            foreach (var item in ViewModel.Files)
            {
                if (item.File.Id == file.Id)
                {
                    item.File = file;

                    var container = Files.ContainerFromItem(item) as SelectorItem;
                    if (container == null)
                    {
                        return;
                    }

                    var content = container.ContentTemplateRoot as PassportDocumentCell;
                    if (content == null)
                    {
                        return;
                    }

                    content.UpdateFile(ViewModel.ProtoService, file);
                }
            }

            foreach (var item in ViewModel.Translation)
            {
                if (item.File.Id == file.Id)
                {
                    item.File = file;

                    var container = Translation.ContainerFromItem(item) as SelectorItem;
                    if (container == null)
                    {
                        return;
                    }

                    var content = container.ContentTemplateRoot as PassportDocumentCell;
                    if (content == null)
                    {
                        return;
                    }

                    content.UpdateFile(ViewModel.ProtoService, file);
                }
            }
        }

        #endregion
    }
}
