using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Diagnostics;
using HtmlAgilityPack;
using System.Net;

namespace journals
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Title = "Agregador de Feed RSS para Médicos";
            this.Width = 1000;
            this.Height = 600;

            var mainLayout = new StackPanel();

            // Input para URL do feed
            var urlInput = new TextBox { Margin = new Thickness(10) };
            urlInput.GotFocus += (sender, args) => { if (urlInput.Text == "Digite o URL do feed RSS...") urlInput.Text = ""; };
            urlInput.LostFocus += (sender, args) => { if (string.IsNullOrWhiteSpace(urlInput.Text)) urlInput.Text = "Digite o URL do feed RSS..."; };
            urlInput.Text = "Digite o URL do feed RSS...";
            mainLayout.Children.Add(urlInput);

            // Botões para adicionar feed e carregar de arquivo
            var buttonLayout = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
            var addButton = new Button { Content = "Adicionar Feed", Margin = new Thickness(5) };
            addButton.Click += async (sender, e) => await AddFeed(urlInput.Text);
            buttonLayout.Children.Add(addButton);

            var fileButton = new Button { Content = "Carregar Feed XML do Computador", Margin = new Thickness(5) };
            fileButton.Click += LoadFeedFromFile;
            buttonLayout.Children.Add(fileButton);

            mainLayout.Children.Add(buttonLayout);

            // Splitter para lista de feeds e visualizador de conteúdo
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });

            feedList = new ListBox { Margin = new Thickness(10) };
            feedList.SelectionChanged += async (sender, e) => await DisplayContent(feedList.SelectedItem as FeedItem);
            grid.Children.Add(feedList);
            Grid.SetColumn(feedList, 0);

            webViewer = new WebBrowser();
            grid.Children.Add(webViewer);
            Grid.SetColumn(webViewer, 1);

            mainLayout.Children.Add(grid);

            // Botão para abrir no navegador
            openInBrowserButton = new Button { Content = "Abrir no Navegador", Margin = new Thickness(10), Visibility = Visibility.Hidden };
            openInBrowserButton.Click += OpenInBrowser;
            mainLayout.Children.Add(openInBrowserButton);

            this.Content = mainLayout;
        }

        private async Task AddFeed(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        string response = await client.GetStringAsync(url);
                        ParseFeed(response);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao carregar o feed: {ex.Message}");
                }
            }
        }

        private void LoadFeedFromFile(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Arquivos XML (*.xml)|*.xml"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string content = System.IO.File.ReadAllText(openFileDialog.FileName);
                    ParseFeed(content);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao carregar o arquivo: {ex.Message}");
                }
            }
        }

        private void ParseFeed(string content)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(content);
                XmlNodeList items = doc.GetElementsByTagName("item");
                foreach (XmlNode item in items)
                {
                    string title = item["title"].InnerText;
                    string link = item["link"].InnerText;
                    string description = item["description"].InnerText;
                    feedList.Items.Add(new FeedItem { Title = title, Link = link, Description = description });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar o feed: {ex.Message}");
            }
        }

        private async Task DisplayContent(FeedItem item)
        {
            if (item != null)
            {
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        var response = await client.GetStringAsync(item.Link);
                        var htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(response);

                        // Tentar encontrar o abstract na meta tag
                        var metaNode = htmlDoc.DocumentNode.SelectSingleNode("//meta[@name='citation_abstract']");
                        if (metaNode != null && metaNode.Attributes["content"] != null)
                        {
                            webViewer.NavigateToString(WebUtility.HtmlDecode(metaNode.Attributes["content"].Value));
                        }
                        else
                        {
                            // Tentar encontrar um <div> que contenha a palavra "Abstract" seguido por parágrafos
                            var abstractDiv = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(text(), 'Abstract')]");
                            if (abstractDiv != null)
                            {
                                var paragraphs = abstractDiv.SelectNodes("following-sibling::*[self::p or self::div]");
                                if (paragraphs != null)
                                {
                                    string abstractText = "<div>";
                                    foreach (var paragraph in paragraphs)
                                    {
                                        if (paragraph.Name == "div" && paragraph.InnerText.Contains("References")) break;
                                        abstractText += $"<p>{WebUtility.HtmlDecode(paragraph.InnerText)}</p>";
                                    }
                                    abstractText += "</div>";
                                    webViewer.NavigateToString(abstractText.Trim());
                                }
                                else
                                {
                                    webViewer.NavigateToString("<p>Abstract não encontrado.</p>");
                                }
                            }
                            else
                            {
                                webViewer.NavigateToString("<p>Abstract não encontrado.</p>");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    webViewer.NavigateToString($"<p>Erro ao carregar o conteúdo: {ex.Message}</p>");
                }

                openInBrowserButton.Visibility = Visibility.Visible;
                currentLink = item.Link;
            }
        }

        private void OpenInBrowser(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentLink))
            {
                Process.Start(new ProcessStartInfo { FileName = currentLink, UseShellExecute = true });
            }
        }

        private ListBox feedList;
        private WebBrowser webViewer;
        private Button openInBrowserButton;
        private string currentLink;
    }

    public class FeedItem
    {
        public string Title { get; set; }
        public string Link { get; set; }
        public string Description { get; set; }

        public override string ToString()
        {
            return Title;
        }
    }

    public partial class App : Application
    {
    }
}

// Arquivo XAML simplificado
/*
<Window x:Class="Journals.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Agregador de Feed RSS para Médicos" Height="600" Width="1000">
</Window>
*/