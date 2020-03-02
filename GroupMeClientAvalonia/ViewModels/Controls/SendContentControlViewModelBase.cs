﻿using System;
using System.IO;
using System.Windows.Input;
using GroupMeClientApi.Models;

namespace GroupMeClientAvalonia.ViewModels.Controls
{
    /// <summary>
    /// <see cref="SendContentControlViewModelBase"/> provides a base ViewModel for dialogs that handle sending specialized content.
    /// </summary>
    public abstract class SendContentControlViewModelBase : GalaSoft.MvvmLight.ViewModelBase, IDisposable
    {
        private string typedMessageContents;
        private bool isSending;

        /// <summary>
        /// Gets or sets the command to be performed when the message is ready to send.
        /// </summary>
        public ICommand SendMessage { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Group"/> or <see cref="Chat"/> to which this content is being sent.
        /// </summary>
        public IMessageContainer MessageContainer { get; set; }

        /// <summary>
        /// Gets or sets the data stream for the content that is being sent.
        /// </summary>
        public Stream ContentStream { get; set; }

        /// <summary>
        /// Gets or sets the message the user has composed to send.
        /// </summary>
        public string TypedMessageContents
        {
            get { return this.typedMessageContents; }
            set { this.Set(() => this.TypedMessageContents, ref this.typedMessageContents, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the sending animation should be displayed.
        /// </summary>
        public bool IsSending
        {
            get { return this.isSending; }
            set { this.Set(() => this.IsSending, ref this.isSending, value); }
        }

        /// <inheritdoc/>
        public virtual void Dispose()
        {
            this.ContentStream?.Close();
            this.ContentStream?.Dispose();
        }
    }
}
