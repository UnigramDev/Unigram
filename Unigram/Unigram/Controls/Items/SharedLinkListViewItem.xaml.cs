using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.ViewModels;
using Unigram.Views;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Items
{
    public sealed partial class SharedLinkListViewItem : UserControl
    {
        public TLMessage ViewModel => DataContext as TLMessage;

        private UnigramViewModelBase _context;
        public UnigramViewModelBase Context
        {
            get
            {
                if (_context == null)
                {
                    var parent = VisualTreeHelper.GetParent(this);
                    while (parent as ListView == null && parent != null)
                    {
                        parent = VisualTreeHelper.GetParent(parent);
                    }

                    var item = parent as ListView;
                    if (item != null)
                    {
                        _context = item.DataContext as UnigramViewModelBase;
                    }
                }

                return _context;
            }
        }

        private TLMessage _oldViewModel;

        public SharedLinkListViewItem()
        {
            InitializeComponent();

            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (_oldViewModel != null)
            {
                //_oldViewModel.PropertyChanged -= OnPropertyChanged;
                _oldViewModel = null;
            }

            if (DataContext is TLMessage message)
            {
                _oldViewModel = ViewModel;
                //ViewModel.PropertyChanged += OnPropertyChanged;

                var links = new List<string>();
                var hasThumb = false;

                string title = null;
                string description = null;
                string description2 = null;
                string webPageLink = null;
                bool webPageCached = false;

                if (message.Media is TLMessageMediaWebPage webPageMedia && webPageMedia.WebPage is TLWebPage webPage)
                {
                    title = webPage.Title;
                    if (string.IsNullOrEmpty(title))
                    {
                        title = webPage.SiteName;
                    }

                    description = string.IsNullOrEmpty(webPage.Description) ? null : webPage.Description;
                    webPageLink = webPage.Url;
                    webPageCached = webPage.HasCachedPage;

                    hasThumb = webPage.HasPhoto && webPage.Photo is TLPhoto photo && photo.Thumb != null;
                }

                if (message != null && message.Entities != null)
                {
                    for (int a = 0; a < message.Entities.Count; a++)
                    {
                        var entity = message.Entities[a];
                        if (entity.Length <= 0 || entity.Offset < 0 || entity.Offset >= message.Message.Length)
                        {
                            continue;
                        }
                        else if (entity.Offset + entity.Length > message.Message.Length)
                        {
                            entity.Length = message.Message.Length - entity.Offset;
                        }

                        if (a == 0 && webPageLink != null && !(entity.Offset == 0 && entity.Length == message.Message.Length))
                        {
                            if (message.Entities.Count == 1)
                            {
                                if (description == null)
                                {
                                    description2 = message.Message;
                                }
                            }
                            else
                            {
                                description2 = message.Message;
                            }
                        }
                        try
                        {
                            String link = null;
                            if (entity is TLMessageEntityTextUrl || entity is TLMessageEntityUrl)
                            {
                                if (entity is TLMessageEntityUrl)
                                {
                                    link = message.Message.Substring(entity.Offset, entity.Length);
                                }
                                else if (entity is TLMessageEntityTextUrl textUrl)
                                {
                                    link = textUrl.Url;
                                }
                                if (title == null || title.Length == 0)
                                {
                                    title = link;
                                    var url = link;
                                    if (url.StartsWith("http") == false)
                                    {
                                        url = "http://" + url;
                                    }

                                    Uri uri = new Uri(url);
                                    title = uri.Host;
                                    if (title == null)
                                    {
                                        title = link;
                                    }
                                    int index;
                                    if (title != null && (index = title.LastIndexOf('.')) >= 0)
                                    {
                                        title = title.Substr(0, index);
                                        if ((index = title.LastIndexOf('.')) >= 0)
                                        {
                                            title = title.Substring(index + 1);
                                        }
                                        title = title.Substring(0, 1).ToUpper() + title.Substring(1);
                                    }
                                    if (entity.Offset != 0 || entity.Length != message.Message.Length)
                                    {
                                        description = message.Message;
                                    }
                                }
                            }
                            else if (entity is TLMessageEntityEmail)
                            {
                                if (title == null || title.Length == 0)
                                {
                                    link = "mailto:" + message.Message.Substring(entity.Offset, entity.Length);
                                    title = message.Message.Substring(entity.Offset, entity.Length);
                                    if (entity.Offset != 0 || entity.Length != message.Message.Length)
                                    {
                                        description = message.Message;
                                    }
                                }
                            }
                            if (link != null)
                            {
                                if (link.ToLower().IndexOf("http") != 0 && link.ToLower().IndexOf("mailto") != 0)
                                {
                                    links.Add("http://" + link);
                                }
                                else
                                {
                                    links.Add(link);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            //FileLog.e(e);
                        }
                    }
                }

                if (webPageLink != null && links.Count == 0)
                {
                    links.Add(webPageLink);
                }

                //if (hasThumb)
                //{
                //    Thumbnail.Visibility = Visibility.Visible;
                //}
                //else
                //{
                //    Thumbnail.Visibility = Visibility.Collapsed;
                //}

                if (title != null)
                {
                    TitleLabel.Text = title.Replace('\n', ' ');
                    TitleLabel.Visibility = Visibility.Visible;
                }
                else
                {
                    TitleLabel.Visibility = Visibility.Collapsed;
                }

                if (description != null)
                {
                    DescriptionLabel.Text = description;
                    DescriptionLabel.Visibility = Visibility.Visible;
                }
                else
                {
                    DescriptionLabel.Visibility = Visibility.Collapsed;
                }

                if (description2 != null)
                {
                    Description2Label.Text = description2;
                    Description2Label.Visibility = Visibility.Visible;

                    if (description != null)
                    {
                        Description2Label.Margin = new Thickness(0, 8, 0, 0);
                    }
                    else
                    {
                        Description2Label.Margin = new Thickness(0);
                    }
                }
                else
                {
                    Description2Label.Visibility = Visibility.Collapsed;
                }

                LinksPanel.Children.Clear();
                LinksPanel.RowDefinitions.Clear();

                for (int i = 0; i < links.Count; i++)
                {
                    var link = links[i];
                    if (Uri.TryCreate(link, UriKind.Absolute, out Uri uri))
                    {
                        var paragraph = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
                        var hyperlink = new Hyperlink { NavigateUri = uri, UnderlineStyle = UnderlineStyle.None };

                        if (link == webPageLink && webPageCached)
                        {
                            hyperlink.Inlines.Add(new Run { Text = "\uE611 ", FontSize = 12, FontFamily = App.Current.Resources["TelegramThemeFontFamily"] as FontFamily });
                        }

                        hyperlink.Inlines.Add(new Run { Text = link });
                        paragraph.Inlines.Add(hyperlink);
                        paragraph.Inlines.Add(new Run { Text = " " });

                        LinksPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                        LinksPanel.Children.Add(paragraph);

                        Grid.SetRow(paragraph, i);
                    }
                }
            }
        }

        private async void Thumbnail_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is TLMessage message && message.Media is TLMessageMediaWebPage webpageMedia && webpageMedia.WebPage is TLWebPage webpage)
            {
                if (webpage.HasCachedPage)
                {
                    Context.NavigationService.Navigate(typeof(InstantPage), message.Media);
                }
                else
                {
                    var url = webpage.Url;
                    if (url.StartsWith("http") == false)
                    {
                        url = "http://" + url;
                    }

                    if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                    {
                        if (MessageHelper.IsTelegramUrl(uri))
                        {
                            MessageHelper.HandleTelegramUrl(webpage.Url);
                        }
                        else
                        {
                            await Launcher.LaunchUriAsync(uri);
                        }
                    }
                }
            }
        }
    }
}
