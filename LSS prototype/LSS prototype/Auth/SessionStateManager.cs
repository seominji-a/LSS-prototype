using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace LSS_prototype.Auth
{
    /// <summary>
    /// 세션 만료 시 현재 상태를 저장하고 재로그인 시 복원
    /// </summary>
    public static class SessionStateManager
    {
        private static List<Window> _suspendedWindows = new List<Window>();
        private static bool _isSessionSuspended = false;

        public static bool IsSessionSuspended => _isSessionSuspended;

        /// <summary>
        /// 현재 열린 모든 창 일시정지 (숨기기)
        /// </summary>
        public static void SuspendSession()
        {
            _isSessionSuspended = true;
            _suspendedWindows.Clear();

            foreach (Window window in Application.Current.Windows.Cast<Window>().ToList())
            {
                // Login / SessionLogin 창은 잠금 흐름의 주체이므로 숨김 대상에서 제외
                string typeName = window.GetType().Name;
                if (typeName != "Login" && typeName != "SessionLogin")
                {
                    window.Hide();
                    _suspendedWindows.Add(window);
                }
            }
        }

        /// <summary>
        /// 재로그인 시 이전 세션 복원
        /// </summary>
        public static void RestoreSession()
        {
            if (!_isSessionSuspended)
                return;

            // 숨겨뒀던 창들 다시 보이기
            // IsLoaded가 false인 창은 이미 닫힌 상태이므로 건너뜀
            foreach (Window window in _suspendedWindows)
            {
                try
                {
                    // IsLoaded 가 true 여도 이미 닫힌 창이면 Show()가 예외를 던질 수 있으므로 방어 처리
                    if (window != null && window.IsLoaded)
                        window.Show();
                }
                catch (InvalidOperationException)
                {
                    // 창이 이미 닫힌 경우 무시
                }
            }

            // 마지막에 활성화됐던 창을 메인으로
            if (_suspendedWindows.Count > 0)
            {
                var lastWindow = _suspendedWindows[_suspendedWindows.Count - 1];
                Application.Current.MainWindow = lastWindow;
                lastWindow.Activate();
            }

            _isSessionSuspended = false;
        }

        /// <summary>
        /// 세션 완전 종료 (로그아웃 시)
        /// </summary>
        public static void ClearSession()
        {
            // 모든 창 닫기
            foreach (Window window in _suspendedWindows)
            {
                if (window != null)
                {
                    window.Close();
                }
            }

            _suspendedWindows.Clear();
            _isSessionSuspended = false;
        }
    }
}