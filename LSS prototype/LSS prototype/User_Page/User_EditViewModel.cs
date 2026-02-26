using LSS_prototype.DB_CRUD;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LSS_prototype.User_Page
{
    public class User_EditViewModel : INotifyPropertyChanged
    {
        public string UserName { get; }
        public string LoginId { get; }
        public Action<bool?> CloseAction { get; set; }
        public ICommand CancelCommand { get; }


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


        public User_EditViewModel(UserModel user)
        {
            UserName = user.UserName;
            LoginId = user.LoginId;
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false));
        }

        public void ExecuteSubmit(string newPassword, string confirmPassword)
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

            /*// 3. 유효성 검사 ( 테스트 기간동안 잠시 주석 ) 
            string error = DB_Manager.ValidatePassword(newPassword);
            if (error != null)
            {
                CustomMessageWindow.Show(error,
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }*/

            // 4. DB 업데이트
            try
            {
                var db = new DB_Manager();
                bool success = db.UpdatePassword(LoginId, newPassword);
                if (success)
                {
                    CustomMessageWindow.Show("비밀번호가 변경되었습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Info);
                    CloseAction?.Invoke(true);
                }
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

    }
}
