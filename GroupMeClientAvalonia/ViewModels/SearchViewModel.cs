﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GroupMeClientAvalonia.ViewModels.Controls;
using GroupMeClientApi.Models;
using GroupMeClientPlugin.GroupChat;
using Microsoft.EntityFrameworkCore;
using System.Reactive;
using ReactiveUI;
using DynamicData.Binding;
using DynamicData;
using System.Reactive.Linq;
using GroupMeClientApi.Models.Attachments;

namespace GroupMeClientAvalonia.ViewModels
{
    /// <summary>
    /// <see cref="SearchViewModel"/> provides a ViewModel for the <see cref="Controls.SearchView"/> view.
    /// </summary>
    public class SearchViewModel : ViewModelBase, ICachePluginUIIntegration
    {
        private string searchTerm = string.Empty;
        private string selectedGroupName = string.Empty;
        private bool filterHasAttachedImage;
        private bool filterHasAttachedLinkedImage;
        private bool filterHasAttachedMentions;
        private bool filterHasAttachedVideo;
        private DateTime filterStartDate;
        private DateTime filterEndDate;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchViewModel"/> class.
        /// </summary>
        /// <param name="groupMeClient">The client to use.</param>
        /// <param name="cacheContext">The cache database to use.</param>
        public SearchViewModel(GroupMeClientApi.GroupMeClient groupMeClient, Caching.CacheContext cacheContext)
        {
            this.GroupMeClient = groupMeClient ?? throw new System.ArgumentNullException(nameof(groupMeClient));
            this.CacheContext = cacheContext ?? throw new ArgumentNullException(nameof(cacheContext));

            this.ResultsView = new PaginatedMessagesControlViewModel()
            {
                MessageSelectedCommand = new RelayCommand<MessageControlViewModelBase>(this.MessageSelected),
                ShowLikers = false,
                NewestAtBottom = false,
            };

            this.ContextView = new PaginatedMessagesControlViewModel()
            {
                ShowTitle = false,
                ShowLikers = true,
                SyncAndUpdate = true,
                NewestAtBottom = true,
            };

            this.PopupManager = new Controls.PopupViewModel()
            {
                ClosePopup = new RelayCommand(this.CloseLittlePopup),
                EasyClosePopup = new RelayCommand(this.CloseLittlePopup),
            };

            this.GroupChatCachePlugins = new ObservableCollection<GroupMeClientPlugin.GroupChat.IGroupChatCachePlugin>();
            this.GroupChatCachePluginActivated =
                new RelayCommand<GroupMeClientPlugin.GroupChat.IGroupChatCachePlugin>(this.ActivateGroupPlugin);

            foreach (var plugin in Plugins.PluginManager.Instance.GroupChatCachePlugins)
            {
                this.GroupChatCachePlugins.Add(plugin);
            }

            this.SortedGroupChats = new ObservableCollectionExtended<GroupControlViewModel>();
            this.AllGroupsChats = new SourceList<GroupControlViewModel>();

            var updatedSort = this.AllGroupsChats
                .Connect()
                .WhenPropertyChanged(c => c.LastUpdated)
                .Throttle(TimeSpan.FromMilliseconds(250))
                .Select(_ => Unit.Default);

            this.AllGroupsChats.AsObservableList()
                .Connect()
                .Sort(SortExpressionComparer<GroupControlViewModel>.Descending(g => g.LastUpdated), resort: updatedSort)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(this.SortedGroupChats)
                .Subscribe();

            this.Loaded = new RelayCommand(this.StartIndexing);
        }

        /// <summary>
        /// Gets the action that should be executed when the search page loads.
        /// </summary>
        public ICommand Loaded { get; private set; }

        /// <summary>
        /// Gets a listing of all available Groups and Chats.
        /// </summary>
        public IObservableCollection<GroupControlViewModel> SortedGroupChats { get; }

        /// <summary>
        /// Gets or sets the popup manager to be used for popups 
        /// </summary>
        public Controls.PopupViewModel PopupManager { get; set; }

        /// <summary>
        /// Gets the ViewModel for the paginated search results.
        /// </summary>
        public PaginatedMessagesControlViewModel ResultsView { get; }

        /// <summary>
        /// Gets the ViewModel for the in-context message view.
        /// </summary>
        public PaginatedMessagesControlViewModel ContextView { get; }

        /// <summary>
        /// Gets the collection of ViewModels for <see cref="Message"/>s to be displayed.
        /// </summary>
        public ObservableCollection<GroupMeClientPlugin.GroupChat.IGroupChatCachePlugin> GroupChatCachePlugins { get; }

        /// <summary>
        /// Gets the action to be performed when a Plugin in the
        /// Options Menu is activated.
        /// </summary>
        public ICommand GroupChatCachePluginActivated { get; }

        /// <summary>
        /// Gets or sets the plugin that should be automatically executed when indexing is complete.
        /// This property is only used for UI-automation tasks. If null, the UI will be displayed normally
        /// when loading is complete. If a plugin is specified, the group specified in <see cref="ActivatePluginForGroupOnLoad"/>
        /// will be used as a parameter.
        /// </summary>
        public IGroupChatCachePlugin ActivatePluginOnLoad { get; set; }

        /// <summary>
        /// Gets or sets a value indicating which group should be passed as a parameter to an automatically executed
        /// plugin. See <see cref="ActivatePluginOnLoad"/> for more information.
        /// </summary>
        public IMessageContainer ActivatePluginForGroupOnLoad { get; set; }

        /// <summary>
        /// Gets or sets the string entered to search for.
        /// </summary>
        public string SearchTerm
        {
            get => this.searchTerm;
            set => this.SetSearchProperty(() => this.SearchTerm, ref this.searchTerm, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether only messages containing an image attachment should be shown.
        /// </summary>
        public bool FilterHasAttachedImage
        {
            get => this.filterHasAttachedImage;
            set => this.SetSearchProperty(() => this.FilterHasAttachedImage, ref this.filterHasAttachedImage, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether only messages containing a linked image attachment should be shown.
        /// </summary>
        public bool FilterHasAttachedLinkedImage
        {
            get => this.filterHasAttachedLinkedImage;
            set => this.SetSearchProperty(() => this.FilterHasAttachedLinkedImage, ref this.filterHasAttachedLinkedImage, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether only messages mentioning other users should be shown.
        /// </summary>
        public bool FilterHasAttachedMentions
        {
            get => this.filterHasAttachedMentions;
            set => this.SetSearchProperty(() => this.FilterHasAttachedMentions, ref this.filterHasAttachedMentions, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether only messages containing a video attachment should be shown.
        /// </summary>
        public bool FilterHasAttachedVideo
        {
            get => this.filterHasAttachedVideo;
            set => this.SetSearchProperty(() => this.FilterHasAttachedVideo, ref this.filterHasAttachedVideo, value);
        }

        /// <summary>
        /// Gets or sets the beginning date for the time period of messages to display.
        /// </summary>
        public DateTime FilterStartDate
        {
            get => this.filterStartDate;
            set => this.SetSearchProperty(() => this.FilterStartDate, ref this.filterStartDate, value);
        }

        /// <summary>
        /// Gets or sets the ending date for the time period of messages to display.
        /// </summary>
        public DateTime FilterEndDate
        {
            get => this.filterEndDate;
            set => this.SetSearchProperty(() => this.FilterEndDate, ref this.filterEndDate, value);
        }

        /// <summary>
        /// Gets the name of the selected group.
        /// </summary>
        public string SelectedGroupName
        {
            get { return this.selectedGroupName; }
            private set { this.Set(() => this.SelectedGroupName, ref this.selectedGroupName, value); }
        }

        private GroupMeClientApi.GroupMeClient GroupMeClient { get; }

        private Caching.CacheContext CacheContext { get; }

        private IMessageContainer SelectedGroupChat { get; set; }

        private Task IndexingTask { get; set; }

        private CancellationTokenSource CancellationTokenSource { get; set; }

        private SourceList<GroupControlViewModel> AllGroupsChats { get; }

        /// <inheritdoc/>
        void ICachePluginUIIntegration.GotoContextView(Message message, IMessageContainer container)
        {
            this.OpenNewGroupChat(container);
            this.UpdateContextView(message);
        }

        private void SetSearchProperty<T>(System.Linq.Expressions.Expression<Func<T>> propertyExpression, ref T field, T newValue)
        {
            this.Set(propertyExpression, ref field, newValue);
            this.UpdateSearchResults();
        }

        private void StartIndexing()
        {
            if (this.IndexingTask != null && !(this.IndexingTask.IsCompleted || this.IndexingTask.IsCanceled))
            {
                // handle cancellation and restart
                this.CancellationTokenSource.Cancel();
                this.IndexingTask.ContinueWith(async (l) =>
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => this.StartIndexing());
                });
                return;
            }

            this.CancellationTokenSource = new CancellationTokenSource();
            this.IndexingTask = this.IndexGroups();
        }

        private async Task IndexGroups()
        {
            var loadingDialog = new LoadingControlViewModel();
            this.PopupManager.PopupDialog = loadingDialog;

            var groups = await this.GroupMeClient.GetGroupsAsync();
            var chats = await this.GroupMeClient.GetChatsAsync();
            var groupsAndChats = Enumerable.Concat<IMessageContainer>(groups, chats);

            this.AllGroupsChats.Clear();

            foreach (var group in groupsAndChats)
            {
                this.CancellationTokenSource.Token.ThrowIfCancellationRequested();

                if (this.ActivatePluginForGroupOnLoad != null && this.ActivatePluginOnLoad != null)
                {
                    // if a plugin is set to automatically execute for only a single group,
                    // index only that group to improve performance
                    if (this.ActivatePluginForGroupOnLoad.Id != group.Id)
                    {
                        continue;
                    }
                }

                loadingDialog.Message = $"Indexing {group.Name}";
                await this.IndexGroup(group);

                // Add Group/Chat to the list
                var vm = new GroupControlViewModel(group)
                {
                    GroupSelected = new RelayCommand<GroupControlViewModel>((s) => this.OpenNewGroupChat(s.MessageContainer), (g) => true, true),
                };
                this.AllGroupsChats.Add(vm);
            }

            this.PopupManager.PopupDialog = null;

            // Check to see if a plugin should be automatically executed.
            if (this.ActivatePluginForGroupOnLoad != null && this.ActivatePluginOnLoad != null)
            {
                var cache = this.GetMessagesForGroup(this.ActivatePluginForGroupOnLoad);
                _ = this.ActivatePluginOnLoad.Activated(this.ActivatePluginForGroupOnLoad, cache, this);

                this.ActivatePluginForGroupOnLoad = null;
                this.ActivatePluginOnLoad = null;
            }
        }

        private async Task IndexGroup(IMessageContainer container)
        {
            var groupState = this.CacheContext.IndexStatus.Find(container.Id);

            if (groupState == null)
            {
                groupState = new Caching.CacheContext.GroupIndexStatus()
                {
                    Id = container.Id,
                };
                this.CacheContext.IndexStatus.Add(groupState);
            }

            async Task<ICollection<Message>> InitialDownloadAction()
            {
                return await container.GetMessagesAsync();
            }

            var newestMessages = await Utilities.ReliabilityStateMachine.TryUntilSuccess(InitialDownloadAction, this.CancellationTokenSource.Token);

            this.CacheContext.AddMessages(newestMessages);

            long.TryParse(groupState.LastIndexedId, out var lastIndexId);
            long.TryParse(newestMessages.Last().Id, out var retreiveFrom);

            while (lastIndexId < retreiveFrom && !this.CancellationTokenSource.IsCancellationRequested)
            {
                // not up-to-date, we need to retreive the delta
                async Task<ICollection<Message>> DownloadDeltaAction()
                {
                    return await container.GetMaxMessagesAsync(GroupMeClientApi.MessageRetreiveMode.BeforeId, retreiveFrom.ToString());
                }

                var results = await Utilities.ReliabilityStateMachine.TryUntilSuccess(DownloadDeltaAction, this.CancellationTokenSource.Token);

                this.CacheContext.AddMessages(results);

                if (results.Count == 0)
                {
                    // we've hit the top.
                    break;
                }

                long.TryParse(results.Last().Id, out var latestRetreivedOldestId);
                retreiveFrom = latestRetreivedOldestId;
            }

            groupState.LastIndexedId = newestMessages.First().Id; // everything is downloaded
            await this.CacheContext.SaveChangesAsync(this.CancellationTokenSource.Token);
        }

        private void OpenNewGroupChat(IMessageContainer group)
        {
            this.FilterStartDate = group.CreatedAtTime.AddDays(-1);
            this.FilterEndDate = DateTime.Now.AddDays(1);

            this.SelectedGroupChat = group;
            this.SearchTerm = string.Empty;
            this.SelectedGroupName = group.Name;
            this.ContextView.Messages = null;
        }

        private void MessageSelected(MessageControlViewModelBase message)
        {
            if (message != null)
            {
                this.UpdateContextView(message.Message);
            }
        }

        private IQueryable<Message> GetMessagesForGroup(IMessageContainer group)
        {
            if (group is Group g)
            {
                return this.CacheContext.Messages
                    .AsNoTracking()
                    .Where(m => m.GroupId == g.Id);
            }
            else if (group is Chat c)
            {
                // Chat.Id returns the Id of the other user
                // However, GroupMe messages are natively returned with a Conversation Id instead
                // Conversation IDs are user1+user2.
                var sampleMessage = c.Messages.FirstOrDefault();

                return this.CacheContext.Messages
                    .AsNoTracking()
                    .Where(m => m.ConversationId == sampleMessage.ConversationId);
            }
            else
            {
                return Enumerable.Empty<Message>().AsQueryable();
            }
        }

        private void UpdateSearchResults()
        {
            this.ResultsView.Messages = null;

            var messagesForGroupChat = this.GetMessagesForGroup(this.SelectedGroupChat);

            var startDate = this.FilterStartDate;
            var endDate = (this.FilterEndDate == DateTime.MinValue) ? DateTime.Now : this.FilterEndDate.AddDays(1);

            var startDateUnix = ((DateTimeOffset)startDate).ToUnixTimeSeconds();
            var endDateUnix = ((DateTimeOffset)endDate).ToUnixTimeSeconds();

            var results = messagesForGroupChat
                .Where(m => m.Text.ToLower().Contains(this.SearchTerm.ToLower()))
                .Where(m => m.CreatedAtUnixTime >= startDateUnix)
                .Where(m => m.CreatedAtUnixTime <= endDateUnix);

            var filteredMessages = Enumerable.Empty<Message>().AsQueryable();
            var filtersApplied = false;

            if (this.FilterHasAttachedImage)
            {
                var messagesWithImages = results.AsEnumerable()
                    .Where(m => m.Attachments.OfType<ImageAttachment>().Count() >= 1);

                filteredMessages = filteredMessages.Union(messagesWithImages);
                filtersApplied = true;
            }

            if (this.FilterHasAttachedLinkedImage)
            {
                var messagesWithLinkedImages = results.AsEnumerable()
                    .Where(m => m.Attachments.OfType<LinkedImageAttachment>().Count() >= 1);

                filteredMessages = filteredMessages.Union(messagesWithLinkedImages);
                filtersApplied = true;
            }

            if (this.FilterHasAttachedVideo)
            {
                var messagesWithVideos = results.AsEnumerable()
                    .Where(m => m.Attachments.OfType<VideoAttachment>().Count() >= 1);

                filteredMessages = filteredMessages.Union(messagesWithVideos);
                filtersApplied = true;
            }

            if (this.FilterHasAttachedMentions)
            {
                var messagesWithMentions = results.AsEnumerable()
                    .Where(m => m.Attachments.OfType<MentionsAttachment>().Count() >= 1);

                filteredMessages = filteredMessages.Union(messagesWithMentions.AsEnumerable());
                filtersApplied = true;
            }

            if (!filtersApplied)
            {
                // No attachment filters were selected, so show all messages
                filteredMessages = results;
            }

            var orderedMessages = filteredMessages
                .OrderByDescending(m => m.Id);

            this.ResultsView.AssociateWith = this.SelectedGroupChat;

            // TODO: Can we disable Client Side evaluation? Breaking change in Entity Framework Core 3
            // Enabling filters will be a lot faster if we can.
            this.ResultsView.Messages = orderedMessages;
            this.ResultsView.ChangePage(0);
        }

        private void UpdateContextView(Message message)
        {
            this.ContextView.Messages = null;

            var messagesForGroupChat = this.GetMessagesForGroup(this.SelectedGroupChat)
                .OrderBy(m => m.Id);

            this.ContextView.AssociateWith = this.SelectedGroupChat;
            this.ContextView.Messages = messagesForGroupChat;
            this.ContextView.EnsureVisible(message);
        }

        private void ActivateGroupPlugin(IGroupChatCachePlugin plugin)
        {
            var cache = this.GetMessagesForGroup(this.SelectedGroupChat);
            _ = plugin.Activated(this.SelectedGroupChat, cache, this);
        }

        private void CloseLittlePopup()
        {
            if (this.PopupManager.PopupDialog is LoadingControlViewModel)
            {
                if (this.IndexingTask != null && !(this.IndexingTask.IsCompleted || this.IndexingTask.IsCanceled))
                {
                    // handle cancellation and restart
                    this.CancellationTokenSource.Cancel();
                    this.IndexingTask.ContinueWith((l) =>
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => this.CloseLittlePopup());
                    });
                    return;
                }
            }

            if (this.PopupManager.PopupDialog is IDisposable d)
            {
                d.Dispose();
            }

            this.PopupManager.PopupDialog = null;
        }
    }
}