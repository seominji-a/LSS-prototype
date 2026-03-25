using LSS_prototype.Auth;
using LSS_prototype.DB_CRUD;
using LSS_prototype.Patient_Page;
using LSS_prototype.User_Page;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LSS_prototype.Login_Page
{
    public class LoginViewModel : INotifyPropertyChanged
    {

        #region Fields
        private string _userId;
        private bool _showAdminMode;              // 체크박스 노출 여부
        private bool _isAdminMode; // 어드민 체크 여부 확인 변수
        private List<string> _adminIdList = new List<string>();  // 어드민 권한  ID를 저장할 LIST
        #endregion

        #region Commands & Actions
        // 버튼과 연결될 객체
        public ICommand LoginCommand { get; }
        /// <summary>
        /// 패스워드 포커스 이벤트 호출하기 위한 객체
        /// </summary>
        public Action FocusUserIdAction { get; set; }
        #endregion

        #region Properties
        public string UserId
        {
            get => _userId;
            set
            {
                var cleaned = value?.Replace(" ", ""); // ID에 모든 공백을 제거한다.
                if (_userId == cleaned) return; // 공백을 제외한 CLANED의 값이 기존 userid의 값과 같으면 사용자는 공백을 입력한것이므로
                _userId = cleaned;              // set 작업 return
                OnPropertyChanged();
            }
        }

        public bool ShowAdminMode
        {
            get => _showAdminMode;
            private set
            {
                if (_showAdminMode == value) return;
                _showAdminMode = value;
                OnPropertyChanged();
            }
        }

        public bool IsAdminMode
        {
            get => _isAdminMode;
            set { _isAdminMode = value; OnPropertyChanged(); }
        }
        #endregion

        #region Constructor
        public LoginViewModel()
        {
           LoginCommand = new AsyncRelayCommand(async (param) => await ExecuteLogin(param));
        }
        #endregion

        #region Admin ID 관리
        public async Task LoadAdminIds()
        {
            try
            {
                var db = new DB_Manager();
                _adminIdList?.Clear();
                var list = db.SelectAdminLoginIds();

                if (list != null)
                {
                    _adminIdList.AddRange(list);
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                _adminIdList?.Clear();
            }
        }

        // PasswordBox 포커스 들어올 때 호출되는 함수
        public void UpdateAdminModeVisibilityByUserId()
        {
            string id = (UserId ?? string.Empty).Trim();

            bool isAdminId = !string.IsNullOrEmpty(id) && _adminIdList.Contains(id);

            ShowAdminMode = isAdminId;

            // Admin이 아니면 체크도 강제로 해제
            if (!isAdminId)
                IsAdminMode = false;
        }
        #endregion

        #region Login 처리
        private async Task ExecuteLogin(object parameter)
        {

            // PasswordBox는 바인딩이 어려워 CommandParameter로 전달받아 사용
            var passwordBox = parameter as PasswordBox;
            string password = passwordBox != null ? passwordBox.Password : string.Empty;

            string roleCode = string.Empty;
            DateTime? passwordChangedAt = null;
            string user_id = string.Empty;
            var dbManager = new DB_Manager();

            try
            {

                // ══════════════════════════════════════════
                // 0) MASTER 계정 OTP 검증 (DB 조회 없이 환경변수 기반)
                // ══════════════════════════════════════════
                if (await Common.VerifyMasterOtp(UserId, password))
                {
                    // OTP 인증 성공 → 세션 시작 (roleCode는 MASTER 고정)

                    AuthToken.SignIn(UserId, "M"); //마스터 계정 ROLE_CODE M

                    await CustomMessageWindow.ShowAsync(
                        "MASTER 로그인 성공\n관리자 화면으로 이동합니다.",
                        CustomMessageWindow.MessageBoxType.Ok,
                        1,
                        CustomMessageWindow.MessageIconType.Info);

                    Common.CurrentUserId = UserId;
                    var masterShell = new MainPage();
                    masterShell.Show();
                    masterShell.NavigateTo(new User());
                    App.ActivityMonitor.Start(masterShell); // ← 세션 관리 기능은 테스트때 잠시 주석
                    _ = AutoCleanupService.RunAsync(); // 만료기한 지난 삭제된 데이터 정리
                    CloseLoginWindow();
                    return;
                }

                // 1) 로그인 검증 (해시/솔트 + ROLE_CODE + PASSWORD_CHANGED_AT)
                if (!dbManager.Login_check(UserId, password, out roleCode, out passwordChangedAt, out user_id))
                {
                        await CustomMessageWindow.ShowAsync(
                        "아이디 또는 비밀번호가 올바르지 않습니다.",
                        CustomMessageWindow.MessageBoxType.Ok,
                        0,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }


                // 2 ) 최초 로그인(비밀번호 변경 이력 없음) → 무조건 변경 강제
                // ※ 세션 복원(잠금 해제)은 SessionLoginViewModel.ExecuteUnlock 에서 처리
                // user_id의 값이 1이라는것은 최초로 등록된 AMDIN 계정이기 때문이라는 뜻
                // 3번의 로직은, 최초로 등록되어있는 ADMIN 1개의 ID에 대해서만 비밀번호 변경페이지로 이동시킨다.
                // 추후 마지막 비밀번호 변경일 +30일이 지나면 모든 사용자에게 경고를 주려면
                // 아래 코드를 수정하면됨 ( 0224 박한용 )
                if (!passwordChangedAt.HasValue && user_id == "1")
                {
                        await CustomMessageWindow.ShowAsync(
                        "최초 로그인입니다.\n비밀번호 변경 페이지로 이동합니다.",
                        CustomMessageWindow.MessageBoxType.Ok,
                        3,
                        CustomMessageWindow.MessageIconType.Info);


                    var dlg = new ChangePasswordDialog(new ChangePasswordModelView(UserId));
                    dlg.Owner = Application.Current.Windows.OfType<Login>().FirstOrDefault();
                    dlg.Topmost = true;
                    bool? result = dlg.ShowDialog();

                    if (result != true)
                    {
                            await CustomMessageWindow.ShowAsync(
                            "비밀번호 변경이 완료되지 않았습니다.",
                            CustomMessageWindow.MessageBoxType.Ok,
                            2,
                            CustomMessageWindow.MessageIconType.Warning);

                    }
                    UserId = string.Empty;
                    await LoadAdminIds();
                    UpdateAdminModeVisibilityByUserId(); // 추가
                    passwordBox?.Clear();
                    FocusUserIdAction?.Invoke();         // 추가
                    return; // 비밀번호 변경 이벤트가 일어났을땐, 무조건 해당 함수 한번 종료하고
                    // 다시 사용자가 로그인 버튼을 눌러 해당 함수를 호출하도록 구현 ( 0224 박한용 )

                }


                // 4 ) 로그인 성공 → 세션/토큰 시작
                AuthToken.SignIn(UserId, roleCode);

                string msg = "로그인 성공";
                if (IsAdminMode) msg = "로그인 성공\n 관리자 화면으로 이동합니다.";

               await CustomMessageWindow.ShowAsync(
               msg,
               CustomMessageWindow.MessageBoxType.Ok,
               1,
               CustomMessageWindow.MessageIconType.Info);

                var shell = new MainPage();
                shell.Show();

                // AdminMode 체크 여부에 따라 화면 분기
                if (IsAdminMode)
                    shell.NavigateTo(new User());
                else
                    shell.NavigateTo(new Patient());

                Common.CurrentUserId = UserId;
                var pacsSet = new DB_Manager().GetPacsSet();
                Common.MwlDescriptionFilter = pacsSet?.MwlDescriptionFilter ?? "";

                App.ActivityMonitor.Start(shell); // ← 세션 관리 기능은 테스트때 잠시 주석
                _ = AutoCleanupService.RunAsync(); // 만료기한 지난 삭제된 데이터 정리
                CloseLoginWindow();
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }
        #endregion

        #region Window 관리
        private void CloseLoginWindow()
        {
            Application.Current.Windows.OfType<Login>().FirstOrDefault()?.Close();
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}