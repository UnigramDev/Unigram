using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Services.NavigationService;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Passport
{
    public class PassportDocumentViewModelBase : TLViewModelBase, IDelegable<IFileDelegate>, IHandle<UpdateFile>
    {
        private readonly Dictionary<int, DatedFile> _uploadingFiles = new Dictionary<int, DatedFile>();
        private string _password;

        public IFileDelegate Delegate { get; set; }

        public PassportDocumentViewModelBase(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Files = new MvxObservableCollection<DatedFile>();
            Translation = new MvxObservableCollection<DatedFile>();

            AddFileCommand = new RelayCommand(AddFileExecute);
            AddTranslationCommand = new RelayCommand(AddTranslationExecute);

            SaveCommand = new RelayCommand(SaveExecute);
        }

        private PassportElement _documentType;
        public PassportElement DocumentType
        {
            get { return _documentType; }
            set { Set(ref _documentType, value); }
        }

        private bool _isDirty;
        public bool IsDirty
        {
            get { return _isDirty; }
            set { Set(ref _isDirty, value); }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Aggregator.Subscribe(this);

            if (state.TryGet("json", out PassportElement element))
            {
                state.Remove("json");

                var personal = element.GetPersonalDocument();
                if (personal == null)
                {
                    return;
                }

                DocumentType = element;
                Files.ReplaceWith(personal.Files);
                Translation.ReplaceWith(personal.Translation);
            }

            if (state.TryGet("password", out string password))
            {
                state.Remove("password");

                _password = password;
            }
        }

        public override async Task OnNavigatingFromAsync(NavigatingEventArgs args)
        {
            var deferral = args.GetDeferral();

            if (_isDirty && args.NavigationMode == NavigationMode.Back)
            {
                args.Cancel = true;
                deferral.Complete();

                var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.DiscardChanges, Strings.Resources.PassportDiscardChanges, Strings.Resources.PassportDiscard, Strings.Resources.Cancel);
                if (confirm == Windows.UI.Xaml.Controls.ContentDialogResult.Primary)
                {
                    _isDirty = false;
                    NavigationService.GoBack();
                }
            }

        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return base.OnNavigatedFromAsync(pageState, suspending);
        }

        public MvxObservableCollection<DatedFile> Files { get; private set; }
        public MvxObservableCollection<DatedFile> Translation { get; private set; }

        public RelayCommand AddFileCommand { get; }
        private void AddFileExecute()
        {
            AddFile(false);
        }

        public RelayCommand AddTranslationCommand { get; }
        private void AddTranslationExecute()
        {
            AddFile(true);
        }

        private async void AddFile(bool translation)
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.AddRange(Constants.PhotoTypes);

            var files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                IsDirty = true;

                foreach (var file in files)
                {
                    var asFile = true;
                    var generated = await file.ToGeneratedAsync(asFile ? ConversionType.Copy : ConversionType.Compress);

                    ProtoService.Send(new UploadFile(generated, new FileTypeSecure(), 16), result =>
                    {
                        if (result is File upload)
                        {
                            var item = new DatedFile(upload, DateTime.Now.ToTimestamp());

                            _uploadingFiles[upload.Id] = item;

                            BeginOnUIThread(() =>
                            {
                                if (translation)
                                {
                                    Translation.Add(item);
                                }
                                else
                                {
                                    Files.Add(item);
                                }
                            });
                        }
                    });
                }
            }
        }

        public void Handle(UpdateFile update)
        {
            BeginOnUIThread(() =>
            {
                Delegate?.UpdateFile(update.File);
            });
        }

        public RelayCommand SaveCommand { get; }
        private async void SaveExecute()
        {
            var element = _documentType;
            if (element == null)
            {
                return;
            }

            if (Files.IsEmpty())
            {
                RaisePropertyChanged("FILES_INVALID");
                return;
            }

            //if (Translation.IsEmpty())
            //{
            //    RaisePropertyChanged("TRANSLATION_INVALID");
            //    return;
            //}

            var personal = new InputPersonalDocument();
            personal.Files = Files.Select(x => new InputFileId(x.File.Id)).ToArray();
            personal.Translation = Translation.Select(x => new InputFileId(x.File.Id)).ToArray();

            var input = element.ToInputElement();
            input.SetPersonalDocument(personal);

            var response = await ProtoService.SendAsync(new SetPassportElement(input, _password));
            if (response is PassportElement)
            {
                NavigationService.GoBack();
            }
        }
    }
}
