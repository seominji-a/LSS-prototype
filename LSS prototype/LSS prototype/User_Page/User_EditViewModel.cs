using LSS_prototype.DB_CRUD;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace LSS_prototype.User_Page
{
    public class User_EditViewModel : INotifyPropertyChanged
    {
        public string UserName { get; }
        public string OriginalLoginId { get; }  // 기존 ID 보관 (DB 조회용)
        public bool CanEditId { get; }    // true = 마스터 계정 → ID 수정 가능

        private string _loginId;
        public string LoginId
        {
            get => _loginId;
            set { _loginId = value; OnPropertyChanged(); }
        }

        public Action<bool?> CloseAction { get; set; }
        public ICommand CancelCommand { get; }

        public User_EditViewModel(UserModel user, bool isMaster = false)
        {
            UserName = user.UserName;
            OriginalLoginId = user.LoginId;  // 원본 저장
            LoginId = user.LoginId;  // 화면 바인딩 초기값
            CanEditId = isMaster;
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false));
        }

        public void ExecuteSubmit(string newPassword, string confirmPassword)
        {
            try
            {
                // 1. 빈값 검사
                if (string.IsNullOrEmpty(newPassword))
                {
                    CustomMessageWindow.Show("비밀번호를 입력해주세요.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // 2. 비밀번호 일치 검사
                if (newPassword != confirmPassword)
                {
                    CustomMessageWindow.Show("비밀번호가 일치하지 않습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // 3. 유효성 검사 ( 테스트 기간동안 잠시 주석 )
                string error = DB_Manager.ValidatePassword(newPassword);
                if (error != null)
                {
                    CustomMessageWindow.Show(error,
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // 4. DB 업데이트 ( 경우의수 2가지 )
                // 4-1. Master가 아닌 관리자 계정이 수정하는 경우 → 비밀번호만 변경
                // 4-2. Master 계정이 수정하는 경우 → ID + 비밀번호 변경 가능
                string masterId = Environment.GetEnvironmentVariable("MASTER_ID", EnvironmentVariableTarget.Machine);
                bool isMaster = Common.CurrentUserId == masterId;

                var db = new DB_Manager();

                bool success;

                if (isMaster) // 최고권한을 가진 ID 일 때 
                    success = db.UpdateCredential(OriginalLoginId, LoginId, newPassword);  // 기존ID, 새ID, 새PW
                else
                    success = db.UpdatePassword(OriginalLoginId, newPassword);             // 기존ID, 새PW

                if (success)
                {
                    string msg = isMaster
                        ? "관리자 권한 로그인정보가 \n변경되었습니다."
                        : "비밀번호가 변경되었습니다.";

                    CustomMessageWindow.Show(msg,
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Info);

                    CloseAction?.Invoke(true);
                }
                else
                {
                    CustomMessageWindow.Show("변경에 실패했습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Warning);
                }
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}