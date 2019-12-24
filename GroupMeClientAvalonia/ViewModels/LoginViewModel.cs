﻿using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace GroupMeClientAvalonia.ViewModels
{
    /// <summary>
    /// <see cref="LoginViewModel"/> provides the ViewModel for the login screen.
    /// </summary>
    public class LoginViewModel : ViewModelBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LoginViewModel"/> class.
        /// </summary>
        /// <param name="settings">The settings manager that should be used.</param>
        public LoginViewModel(Settings.SettingsManager settings)
        {
            this.SettingsManager = settings;

            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            var productName = fvi.ProductName;

            this.OAuthClient = new GroupMeClientApi.OAuthClient(
                productName,
                Properties.Resources.GroupMeAPIClientId);

            this.LoginButtonClicked = new RelayCommand(this.LaunchLogin);

            _ = this.WaitForLogin();
        }

        /// <summary>
        /// Gets the action when the login button is clicked.
        /// </summary>
        public ICommand LoginButtonClicked { get; }

        /// <summary>
        /// Gets or sets the action when the login process has been completed.
        /// </summary>
        public ICommand LoginCompleted { get; set; }

        private Settings.SettingsManager SettingsManager { get; }

        private GroupMeClientApi.OAuthClient OAuthClient { get; }

        private void LaunchLogin()
        {
            Extensions.WebBrowserHelper.OpenUrl(this.OAuthClient.GroupMeOAuthUrl);
        }

        private async Task WaitForLogin()
        {
            var authKeyResult = await this.OAuthClient.GetAuthToken();
            this.OAuthClient.Stop();

            this.SettingsManager.CoreSettings.AuthToken = authKeyResult;
            this.SettingsManager.SaveSettings();

            this.LoginCompleted?.Execute(this);
        }
    }
}