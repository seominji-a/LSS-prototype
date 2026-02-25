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

        public async Task SaveAsync(string newPw, string confirmPw, string id)
        {
            //0225 기준 검증함수 구현완료 테스트 편의상 잠시 주석 처리 추후 정식 테스트 시 주석 풀어서 진행
            // 작성자 박한용
            /*string error = DB_Manager.ValidatePassword(newPw);
            if (error != null)
            {
                await CustomMessageWindow.ShowAsync(
                    error,
                    CustomMessageWindow.MessageBoxType.AutoClose,
                    2,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }*/

            if (newPw != confirmPw)
            {
                await CustomMessageWindow.ShowAsync(
                    "비밀번호가 일치하지 않습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose,
                    2,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }

            var db = new DB_Manager();
            bool success_flag = db.UpdatePassword(id,newPw);

            if (!success_flag)
            {
                await CustomMessageWindow.ShowAsync(
                    "비밀번호 변경에 실패했습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose,
                    2,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }

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