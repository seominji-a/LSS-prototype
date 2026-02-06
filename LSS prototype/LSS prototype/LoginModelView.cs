using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace LSS_prototype
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        // 1. 내부 저장 변수 (Field)
        private string _userId;

        // 버튼과 연결될 명령 객체
        public ICommand LoginCommand { get; }

        // 2. 화면과 연결될 속성 (Property)
        public string UserId
        {
            get => _userId;
            set
            {
                _userId = value;
                // 값이 바뀔 때마다 "UserId가 바뀌었어!"라고 화면에 알려줌
                OnPropertyChanged();
            }
        }

        public LoginViewModel()
        {
            // LoginCommand가 실행되면 ExecuteLogin 함수를 실행해라!
            LoginCommand = new RelayCommand(ExecuteLogin);
        }

        // 실제 로그인 로직이 들어갈 함수
        private void ExecuteLogin(object parameter)
        {
            // 1. PasswordBox(택배 박스)를 받아서 실제 PasswordBox 객체로 변신시킵니다.
            var passwordBox = parameter as System.Windows.Controls.PasswordBox;

            // 2. PasswordBox 안에 들어있는 비밀번호를 꺼냅니다.
            string password = passwordBox?.Password;

            // 3. ViewModel이 이미 가지고 있는 UserId와 방금 꺼낸 password를 메시지 박스로 출력!
            // \n 은 줄바꿈을 의미합니다.
            System.Windows.MessageBox.Show($"[ViewModel 확인]\n아이디: {UserId}\n비밀번호: {password}");
        }

        // --- 필수 인터페이스 구현 (이건 그냥 공식처럼 복사해서 쓰시면 돼요) ---
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}