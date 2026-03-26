using LSS_prototype.Auth;
using LSS_prototype.DB_CRUD;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LSS_prototype.Login_Page
{
    public class SessionLoginViewModel : INotifyPropertyChanged
    {
        #region 필드

        private string _currentUserName;

        #endregion

        #region 프로퍼티

        /// <summary>현재 잠금 상태의 사용자 ID (화면 안내문 표시용)</summary>
        public string CurrentUserName
        {
            get => _currentUserName;
            private set { _currentUserName = value; OnPropertyChanged(); }
        }

        #endregion

        #region 커맨드

        public ICommand UnlockCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand ExitCommand { get; }

        #endregion

        #region 생성자

        public SessionLoginViewModel()
        {
            // 잠금 시점의 로그인 ID를 화면에 표시
            CurrentUserName = Common.CurrentUserId;

            UnlockCommand = new AsyncRelayCommand(async p => await ExecuteUnlock(p));
            LogoutCommand = new AsyncRelayCommand(async _ => await Common.ExecuteLogout());
            ExitCommand = new AsyncRelayCommand(async _ => await Common.ExcuteExit());
        }

        #endregion

        #region 잠금 해제

        private async Task ExecuteUnlock(object parameter)
        {
            var passwordBox = parameter as PasswordBox;
            string password = passwordBox?.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(password))
            {
                await CustomMessageWindow.ShowAsync(
                    "비밀번호를 입력해주세요.",
                    CustomMessageWindow.MessageBoxType.Ok,
                    0,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }

            try
            {
                string roleCode = string.Empty;

                // ── 1) MASTER 계정 OTP 검증 먼저 ──
                // MASTER 계정은 DB가 아닌 환경변수 기반 OTP 인증이므로
                // Login_check 보다 먼저 체크해야 함2qj
                bool isMaster = await Common.VerifyMasterOtp(Common.CurrentUserId, password);

                if (!isMaster)
                {
                    // ── 2) 일반 계정 DB 검증 ──
                    var db = new DB_Manager();
                    bool isValid = db.Login_check(
                        Common.CurrentUserId, password,
                        out roleCode, out _, out _);

                    if (!isValid)
                    {
                        await CustomMessageWindow.ShowAsync(
                            "비밀번호가 올바르지 않습니다.",
                            CustomMessageWindow.MessageBoxType.Ok,
                            0,
                            CustomMessageWindow.MessageIconType.Warning);
                        passwordBox?.Focus(); 
                        return;
                    }
                }

                // ── 3) 잠금 해제 성공 → 세션 복원 ──
                if (AuthToken.IsAuthenticated)
                {
                    // 수동 Lock: lock ↔ unlock은 하나의 세션
                    // 토큰 재발급 없이 Touch()로 타이머만 리셋
                    AuthToken.Touch();
                }
                else
                {
                    // 세션 만료: 타임아웃으로 SignOut()된 상태 → 토큰 재발급
                    // MASTER는 roleCode가 "M" 고정
                    AuthToken.SignIn(Common.CurrentUserId, isMaster ? "M" : roleCode);
                }

                SessionStateManager.RestoreSession();

                // 정지했던 세션 모니터 재시작 (lock 중 타이머 정지 → unlock 후 재개)
                var restoredShell = Application.Current.Windows
                    .OfType<MainPage>().FirstOrDefault();
                if (restoredShell != null)
                    App.ActivityMonitor.Start(restoredShell);

                // SessionLogin 창 닫기
                Application.Current.Windows
                    .OfType<SessionLogin>().FirstOrDefault()?.Close();
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}