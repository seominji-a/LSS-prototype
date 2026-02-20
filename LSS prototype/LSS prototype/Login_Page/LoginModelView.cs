using LSS_prototype.Auth;
using LSS_prototype.DB_CRUD;
using LSS_prototype.Patient_Page;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LSS_prototype.Login_Page
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
            //LoginCommand = new RelayCommand(ExecuteLogin);
           LoginCommand = new AsyncRelayCommand(async (param) => await ExecuteLogin(param));
        }

        // 실제 로그인 로직이 들어갈 함수
        private async Task ExecuteLogin(object parameter)
        {
            var passwordBox = parameter as PasswordBox;
            string password = passwordBox?.Password; // 패스워드박스는 특성상 ID 처럼 바인딩이 UI단에서 바로안됨.
            string roleCode = string.Empty;
            DB_Manager dbManager = new DB_Manager();
            try
            {
                if (dbManager.Login_check(UserId, password, out roleCode))
                {
                    AuthToken.SignIn(UserId, roleCode);   // 토큰/세션 관리 시작

                    //  세션 복원 확인
                    if (SessionStateManager.IsSessionSuspended)
                    {
                        // 이전 세션 복원
                        var msg = new CustomMessageWindow(
                            "이전 작업 화면을 복원합니다.",
                            CustomMessageWindow.MessageBoxType.AutoClose,
                            1);
                        await msg.ShowAsync();

                        //  숨겨뒀던 창들 복원
                        SessionStateManager.RestoreSession();

                        // 로그인 창 닫기
                        Application.Current.Windows.OfType<Login>().FirstOrDefault()?.Close();

                        //  세션 모니터링 재시작 ( 추후 테스트 시 이 부분 주석 풀기 )
                        //App.ActivityMonitor.Start(Application.Current.MainWindow);
                    }
                    else
                    {
                        // 새로운 로그인 (기존 로직)
                        var msg = new CustomMessageWindow(
                            "로그인 성공",
                            CustomMessageWindow.MessageBoxType.AutoClose,
                            1);
                        await msg.ShowAsync();

                        Patient patient = new Patient();
                        patient.Show();
                        //App.ActivityMonitor.Start(patient);

                        Application.Current.Windows.OfType<Login>().FirstOrDefault()?.Close();
                    }
                }
                else
                {

                    var msg = new CustomMessageWindow(
                        "아이디 또는 비밀번호가 올바르지 않습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose,
                        1); // 1초 자동 닫힘 추천 (OK로 하고 싶으면 Ok로 바꿔도 됨)

                    await msg.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "ExecuteLogin Function Check");
            }
           
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}