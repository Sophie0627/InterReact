﻿using System;
using System.ComponentModel;
using System.Windows;

#nullable enable

namespace WpfDepth
{
    public abstract class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void NotifyPropertiesChanged()
            => NotifyPropertyChanged("");

        // An Empty value or null for the propertyName parameter indicates that all of the properties have changed.
        protected virtual void NotifyPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public static class Utilities
    {
        public static void MessageBoxShow(string message, string caption, bool terminate = false)
        {
            RunOnDispatcher(() =>
            {
                MessageBox.Show(message, caption);
                if (terminate)
                    // Application.Current.Shutdown() may only be called from the thread that created the Application object.
                    Application.Current.Shutdown();
            });
        }

        public static void RunOnDispatcher(Action action)
        {
            var dispatcher = Application.Current.Dispatcher;
            if (dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }

    }
}
