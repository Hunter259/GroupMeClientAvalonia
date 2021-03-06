﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using GroupMeClientApi.Models;
using GroupMeClientAvalonia.Extensions;
using GroupMeClientAvalonia.Utilities;

namespace GroupMeClientAvalonia.ViewModels.Controls
{
    /// <summary>
    /// <see cref="GroupContentsControlViewModel"/> provides a ViewModel for the <see cref="Views.Controls.GroupContentsControl"/> control that displays the contents (messages) of a Group or Chat.
    /// Controls for sending messages are also included.
    /// </summary>
    public class GroupContentsControlViewModel : ViewModelBase, FileDragDropHelper.IDragDropTarget, IDisposable
    {
        private IMessageContainer messageContainer;
        private AvatarControlViewModel topBarAvatar;
        private string typedMessageContents = string.Empty;
        private bool isSelectionAllowed = false;
        private bool isNotAtBottom = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupContentsControlViewModel"/> class.
        /// </summary>
        public GroupContentsControlViewModel()
        {
            this.Messages = new SourceList<MessageControlViewModelBase>();

            this.ReloadSem = new SemaphoreSlim(1, 1);

            this.SendMessage = new RelayCommand(async () => await this.SendMessageAsync(), () => !this.IsSending, true);
            this.SendAttachment = new RelayCommand(async () => await this.SendFileImageAttachment(), true);
            this.OpenMessageSuggestions = new RelayCommand(this.OpenMessageSuggestionsDialog);
            this.ReloadView = new RelayCommand<ScrollViewer>(async (s) => await this.LoadMoreAsync(s), true);
            this.GroupChatPluginActivated = new RelayCommand<GroupMeClientPlugin.GroupChat.IGroupChatPlugin>(this.ActivateGroupPlugin);
            this.GroupChatCachePluginActivated = new RelayCommand<GroupMeClientPlugin.GroupChat.IGroupChatCachePlugin>(this.ActivateGroupCachePlugin);
            this.SelectionChangedCommand = new RelayCommand<object>(this.SelectionChangedHandler);
            this.ScrollToEnd = new RelayCommand<ListBox>(this.ScrollToEndHandler);

            this.PopupManager = new Controls.PopupViewModel()
            {
                ClosePopup = new RelayCommand(this.ClosePopupHandler),
                EasyClosePopup = null, // EasyClose makes it too easy to accidently close the send dialog.
            };

            this.ReliabilityStateMachine = new ReliabilityStateMachine();

            this.GroupChatPlugins = new ObservableCollection<GroupMeClientPlugin.GroupChat.IGroupChatPlugin>();
            foreach (var plugin in Plugins.PluginManager.Instance.GroupChatPlugins)
            {
                this.GroupChatPlugins.Add(plugin);
            }

            this.GroupChatCachePlugins = new ObservableCollection<GroupMeClientPlugin.GroupChat.IGroupChatCachePlugin>();
            foreach (var plugin in Plugins.PluginManager.Instance.GroupChatCachePlugins)
            {
                this.GroupChatCachePlugins.Add(plugin);
            }

            this.GroupChatPlugins.Add(new MultiLikeControlViewModel.MultiLikePseudoPlugin(this));

            this.SortedMessages = new ObservableCollectionExtended<MessageControlViewModelBase>();
            this.Messages.AsObservableList()
                .Connect()
                .Sort(SortExpressionComparer<MessageControlViewModelBase>.Ascending(m => m.Id))
                .Bind(this.SortedMessages)
                .Subscribe();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupContentsControlViewModel"/> class.
        /// </summary>
        /// <param name="messageContainer">The Group or Chat to bind to.</param>
        /// <param name="settings">The settings instance to use.</param>
        public GroupContentsControlViewModel(IMessageContainer messageContainer, Settings.SettingsManager settings)
            : this()
        {
            this.MessageContainer = messageContainer;
            this.Settings = settings;
            this.TopBarAvatar = new AvatarControlViewModel(this.MessageContainer, this.MessageContainer.Client.ImageDownloader);

            _ = this.LoadMoreAsync();
        }

        /// <summary>
        /// Gets or sets the action to be performed when the close button is pressed.
        /// </summary>
        public ICommand CloseGroup { get; set; }

        /// <summary>
        /// Gets or sets the action to be performed when a message is ready to send.
        /// </summary>
        public ICommand SendMessage { get; set; }

        /// <summary>
        /// Gets or sets the action to be performed when the user wants to send an attachment file.
        /// </summary>
        public ICommand SendAttachment { get; set; }

        /// <summary>
        /// Gets or sets the action to be performd when the user has selected the Message Effects Generator.
        /// </summary>
        public ICommand OpenMessageSuggestions { get; set; }

        /// <summary>
        /// Gets the action to be performed when more messages need to be loaded.
        /// </summary>
        public ICommand ReloadView { get; private set; }

        /// <summary>
        /// Gets the action to be be performed when the selected messages have changed.
        /// This is used by the Multi-Like plugin.
        /// </summary>
        public ICommand SelectionChangedCommand { get; }

        /// <summary>
        /// Gets or sets the action to be performd to scroll the messages list to the end.
        /// </summary>
        public ICommand ScrollToEnd { get; set; }

        /// <summary>
        /// Gets the collection of ViewModels for <see cref="Message"/>s to be displayed, in sorted order.
        /// </summary>
        public IObservableCollection<MessageControlViewModelBase> SortedMessages { get; private set; }

        /// <summary>
        /// Gets the collection of available Group/Chat UI Plugins to display.
        /// </summary>
        public ObservableCollection<GroupMeClientPlugin.GroupChat.IGroupChatPlugin> GroupChatPlugins { get; }

        /// <summary>
        /// Gets the collection of available Group/Chat Cache UI Plugins to display.
        /// </summary>
        public ObservableCollection<GroupMeClientPlugin.GroupChat.IGroupChatCachePlugin> GroupChatCachePlugins { get; }

        /// <summary>
        /// Gets the action to be performed when a Plugin in the
        /// Options Menu is activated.
        /// </summary>
        public ICommand GroupChatPluginActivated { get; }

        /// <summary>
        /// Gets the action to be performed when a Cache-type Plugin in the
        /// Options Menu is activated.
        /// </summary>
        public ICommand GroupChatCachePluginActivated { get; }

        /// <summary>
        /// Gets or sets the popup manager to be used for popups.
        /// </summary>
        public Controls.PopupViewModel PopupManager { get; set; }

        /// <summary>
        /// Gets the title of the <see cref="Group"/> or <see cref="Chat"/>.
        /// </summary>
        public string Title => this.MessageContainer.Name;

        /// <summary>
        /// Gets the unique identifier for the <see cref="Group"/> or <see cref="Chat"/>.
        /// </summary>
        public string Id => this.MessageContainer.Id;

        /// <summary>
        /// Gets the list of messages that are currently selected.
        /// </summary>
        public object CurrentlySelectedMessages { get; private set; }

        /// <summary>
        /// Gets or sets the <see cref="IMessageContainer"/> being displayed.
        /// </summary>
        public IMessageContainer MessageContainer
        {
            get => this.messageContainer;
            set => this.Set(() => this.MessageContainer, ref this.messageContainer, value);
        }

        /// <summary>
        /// Gets the avatar to be displayed in the top bar.
        /// </summary>
        public AvatarControlViewModel TopBarAvatar
        {
            get => this.topBarAvatar;
            private set => this.Set(() => this.TopBarAvatar, ref this.topBarAvatar, value);
        }

        /// <summary>
        /// Gets or sets the contents the user has typed for a new message.
        /// </summary>
        public string TypedMessageContents
        {
            get => this.typedMessageContents;
            set => this.Set(() => this.TypedMessageContents, ref this.typedMessageContents, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the messages list is not currently scrolled to the bottom.
        /// </summary>
        public bool IsNotAtBottom
        {
            get => this.isNotAtBottom;
            set => this.Set(() => this.IsNotAtBottom, ref this.isNotAtBottom, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether selecting messages is allowed.
        /// </summary>
        public bool IsSelectionAllowed
        {
            get => this.isSelectionAllowed;
            set => this.Set(() => this.IsSelectionAllowed, ref this.isSelectionAllowed, value);
        }

        private SourceList<MessageControlViewModelBase> Messages { get; }

        private SemaphoreSlim ReloadSem { get; }

        private Message FirstDisplayedMessage { get; set; } = null;

        private DateTime LastMarkerTime { get; set; } = DateTime.MinValue;

        private ReliabilityStateMachine ReliabilityStateMachine { get; }

        private Timer RetryTimer { get; set; }

        private bool IsSending { get; set; }

        private Settings.SettingsManager Settings { get; }

        /// <summary>
        /// Reloads and redisplay the newest messages.
        /// This will capture any messages send since the last reload.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task LoadNewMessages()
        {
            await this.LoadMoreAsync(null, true);
        }

        /// <summary>
        /// Updates the 'Likes' for a currently displayed <see cref="Message"/>.
        /// </summary>
        /// <param name="message">The message containing the updated list of likers.</param>
        public void UpdateMessageLikes(Message message)
        {
            var msgVm = this.Messages.Items.FirstOrDefault(m => m.Id == message.Id);
            if (msgVm is MessageControlViewModel messageVm)
            {
                messageVm.UpdateLikers(message.FavoritedBy);
            }
        }

        /// <inheritdoc />
        void IDisposable.Dispose()
        {
            this.Messages.Clear();

            this.RetryTimer?.Dispose();

            try
            {
                foreach (var msg in this.Messages.Items)
                {
                    (msg as IDisposable)?.Dispose();
                }
            }
            catch (Exception)
            {
            }
        }

        /// <inheritdoc />
        void FileDragDropHelper.IDragDropTarget.OnFileDrop(string[] filepaths)
        {
            string[] supportedExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };

            foreach (var file in filepaths)
            {
                if (supportedExtensions.Contains(Path.GetExtension(file).ToLower()))
                {
                    var data = File.ReadAllBytes(file);
                    this.ShowImageSendDialog(data);
                    break;
                }
            }
        }

        /// <inheritdoc />
        void FileDragDropHelper.IDragDropTarget.OnImageDrop(byte[] image)
        {
            this.ShowImageSendDialog(image);
        }

        private async Task LoadMoreAsync(ScrollViewer scrollViewer = null, bool updateNewest = false)
        {
            await this.ReloadSem.WaitAsync();

            try
            {
                ICollection<Message> results;

                if (this.FirstDisplayedMessage == null || updateNewest)
                {
                    // load the most recent messages
                    results = await this.MessageContainer.GetMessagesAsync();
                }
                else
                {
                    // load the 20 (GroupMe default) messages preceeding the oldest (first) one displayed
                    results = await this.MessageContainer.GetMessagesAsync(GroupMeClientApi.MessageRetreiveMode.BeforeId, this.FirstDisplayedMessage.Id);
                }

                this.UpdateDisplay(scrollViewer, results);

                // if everything was successful, reset the reliability monitor
                this.ReliabilityStateMachine.Succeeded();
                this.RetryTimer?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in {nameof(this.LoadMoreAsync)} - {ex.Message}. Retrying...");
                this.RetryTimer = this.ReliabilityStateMachine.GetRetryTimer(async () => await this.LoadMoreAsync(scrollViewer, updateNewest));
            }
            finally
            {
                this.ReloadSem.Release();
            }
        }

        private void UpdateDisplay(ScrollViewer scrollViewer, ICollection<Message> messages)
        {
            if (messages.Count == 0)
            {
                return;
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Run on UI thread since the ScrollViewer is being directly manipulated.
                double originalHeight = scrollViewer?.GetValue(ScrollViewer.VerticalScrollBarMaximumProperty) ?? 0.0;
                if (originalHeight != 0)
                {
                    // Prevent the At Top event from firing while we are adding new messages
                    scrollViewer.SetValue(ScrollViewer.VerticalScrollBarValueProperty, 1);
                }

                var maxTimeDifference = TimeSpan.FromMinutes(15);

                // Messages retrieved with the before_id parameter are returned in descending order
                // Reverse iterate through the messages collection to go newest->oldest
                for (int i = messages.Count - 1; i >= 0; i--)
                {
                    var msg = messages.ElementAt(i);

                    var oldMsg = this.Messages.Items.FirstOrDefault(m => m.Id == msg.Id);

                    if (oldMsg == null)
                    {
                        // add new message
                        var msgVm = new MessageControlViewModel(
                            msg,
                            showPreviewsOnlyForMultiImages: this.Settings.UISettings.ShowPreviewsForMultiImages);
                        this.Messages.Add(msgVm);

                        // add an inline timestamp if needed
                        if (msg.CreatedAtTime.Subtract(this.LastMarkerTime) > maxTimeDifference)
                        {
                            var messageId = long.Parse(msg.Id);
                            var timeStampId = (messageId - 1).ToString();

                            this.Messages.Add(new InlineTimestampControlViewModel(msg.CreatedAtTime, timeStampId, msgVm.DidISendIt));
                            this.LastMarkerTime = msg.CreatedAtTime;
                        }
                    }
                    else
                    {
                        // update an existing one if needed
                        oldMsg.Message = msg;
                    }
                }

                // process read receipt and sent receipts
                if (this.MessageContainer.ReadReceipt != null)
                {
                    // Remove old markers
                    var toRemove = this.Messages.Items.OfType<InlineReadSentMarkerControlViewModel>().ToList();
                    foreach (var marker in toRemove)
                    {
                        this.Messages.Remove(marker);
                    }

                    // Attach a "Read Receipt" if the read message is displayed.
                    var matchedMessage = this.Messages.Items.FirstOrDefault(m => m.Id == this.MessageContainer.ReadReceipt.MessageId);
                    if (matchedMessage != null)
                    {
                        var msgId = long.Parse(matchedMessage.Id);

                        var readMarker = new InlineReadSentMarkerControlViewModel(
                            this.MessageContainer.ReadReceipt.ReadAtTime,
                            true,
                            (msgId + 1).ToString(),
                            (matchedMessage as MessageControlViewModel).DidISendIt);

                        this.Messages.Add(readMarker);
                    }

                    // Attach a "Sent Receipt" to the last message confirmed sent by GroupMe
                    var me = this.MessageContainer.WhoAmI();
                    var lastSentMessage = this.Messages.Items
                        .OfType<MessageControlViewModel>()
                        .OrderByDescending(m => m.Id)
                        .FirstOrDefault(m => m.Message.UserId == me.Id);

                    if (lastSentMessage != null && lastSentMessage != matchedMessage)
                    {
                        var msgId = long.Parse(lastSentMessage.Id);

                        var sentMarker = new InlineReadSentMarkerControlViewModel(
                            lastSentMessage.Message.CreatedAtTime,
                            false,
                            (msgId + 1).ToString(),
                            (lastSentMessage as MessageControlViewModel).DidISendIt);

                        this.Messages.Add(sentMarker);
                    }

                    // Send a Read Receipt for the last message received
                    var lastReceivedMessage = this.Messages.Items
                        .OfType<MessageControlViewModel>()
                        .OrderByDescending(m => m.Id)
                        .FirstOrDefault(m => m.Message.UserId != me.Id);

                    if (lastReceivedMessage != null && this.MessageContainer is Chat c)
                    {
                        var result = Task.Run(async () => await c.SendReadReceipt(lastReceivedMessage.Message)).Result;
                    }
                }

                if (originalHeight != 0)
                {
                    // Calculate the offset where the last message the user was looking at is
                    // Scroll back to there so new messages appear on top, above screen
                    scrollViewer?.GetObservable(ScrollViewer.VerticalScrollBarMaximumProperty).Take(2).Skip(1).Subscribe(newMax =>
                    {
                        double difference = newMax - originalHeight;
                        scrollViewer.SetValue(ScrollViewer.VerticalScrollBarValueProperty, difference);
                    });
                }

                if (messages.Count > 0)
                {
                    this.FirstDisplayedMessage = messages.Last();
                }
            });
        }

        private async Task SendMessageAsync()
        {
            if (!string.IsNullOrEmpty(this.TypedMessageContents))
            {
                this.IsSending = true;
                var newMessage = Message.CreateMessage(
                    this.TypedMessageContents,
                    guidPrefix: "gmdca");
                await this.SendMessageAsync(newMessage);
            }
        }

        private async Task SendFileImageAttachment()
        {
            var supportedImages = GroupMeClientApi.Models.Attachments.ImageAttachment.SupportedExtensions
                .ToList()
                .Select(e => e.Trim('.'));
            var supportedFiles = GroupMeClientApi.Models.Attachments.FileAttachment.GroupMeDocumentMimeTypeMapper.SupportedExtensions
                .ToList()
                .Select(e => e.Trim('.'));

            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filters.Add(new FileDialogFilter() { Name = "Images", Extensions = { supportedImages } });
            openFileDialog.Filters.Add(new FileDialogFilter() { Name = "Documents", Extensions = { supportedFiles } });

            var fileName = await openFileDialog.ShowAsync(Program.GroupMeMainWindow);
            if (!string.IsNullOrEmpty(fileName.FirstOrDefault()))
            {
                var extension = Path.GetExtension(fileName.FirstOrDefault()).Trim('.');
                if (supportedImages.Contains(extension))
                {
                    this.ShowImageSendDialog(File.ReadAllBytes(fileName.FirstOrDefault()));
                }
                else if (supportedFiles.Contains(extension))
                {
                    this.ShowFileSendDialog(fileName.FirstOrDefault());
                }
            }
        }

        private async Task SendContentMessageAsync(GroupMeClientApi.Models.Attachments.Attachment attachment)
        {
            if (!(this.PopupManager.PopupDialog is SendContentControlViewModelBase))
            {
                return;
            }

            var contentSendDialog = this.PopupManager.PopupDialog as SendContentControlViewModelBase;

            if (contentSendDialog.ContentStream == null)
            {
                return;
            }

            contentSendDialog.IsSending = true;
            this.IsSending = true;

            var contents = contentSendDialog.TypedMessageContents;
            var attachmentsList = new List<GroupMeClientApi.Models.Attachments.Attachment> { attachment };

            var message = Message.CreateMessage(
                contents,
                attachmentsList,
                guidPrefix: "gmdca");
            bool success = await this.SendMessageAsync(message);

            if (success)
            {
                this.ClosePopupHandler();
            }
            else
            {
                contentSendDialog.IsSending = false;
            }
        }

        private async Task<bool> SendMessageAsync(Message newMessage)
        {
            bool success;

            success = await this.MessageContainer.SendMessage(newMessage);
            if (success)
            {
                this.MessageContainer.Messages.Add(newMessage);
                await this.LoadMoreAsync(null, true);

                this.TypedMessageContents = string.Empty;
            }
            else
            {
                var msg = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(new MessageBox.Avalonia.DTO.MessageBoxStandardParams
                {
                    ButtonDefinitions = MessageBox.Avalonia.Enums.ButtonEnum.Ok,
                    ContentTitle = "GroupMe Desktop Client",
                    ContentMessage = "Could Not Send Message",
                    Icon = MessageBox.Avalonia.Enums.Icon.Error,
                });

                await msg.ShowDialog(Program.GroupMeMainWindow);
            }

            this.IsSending = false;

            return success;
        }

        private void ShowImageSendDialog(byte[] imageData)
        {
            var dialog = new SendImageControlViewModel()
            {
                ContentStream = new MemoryStream(imageData),
                MessageContainer = this.MessageContainer,
                TypedMessageContents = this.TypedMessageContents,
                SendMessage = new RelayCommand<GroupMeClientApi.Models.Attachments.Attachment>(async (a) => await this.SendContentMessageAsync(a), (a) => !this.IsSending, true),
            };

            this.PopupManager.PopupDialog = dialog;
        }

        private void ShowFileSendDialog(string fileName)
        {
            var dialog = new SendFileControlViewModel()
            {
                ContentStream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                FileName = System.IO.Path.GetFileName(fileName),
                MessageContainer = this.MessageContainer,
                TypedMessageContents = this.TypedMessageContents,
                SendMessage = new RelayCommand<GroupMeClientApi.Models.Attachments.Attachment>(async (a) => await this.SendContentMessageAsync(a), (a) => !this.IsSending, true),
            };

            this.PopupManager.PopupDialog = dialog;
        }

        private void ClosePopupHandler()
        {
            (this.PopupManager.PopupDialog as IDisposable)?.Dispose();
            this.PopupManager.PopupDialog = null;
        }

        private void ActivateGroupPlugin(GroupMeClientPlugin.GroupChat.IGroupChatPlugin plugin)
        {
            _ = plugin.Activated(this.MessageContainer);
        }

        private void ActivateGroupCachePlugin(GroupMeClientPlugin.GroupChat.IGroupChatCachePlugin plugin)
        {
            var command = new Messaging.IndexAndRunPluginRequestMessage(this.MessageContainer, plugin);
            Messenger.Default.Send(command);
        }

        private void OpenMessageSuggestionsDialog()
        {
            var dialog = new MessageEffectsControlViewModel()
            {
                TypedMessageContents = this.TypedMessageContents,
                UpdateMessage = new RelayCommand(this.UseMessageEffectSuggestion),
            };

            this.PopupManager.PopupDialog = dialog;
        }

        private void UseMessageEffectSuggestion()
        {
            if (!(this.PopupManager.PopupDialog is MessageEffectsControlViewModel))
            {
                return;
            }

            var messageEffectsDialog = this.PopupManager.PopupDialog as MessageEffectsControlViewModel;

            this.TypedMessageContents = messageEffectsDialog.SelectedMessageContents;

            this.ClosePopupHandler();
        }

        private void SelectionChangedHandler(object data)
        {
            this.CurrentlySelectedMessages = data;
        }

        private void ScrollToEndHandler(ListBox messagesList)
        {
            var scroll = messagesList.Scroll as ScrollViewer;
            var maxPosition = scroll.GetValue(ScrollViewer.VerticalScrollBarMaximumProperty);
            scroll.SetValue(ScrollViewer.VerticalScrollBarValueProperty, maxPosition);
        }
    }
}