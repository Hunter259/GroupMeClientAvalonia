﻿using System.ComponentModel;

namespace GroupMeClientAvalonia.Settings
{
    /// <summary>
    /// <see cref="ThemeOptions"/> specifies the different theme options that can be applied to the UI.
    /// </summary>
    public enum ThemeOptions
    {
        /// <summary>
        /// Use the system color theme.
        /// </summary>
        [Description("Default Theme")]
        Default,

        /// <summary>
        /// Use the light color theme (white background).
        /// </summary>
        [Description("Light Theme")]
        Light,

        /// <summary>
        /// Use the dark color theme (black background).
        /// </summary>
        [Description("Dark Theme")]
        Dark,
    }
}
