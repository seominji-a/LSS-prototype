using LSS_prototype.DB_CRUD;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace LSS_prototype.Login_Page
{
    public class ChangePasswordModelView : INotifyPropertyChanged
    {
       
        private readonly string _firstId;  // 최초 로그인 ID 보관 (변경 불가)


        private string _loginId;
        public string LoginId
        {
            get => _loginId;
            set { _loginId = value; OnPropertyChanged(); }
        }

        private string _role = string.Empty;
        public string Role
        {
            get => _role;
            set { _role = value; OnPropertyChanged(); }
        }

        public Action<bool> CloseAction { get; set; }

        public ChangePasswordModelView(string loginId)
        {
            _firstId = loginId;  // 원본 저장
        }

        public void Save(string newPw, string confirmPw)
        {
            // 1. 유효성 검사
            if (string.IsNullOrWhiteSpace(LoginId) ||
                string.IsNullOrWhiteSpace(Role) ||
                string.IsNullOrEmpty(newPw) ||
                string.IsNullOrEmpty(confirmPw))
            {
                CustomMessageWindow.Show("필수 입력값이 비어있습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 1,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }

            // 2. 기존 ID와 동일한지 비교
            if (LoginId == _firstId)
            {
                CustomMessageWindow.Show("기존 ID와 동일한 ID로는 변경할 수 없습니다.",
                    CustomMessageWindow.MessageBoxType.Ok, 0,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }

            // 3. 비밀번호 유효성 검사
            string error = DB_Manager.ValidatePassword(newPw);
            if (error != null)
            {
                CustomMessageWindow.Show(error,
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }

            // 4. 비밀번호 일치 확인
            if (newPw != confirmPw)
            {
                CustomMessageWindow.Show("비밀번호가 일치하지 않습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }

            // 5. DB 저장
            var db = new DB_Manager();
            bool success_flag = db.UpdateCredential(_firstId, LoginId, newPw, Role.Trim()); // Role 추가
            

            if (!success_flag)
            {
                CustomMessageWindow.Show("비밀번호 변경에 실패했습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }

            // 6. 성공 
                CustomMessageWindow.Show("비밀번호가 변경되었습니다. 다시 로그인해주세요.",
                    CustomMessageWindow.MessageBoxType.Ok, 0,
                    CustomMessageWindow.MessageIconType.Info);

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