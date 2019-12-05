﻿using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using GalaSoft.MvvmLight.Command;
using GroupMeClientApi;

namespace GroupMeClientAvalonia.ViewModels.Controls.Attachments
{
    /// <summary>
    /// <see cref="GenericLinkAttachmentControlViewModel"/> provides a ViewModel for controls to display a generic webpage attachment.
    /// </summary>
    public class GenericLinkAttachmentControlViewModel : LinkAttachmentBaseViewModel
    {
        private IBitmap faviconImage;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericLinkAttachmentControlViewModel"/> class.
        /// </summary>
        /// <param name="url">The url of the attached website.</param>
        /// <param name="imageDownloader">The downloader to use when retreiving data.</param>
        public GenericLinkAttachmentControlViewModel(string url, ImageDownloader imageDownloader)
            : base(url, imageDownloader)
        {
            this.Clicked = new RelayCommand(this.ClickedAction);
        }

        /// <summary>
        /// Gets the website title.
        /// </summary>
        public string Title => this.LinkInfo?.Title;

        /// <summary>
        /// Gets the website short URL name.
        /// </summary>
        public string Site => this.Uri?.Host;

        /// <summary>
        /// Gets the action to occur when the website is clicked.
        /// </summary>
        public ICommand Clicked { get; }

        /// <summary>
        /// Gets the favicon image.
        /// </summary>
        public IBitmap FaviconImage
        {
            get { return this.faviconImage; }
            private set { this.Set(() => this.FaviconImage, ref this.faviconImage, value); }
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            // Not needed - no unmanaged resources
        }

        /// <inheritdoc/>
        protected override void MetadataDownloadCompleted()
        {
            _ = this.DownloadImageAsync(this.LinkInfo.AnyPreviewPictureUrl, 350, 300);
            _ = this.DownloadFaviconImage(this.LinkInfo.Favicon);
            this.RaisePropertyChanged(string.Empty);
        }

        private async Task DownloadFaviconImage(string url)
        {
            try
            {
                if (!string.IsNullOrEmpty(url))
                {
                    var result = await this.ImageDownloader.DownloadByteDataAsync(url);

                    this.FaviconImage = Utilities.ImageUtils.BytesToImageSource(result);
                }
            }
            catch (Exception)
            {
            }
        }

        private void ClickedAction()
        {
            Extensions.WebBrowserHelper.OpenUrl(this.Url);
        }
    }
}
