using LSS_prototype.Login_Page;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
            // ══════════════════════════════════════════
            // 이전 세션 잔여 상태 초기화
            // ──────────────────────────────────────────
            // 세션 만료 후 재로그인 시 Start()가 다시 호출되는데
            // _monitoredWindows에 이전 세션 창들이 남아있으면
            // StartMonitoring() 내부의 중복체크(Contains)에 걸려서
            // 이벤트 핸들러가 새로 등록이 안 됨
            // → 마우스/키보드 움직여도 Touch()가 안 불림
            // → 세션 연장이 안 되고 즉시 만료 판정
            // 따라서 Start() 호출 시마다 반드시 초기화 필요
            // ══════════════════════════════════════════
            foreach (var window in _monitoredWindows.ToList())
                RemoveEventHandlers(window); // 기존 창에 붙은 이벤트 핸들러 제거 (메모리 누수 방지)
            _monitoredWindows.Clear();       // 리스트 비우기

            // 새 세션의 메인 창 모니터링 시작
            // 이후 새로 열리는 창들은 CheckForNewWindows()가 1초마다 자동 감지해서 등록
            StartMonitoring(mainWindow);

            // ══════════════════════════════════════════
            // 타이머 시작
            // ──────────────────────────────────────────
            // _timeoutCheckTimer : N초마다 세션 만료 여부 체크 (AuthToken.IsExpired())
            // _windowCheckTimer  : 1초마다 새로 열린 창 감지 → 이벤트 핸들러 자동 부착
            // ══════════════════════════════════════════

            _timeoutCheckTimer.Change(TimeSpan.FromSeconds(3), TimeSpan.FromMinutes(1));  // 세션 잠심 시간 15분 

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

            // ── 버튼 클릭 감지 추가 ──
            window.PreviewMouseDown += OnButtonClick;

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

            // ── 버튼 클릭 감지 제거 ──
            window.PreviewMouseDown -= OnButtonClick;
        }

        private void OnUserActivity(object sender, EventArgs e)
        {
            if (AuthToken.IsAuthenticated)
            {
                AuthToken.Touch();
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] 세션 연장");
            }
        }

        // ══════════════════════════════════════════
        // 버튼 클릭 감지 → 로그 기록
        // 로그인된 사용자의 모든 버튼 클릭을 기록
        // 클릭된 요소에서 상위로 올라가며 Button 을 찾음
        // ══════════════════════════════════════════
        private void OnButtonClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // 로그인된 상태일 때만 기록
                if (!AuthToken.IsAuthenticated) return;

                // 클릭된 원본 요소에서 상위로 올라가며 Button 찾기
                var button = FindAncestorOrSelf<Button>(e.OriginalSource as DependencyObject);
                if (button == null) return;

                string windowName = (sender as Window)?.GetType().Name ?? "Unknown";
                string buttonName = button.Name ?? "unnamed";
                string buttonContent = button.Content?.ToString() ?? "";
                string currentUser = Common.CurrentUserId ?? "Unknown";
                Common.WriteSessionLog(
                $"[버튼클릭] 사용자: {currentUser} | 화면: {windowName} | 버튼명: {buttonName} | 버튼내용: {buttonContent}");
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        // ══════════════════════════════════════════
        // 비주얼 트리를 상위로 탐색하며 T 타입 찾기
        // 예) FindAncestorOrSelf<Button>(클릭된요소)
        // ══════════════════════════════════════════
        private static T FindAncestorOrSelf<T>(DependencyObject obj) where T : DependencyObject
        {
            while (obj != null)
            {
                if (obj is T target)
                    return target;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
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
                            "세션이 만료되었습니다. \n다시 로그인해주세요.",
                            CustomMessageWindow.MessageBoxType.Ok,
                            0,
                            CustomMessageWindow.MessageIconType.Danger);

                        AuthToken.SignOut();
                        NavigateToLoginPage();
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " 세션 닫기문제 발생");
                throw; // 세션관련된 기능이 동작하지않으면 치명적이므로 테스트기간동안 throw 처리
            }
        }

        private void NavigateToLoginPage()
        {
            // 기존 창들을 Close()하지 않고 Hide()로 숨김 보관 → 재로그인 시 그대로 복원 가능
            SessionStateManager.SuspendSession();

            var loginWindow = new Login();
            loginWindow.Show();
            Application.Current.MainWindow = loginWindow;
        }
    }
}