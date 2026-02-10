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
            try
            {
                if (string.IsNullOrWhiteSpace(UserID) ||
                    string.IsNullOrWhiteSpace(UserName) ||
                    string.IsNullOrWhiteSpace(Role) ||
                    string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("필수 입력값이 비어있습니다.", "확인",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    bool sucess = _dbManager.InsertUser(UserID.Trim(), UserName.Trim(), Role.Trim(), password);
                    if (sucess) MessageBox.Show("사용자 정보 ADD 성공");
                    CloseAction?.Invoke(true);
                }
                catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Constraint)
                {
                    // UNIQUE 제약(예: LOGIN_ID UNIQUE) 걸렸을 때 흔히 이쪽으로 옴
                    MessageBox.Show("이미 존재하는 아이디이거나 제약조건 오류입니다.\n" + ex.Message, "오류",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"저장 중 오류가 발생했습니다.\n{ex.Message}", "오류",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                // CloseAction?.Invoke(true); 창닫는건 잠시 보류 
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 중 오류가 발생했습니다.\n{ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}