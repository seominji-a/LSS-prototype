using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace LSS_prototype
{
    public class LoginViewModel : INotifyPropertyChanged
    {

        private string _userId;

        // 버튼과 연결될 객체
        public ICommand LoginCommand { get; }


        public string UserId
        {
            get => _userId;
            set
            {
                _userId = value; //UI단에서 ID에 값을 넣을때 마다 반영
                OnPropertyChanged();
            }
        }

        public LoginViewModel()
        {
            // LoginCommand가 실행되면 ExecuteLogin 함수를 실행
            LoginCommand = new RelayCommand(ExecuteLogin);
        }

        // 실제 로그인 로직이 들어갈 함수
        private void ExecuteLogin(object parameter)
        {
            var passwordBox = parameter as System.Windows.Controls.PasswordBox;
            string password = passwordBox?.Password; // 패스워드박스는 특성상 ID 처럼 바인딩이 UI단에서 바로안됨.

            DB_Manager dbManager = new DB_Manager();

            if (dbManager.Login_check(UserId, password))
            {
                MessageBox.Show("로그인 성공!", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                // 메인 화면 띄우기
                MainWindow mainWin = new MainWindow();
                mainWin.Show();

                Application.Current.Windows.OfType<Login>().FirstOrDefault()?.Close();
            }
            else
            {
                MessageBox.Show("아이디 또는 비밀번호가 올바르지 않습니다.", "로그인 실패",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}