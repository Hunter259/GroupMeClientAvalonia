﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Linq;

namespace GroupMeClientAvalonia.Extensions
{
    /// <summary>
    /// FileDragDropHelper.
    /// </summary>
    public class FileDragDropHelper
    {
        /// <summary>
        /// Gets a property indicating if FileDragDrop is supported.
        /// </summary>
        public static readonly AvaloniaProperty IsFileDragDropEnabledProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>(
                "IsFileDragDropEnabled",
                typeof(FileDragDropHelper),
                defaultValue: false);
        //DependencyProperty.RegisterAttached("IsFileDragDropEnabled", typeof(bool), typeof(FileDragDropHelper), new PropertyMetadata(OnFileDragDropEnabled));

        /// <summary>
        /// Gets a property containing the File Drag Drop handler target.
        /// </summary>
        public static readonly AvaloniaProperty FileDragDropTargetProperty =
            AvaloniaProperty.RegisterAttached<Control, FileDragDropHelper>(
                "FileDragDropTarget",
                typeof(FileDragDropHelper));
                
        //DependencyProperty.RegisterAttached("FileDragDropTarget", typeof(object), typeof(FileDragDropHelper), null);

        /// <summary>
        /// <see cref="IDragDropTarget"/> enables receiving updates when data is dropped onto a control.
        /// </summary>
        /// <remarks>
        /// Adapted from https://stackoverflow.com/a/37608994.
        /// </remarks>
        public interface IDragDropTarget
        {
            /// <summary>
            /// Executed when a file has been dragged onto the target.
            /// </summary>
            /// <param name="filepaths">The file name(s) dropped.</param>
            void OnFileDrop(string[] filepaths);

            /// <summary>
            /// Executed when an image has been dragged onto the target.
            /// </summary>
            /// <param name="image">The raw image data that was dropped.</param>
            void OnImageDrop(byte[] image);
        }

        /// <summary>
        /// Gets a value indicating whether File Drag Drop is supported.
        /// </summary>
        /// <param name="obj">The dependency object to retreive the property from.</param>
        /// <returns>A boolean indicating whether enabled.</returns>
        public static bool GetIsFileDragDropEnabled(AvaloniaObject obj)
        {
            return (bool)obj.GetValue(IsFileDragDropEnabledProperty);
        }

        /// <summary>
        /// Sets a value indicating whether File Drag Drop is supported.
        /// </summary>
        /// <param name="obj">The dependency object to retreive the property from.</param>
        /// <param name="value">Whether drag drop is supported.</param>
        public static void SetIsFileDragDropEnabled(AvaloniaObject obj, bool value)
        {
            obj.SetValue(IsFileDragDropEnabledProperty, value);
        }

        /// <summary>
        /// Gets a value containing the Drag Drop target.
        /// </summary>
        /// <param name="obj">The dependency object to retreive the property from.</param>
        /// <returns>The drag drop target.</returns>
        public static bool GetFileDragDropTarget(AvaloniaObject obj)
        {
            return (bool)obj.GetValue(FileDragDropTargetProperty);
        }

        /// <summary>
        /// Sets the drag drop target.
        /// </summary>
        /// <param name="obj">The dependency object to retreive the property from.</param>
        /// <param name="value">The target to assign.</param>
        public static void SetFileDragDropTarget(AvaloniaObject obj, bool value)
        {
            obj.SetValue(FileDragDropTargetProperty, value);
        }

        private static void OnFileDragDropEnabled(IAvaloniaObject d)
        {
            //if (e.NewValue == e.OldValue)
            //{
            //    return;
            //}

            if (d is Control control)
            {
                control.AddHandler(DragDrop.DropEvent, OnDrop);

                //CommandManager.AddPreviewExecutedHandler(control, OnPreviewExecuted);
                //CommandManager.AddPreviewCanExecuteHandler(control, OnPreviewCanExecute);
            }
        }

        private static void OnDrop(object sender, DragEventArgs dragEventArgs)
        {
            if (!(sender is AvaloniaObject d))
            {
                return;
            }

            var target = d.GetValue(FileDragDropTargetProperty);
            if (target is IDragDropTarget fileTarget)
            {
                if (dragEventArgs.Data.Contains(DataFormats.FileNames))
                {
                    fileTarget.OnFileDrop(dragEventArgs.Data.GetFileNames().ToArray());
                }
            }
            else
            {
                throw new Exception("FileDragDropTarget object must be of type IFileDragDropTarget");
            }
        }

        //private static void OnPreviewCanExecute(object sender, CanExecuteRoutedEventArgs e)
        //{
        //    if (e.Command == ApplicationCommands.Paste)
        //    {
        //        e.CanExecute = true;
        //        e.Handled = true;
        //    }
        //}

        //private static void OnPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        //{
        //    if (e.Command == ApplicationCommands.Paste)
        //    {
        //        if (Clipboard.ContainsImage())
        //        {
        //            if (!(sender is DependencyObject d))
        //            {
        //                return;
        //            }

        //            var target = d.GetValue(FileDragDropTargetProperty);

        //            if (target is IDragDropTarget fileTarget)
        //            {
        //                var image = Clipboard.GetImage();
        //                var imageBytes = Utilities.ImageUtils.BitmapSourceToBytes(image);

        //                fileTarget.OnImageDrop(imageBytes);
        //            }
        //            else
        //            {
        //                throw new Exception("FileDragDropTarget object must be of type IFileDragDropTarget");
        //            }

        //            e.Handled = true;
        //        }
        //    }
        //}
    }
}
