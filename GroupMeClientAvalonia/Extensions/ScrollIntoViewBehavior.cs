﻿using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;

namespace GroupMeClientAvalonia.Extensions
{
    /// <summary>
    /// <see cref="ScrollIntoViewForListBox"/> provides a behavior to ensure a selected item in a <see cref="ListBox"/>
    /// is visible.
    /// </summary>
    /// <remarks>
    /// Adapted from https://stackoverflow.com/a/8830961.
    /// </remarks>
    public class ScrollIntoViewBehavior : Behavior<ListBox>
    {
        /// <inheritdoc/>
        protected override void OnAttached()
        {
            base.OnAttached();
            this.AssociatedObject.SelectionChanged += this.AssociatedObject_SelectionChanged;
        }

        /// <inheritdoc/>
        protected override void OnDetaching()
        {
            base.OnDetaching();
            this.AssociatedObject.SelectionChanged -= this.AssociatedObject_SelectionChanged;
        }

        private void AssociatedObject_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox)
            {
                ListBox listBox = sender as ListBox;
                if (listBox.SelectedItem != null)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(
                        (Action)(() =>
                        {
                            Observable.FromEventPattern(this.AssociatedObject, nameof(this.AssociatedObject.LayoutUpdated))
                                .Take(1)
                                .Subscribe(_ =>
                                {
                                    if (listBox.SelectedItem != null)
                                    {
                                        listBox.ScrollIntoView(listBox.SelectedItem);
                                    }
                                });
                        }));
                }
            }
        }
    }
}
