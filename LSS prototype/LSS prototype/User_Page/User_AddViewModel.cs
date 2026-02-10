using System;
using System.ComponentModel;
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
            CancelCommand = new RelayCommand(ExecuteCancel);  // ✅ 수정
        }

        private void ExecuteCancel(object parameter)  // ✅ parameter 추가
        {
            CloseAction?.Invoke(false);
        }

        private void ExecuteSubmit(object parameter)
        {
            var pwBox = parameter as PasswordBox;
            string password = pwBox?.Password;

            // 유효성 검사
            if (string.IsNullOrWhiteSpace(UserName) ||
                string.IsNullOrWhiteSpace(UserID) ||
                string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("필수 입력값을 확인하세요.", "확인",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (UserID.Length < 3)
            {
                MessageBox.Show("USER ID는 최소 3자 이상을 권장합니다.", "확인",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // TODO: DB 저장 로직

                CloseAction?.Invoke(true);
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