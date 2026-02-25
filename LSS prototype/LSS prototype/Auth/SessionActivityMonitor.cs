using LSS_prototype.Login_Page;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;

namespace LSS_prototype.Auth
{
    public class SessionActivityMonitor
    {
        private Timer _timeoutCheckTimer;
        private Timer _windowCheckTimer;
        private readonly List<Window> _monitoredWindows = new List<Window>();

        public SessionActivityMonitor()
        {
            _timeoutCheckTimer = new Timer(
                CheckSessionTimeout,
                null,
                Timeout.Infinite,
                Timeout.Infinite
            );

            _windowCheckTimer = new Timer(
                CheckForNewWindows,
                null,
                Timeout.Infinite,
                Timeout.Infinite
            );
        }

        public void Start(Window mainWindow)
        {
            StartMonitoring(mainWindow);

            _timeoutCheckTimer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            _windowCheckTimer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        private void CheckForNewWindows(object state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window != null && !_monitoredWindows.Contains(window))
                    {
                        System.Diagnostics.Debug.WriteLine($"[새 창 감지] {window.GetType().Name}");
                        StartMonitoring(window);
                    }
                }
            });
        }

        private void StartMonitoring(Window window)
        {
            if (_monitoredWindows.Contains(window))
                return;

            _monitoredWindows.Add(window);

            window.PreviewMouseMove += OnUserActivity;
            window.PreviewMouseDown += OnUserActivity;
            window.PreviewKeyDown += OnUserActivity;
            window.PreviewTouchDown += OnUserActivity;
            window.PreviewTouchMove += OnUserActivity;
            window.PreviewStylusDown += OnUserActivity;
            window.PreviewStylusMove += OnUserActivity;

            window.Closed += (s, e) =>
            {
                _monitoredWindows.Remove(window);
                RemoveEventHandlers(window);
            };
        }

        private void RemoveEventHandlers(Window window)
        {
            window.PreviewMouseMove -= OnUserActivity;
            window.PreviewMouseDown -= OnUserActivity;
            window.PreviewKeyDown -= OnUserActivity;
            window.PreviewTouchDown -= OnUserActivity;
            window.PreviewTouchMove -= OnUserActivity;
            window.PreviewStylusDown -= OnUserActivity;
            window.PreviewStylusMove -= OnUserActivity;
        }

        private void OnUserActivity(object sender, EventArgs e)
        {
            if (AuthToken.IsAuthenticated)
            {
                AuthToken.Touch();
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] 세션 연장");
            }
        }

        private void CheckSessionTimeout(object state)
        {
            try
            {
                if (AuthToken.IsExpired())
                {
                    _timeoutCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    _windowCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CustomMessageWindow.Show(
                            "세션이 만료되었습니다. 다시 로그인해주세요.",
                            CustomMessageWindow.MessageBoxType.Ok,
                            0,
                            CustomMessageWindow.MessageIconType.Danger);

                        AuthToken.SignOut();
                        NavigateToLoginPage();
                    });
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message + " 세션 닫기문제 발생");
                throw; // 세션관련된 기능이 동작하지않으면 치명적이므로 테스트기간동안 throw 처리 
            }
            
        }

        private void NavigateToLoginPage()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // ★ 숨겨진 창들은 그대로 두고, 로그인 창만 띄우기
                var loginWindow = new Login();
                loginWindow.Show();
                Application.Current.MainWindow = loginWindow;

                // ★ 로그인 창 제외한 보이는 창만 닫기 (숨겨진 창은 유지)
                var windowsToClose = Application.Current.Windows
                    .Cast<Window>()
                    .Where(w => w != loginWindow && w.IsVisible)
                    .ToList();

                foreach (var window in windowsToClose)
                {
                    try
                    {
                        window.Close();
                    }
                    catch { }
                }
            });
        }
    }
}