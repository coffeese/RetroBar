using ManagedShell;
using ManagedShell.AppBar;
using ManagedShell.Common.Logging;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Threading;

namespace RetroBar.Utilities
{
    public class WindowManager : IDisposable
    {
        private static object reopenLock = new object();

        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        private bool _reopenPending;
        private bool _displayChangePending;
        private bool _disposed;
        private List<AppBarScreen> _screenState = new List<AppBarScreen>();
        private List<Taskbar> _taskbars = new List<Taskbar>();

        private readonly DictionaryManager _dictionaryManager;
        private readonly ExplorerMonitor _explorerMonitor;
        private readonly StartMenuMonitor _startMenuMonitor;
        private readonly ShellManager _shellManager;
        private readonly Updater _updater;
        private HotkeyManager _hotkeyManager;

        public WindowManager(DictionaryManager dictionaryManager, ExplorerMonitor explorerMonitor, ShellManager shellManager, StartMenuMonitor startMenuMonitor, Updater updater, HotkeyManager hotkeyManager)
        {
            _dictionaryManager = dictionaryManager;
            _explorerMonitor = explorerMonitor;
            _shellManager = shellManager;
            _startMenuMonitor = startMenuMonitor;
            _updater = updater;
            _hotkeyManager = hotkeyManager;

            _shellManager.ExplorerHelper.HideExplorerTaskbar = true;

            openTaskbars();

            _explorerMonitor.ExplorerMonitorStart(this, _shellManager);

            Settings.Instance.PropertyChanged += Settings_PropertyChanged;
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.ShowMultiMon) ||
                e.PropertyName == nameof(Settings.TargetMonitorDeviceName))
            {
                ReopenTaskbars();
            }
        }

        public void ReopenTaskbars()
        {
            if (_disposed || _reopenPending)
            {
                return;
            }

            // Finish the current settings/display notification before closing its windows.
            // Multiple bars can request a rebuild during the same notification.
            _reopenPending = true;
            _dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (!_disposed)
                    {
                        lock (reopenLock)
                        {
                            closeTaskbars();
                            openTaskbars();
                        }
                    }
                }
                finally
                {
                    _reopenPending = false;
                }
            });
        }

        public void NotifyWorkAreaChange()
        {
            ShellLogger.Debug($"WindowManager: Work area change notification received");
            handleDisplayChange();
        }

        public void NotifyDisplayChange(ScreenSetupReason reason)
        {
            ShellLogger.Debug($"WindowManager: Display change notification received ({reason})");
            handleDisplayChange();
        }

        private void handleDisplayChange()
        {
            if (_disposed || _displayChangePending)
            {
                return;
            }

            _displayChangePending = true;
            _dispatcher.BeginInvoke(() =>
            {
                try
                {
                    // AppBar registration also changes the work area; ignore those notifications.
                    if (!_disposed && haveDisplaysChanged())
                    {
                        ReopenTaskbars();
                    }
                }
                finally
                {
                    _displayChangePending = false;
                }
            });
        }

        public bool IsValidHMonitor(IntPtr hMonitor)
        {
            foreach(var screen in _screenState)
            {
                if (screen.HMonitor == hMonitor)
                {
                    return true;
                }
            }

            return false;
        }

        private void closeTaskbars()
        {
            ShellLogger.Debug($"WindowManager: Closing all taskbars");

            foreach (var taskbar in _taskbars)
            {
                taskbar.AllowClose = true;
                taskbar.Close();
            }

            _taskbars.Clear();
        }

        private void openTaskbars()
        {
            _screenState = AppBarScreen.FromAllScreens();

            ShellLogger.Debug($"WindowManager: Opening taskbars");

            foreach (var screen in MonitorSelection.Select(_screenState, Settings.Instance.ShowMultiMon,
                Settings.Instance.TargetMonitorDeviceName, screen => screen.DeviceName, screen => screen.Primary))
            {
                openTaskbar(screen);
            }
        }

        private void openTaskbar(AppBarScreen screen)
        {
            ShellLogger.Debug($"WindowManager: Opening taskbar on screen {screen.DeviceName}");
            Taskbar taskbar = new Taskbar(this, _dictionaryManager, _shellManager, _startMenuMonitor, _updater, _hotkeyManager, screen, Settings.Instance.Edge, Settings.Instance.AutoHide ? AppBarMode.AutoHide : AppBarMode.Normal);
            taskbar.Show();

            _taskbars.Add(taskbar);
        }

        private bool haveDisplaysChanged()
        {
            resetScreenCache();

            var newScreens = AppBarScreen.FromAllScreens();

            if (_screenState.Count == newScreens.Count)
            {
                bool same = true;
                for (int i = 0; i < newScreens.Count; i++)
                {
                    AppBarScreen current = newScreens[i];
                    if (!(_screenState[i].Bounds == current.Bounds && _screenState[i].DeviceName == current.DeviceName && _screenState[i].Primary == current.Primary && _screenState[i].HMonitor == current.HMonitor))
                    {
                        same = false;
                        break;
                    }
                }

                if (same)
                {
                    ShellLogger.Debug("WindowManager: No display changes");
                    return false;
                }
            }

            return true;
        }

        private void resetScreenCache()
        {
            // use reflection to empty screens cache
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var fi = typeof(Screen).GetField("screens", flags) ?? typeof(Screen).GetField("s_screens", flags)
                ?? throw new Exception("Can't find & reset screens cache inside winforms");
            fi.SetValue(null, null);
        }

        public void Dispose()
        {
            _disposed = true;
            _shellManager.ExplorerHelper.HideExplorerTaskbar = false;
            Settings.Instance.PropertyChanged -= Settings_PropertyChanged;
        }
    }
}
