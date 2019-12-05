﻿using System;
using GalaSoft.MvvmLight;
using GroupMeClientApi.Models;

namespace GroupMeClientAvalonia.ViewModels.Controls
{
    /// <summary>
    /// Provides a base class for controls that can be displayed inline with messages.
    /// </summary>
    public abstract class MessageControlViewModelBase : ViewModelBase, IDisposable
    {
        /// <summary>
        /// Gets the unique identifier for the message.
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Gets or sets the message.
        /// </summary>
        public abstract Message Message { get; set; }

        /// <summary>
        /// Gets a value indicating whether this control is selectable.
        /// </summary>
        public abstract bool IsSelectable { get; }

        /// <inheritdoc/>
        void IDisposable.Dispose()
        {
        }
    }
}
