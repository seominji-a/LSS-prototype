using LSS_prototype.DB_CRUD;
using System;
using System.ComponentModel;
using System.Data.SQLite;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace LSS_prototype.User_Page
{
    public class User_AddViewModel : INotifyPropertyChanged
    {
        public Action<bool?> CloseAction { get; set; }
        public ICommand CancelCommand { get; }


        private string _userName; 
        public string UserName
        {
            get => _userName;
            set { _userName = value; OnPropertyChanged(); }
        }

        private string _userID;
        public string UserID
        {
            get => _userID;
            set { _userID = value; OnPropertyChanged(); }
        }

        private string _role = string.Empty;
        public string Role
        {
            get => _role;
            set { _role = value; OnPropertyChanged(); }
        }

        public User_AddViewModel()
        {
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false));
        }

 

        public async Task ExecuteSubmit(string password, string confirmPassword)
        {
            // 1. 유효성 검사
            if (string.IsNullOrWhiteSpace(UserID) ||
                string.IsNullOrWhiteSpace(UserName) ||
                string.IsNullOrWhiteSpace(Role) ||
                string.IsNullOrEmpty(password))
            {
                await CustomMessageWindow.ShowAsync("필수 입력값이 비어있습니다.",
                            CustomMessageWindow.MessageBoxType.Ok, 1,
                            CustomMessageWindow.MessageIconType.Warning);
                return;
            }
            // 2. 검증 함수
            string error = DB_Manager.ValidatePassword(password);
            if (error != null)
            {
                await CustomMessageWindow.ShowAsync(
                    error,
                    CustomMessageWindow.MessageBoxType.Ok,
                    2,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }

            // 3. 사용자가 입력한 2가지 비밀번호 검사
            if (password != confirmPassword)
            {
                await CustomMessageWindow.ShowAsync(
                    "비밀번호가 일치하지 않습니다",
                    CustomMessageWindow.MessageBoxType.Ok,
                    2,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }

            // DB 작업
            try
            {
                var db = new DB_Manager();
                bool success = db.InsertUser(UserID.Trim(), UserName.Trim(), Role.Trim(), password);
                if (success)
                {
                    await CustomMessageWindow.ShowAsync("사용자 정보 추가 성공",
                            CustomMessageWindow.MessageBoxType.Ok, 1,
                            CustomMessageWindow.MessageIconType.Info);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        CloseAction?.Invoke(true);
                    });
                }
            }
            catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Constraint)
            {
                await CustomMessageWindow.ShowAsync("이미 사용중인 ID입니다.",
                    CustomMessageWindow.MessageBoxType.Ok, 2,
                    CustomMessageWindow.MessageIconType.Warning);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }

        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}