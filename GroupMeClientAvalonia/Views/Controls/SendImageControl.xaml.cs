﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GroupMeClientAvalonia.Views.Controls
{
    public class SendImageControl : UserControl
    {
        public SendImageControl()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
