using LSS_prototype.DB_CRUD;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace LSS_prototype.Login_Page
{
    public class ChangePasswordModelView : INotifyPropertyChanged
    {
        private readonly string _loginId;
        public string LoginId => _loginId;

        public Action<bool> CloseAction { get; set; }

        public ChangePasswordModelView(string loginId)
        {
            _loginId = loginId;
        }

        public async Task SaveAsync(string newPw, string confirmPw)
        {
            // 1) 검증
            if (string.IsNullOrWhiteSpace(newPw) || newPw.Length < 4)
            {
                await CustomMessageWindow.ShowAsync(
                    "비밀번호는 4자리 이상으로 입력해주세요.",
                    CustomMessageWindow.MessageBoxType.AutoClose,
                    2,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }

            if (newPw != confirmPw)
            {
                await CustomMessageWindow.ShowAsync(
                    "비밀번호가 일치하지 않습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose,
                    2,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }

            // 해시/솔트/DB UPDATE는 부분 0225에 작성하기 
            // 2) 해시/솔트 생성 ( 기존 해시 생성 함수로 변경 )
            //string salt = PasswordUtil.CreateSalt();
            //string hash = PasswordUtil.HashWithSalt(newPw, salt);

            // 3) DB 업데이트 + PASSWORD_CHANGED_AT = CURRENT_TIMESTAMP
            //var db = new DB_Manager();
            //bool ok = db.UpdatePassword(_loginId, hash, salt);
           /* bool ok = false;
            if (!ok)
            {
                await CustomMessageWindow.ShowAsync(
                    "비밀번호 변경에 실패했습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose,
                    2,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }*/

            await CustomMessageWindow.ShowAsync(
                "비밀번호가 변경되었습니다. 다시 로그인해주세요.",
                CustomMessageWindow.MessageBoxType.AutoClose,
                1,
                CustomMessageWindow.MessageIconType.Info);

            // 4) 성공 flag 전달 (ShowDialog 결과 true)
            CloseAction?.Invoke(true);
        }

        public void Cancel()
        {
            CloseAction?.Invoke(false);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}