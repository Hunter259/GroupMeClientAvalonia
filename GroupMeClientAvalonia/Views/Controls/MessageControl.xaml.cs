﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GroupMeClientAvalonia.Views.Controls
{
    public class MessageControl : UserControl
    {
        public MessageControl()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
