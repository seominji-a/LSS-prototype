using LSS_prototype.DB_CRUD;
using System;
using System.ComponentModel;
using System.Data.SQLite;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LSS_prototype.User_Page
{
    public class User_AddViewModel : INotifyPropertyChanged
    {
        public Action<bool?> CloseAction { get; set; }
        public ICommand SubmitCommand { get; }
        public ICommand CancelCommand { get; }
        private DB_Manager _dbManager;

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

        private string _role = "Physician";
        public string Role
        {
            get => _role;
            set { _role = value; OnPropertyChanged(); }
        }

        public User_AddViewModel()
        {
            SubmitCommand = new RelayCommand(ExecuteSubmit);
            CancelCommand = new RelayCommand(ExecuteCancel);
            _dbManager = new DB_Manager();
        }

        private void ExecuteCancel()  
        {
            CloseAction?.Invoke(false);
        }

        private void ExecuteSubmit(object parameter)
        {
            var pwBox = parameter as PasswordBox;
            string password = pwBox?.Password;

            // 유효성 검사
            if (string.IsNullOrWhiteSpace(UserID) ||
                string.IsNullOrWhiteSpace(UserName) ||
                string.IsNullOrWhiteSpace(Role) ||
                string.IsNullOrEmpty(password))
            {
                CustomMessageWindow.Show("필수 입력값이 비어있습니다.",
                            CustomMessageWindow.MessageBoxType.AutoClose, 1,
                            CustomMessageWindow.MessageIconType.Warning);
                return;
            }

            // DB 작업
            try
            {
                bool success = _dbManager.InsertUser(UserID.Trim(), UserName.Trim(), Role.Trim(), password);
                if (success)
                {
                    CustomMessageWindow.Show("사용자 정보 추가 성공",
                            CustomMessageWindow.MessageBoxType.AutoClose, 1,
                            CustomMessageWindow.MessageIconType.Info);
                    CloseAction?.Invoke(true);
                }
            }
            catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Constraint)
            {
                CustomMessageWindow.Show("이미 존재하는 아이디입니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 1,
                    CustomMessageWindow.MessageIconType.Warning);
            }
            catch (Exception ex)
            {
                CustomMessageWindow.Show($"저장 중 오류가 발생했습니다.\n{ex.Message}",
                    CustomMessageWindow.MessageBoxType.AutoClose, 1,
                    CustomMessageWindow.MessageIconType.Danger);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}