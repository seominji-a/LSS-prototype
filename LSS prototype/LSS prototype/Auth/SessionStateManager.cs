using System.Collections.Generic;
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

            // 로그인 창 제외한 모든 창 숨기기
            foreach (Window window in Application.Current.Windows)
            {
                if (window.GetType().Name != "Login")
                {
                    window.Hide();  // 닫지 않고 숨기기만
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
            foreach (Window window in _suspendedWindows)
            {
                if (window != null)
                {
                    window.Show();
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