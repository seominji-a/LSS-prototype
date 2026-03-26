using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Data.SQLite;
using LSS_prototype.DB_CRUD;
using System.Windows.Threading;

namespace LSS_prototype.Login_Page
{
    public partial class Login : Window
    {
        public Login()
        {
            InitializeComponent();

            var vm = DataContext as LoginViewModel;

            if (vm != null)
            {
                vm.FocusUserIdAction = () =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UserId.Focus();
                        Keyboard.Focus(UserId);
                    }), DispatcherPriority.Input);
                };
            }

            Loaded += async (s, e) =>
            {
                var db = new DB_Manager();
                await db.InitDB();
                await vm?.LoadAdminIds();
            };
        }

        // ── EXIT 버튼 ──
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // ── 캡스락 체크 ① : PasswordBox 포커스 진입 시 ──
        // GotKeyboardFocus 이벤트에 연결
        // → 포커스가 들어온 시점의 CapsLock 상태를 즉시 읽어서 경고 표시
        private async void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                // Admin 체크박스 표시 여부도 이 시점에 같이 갱신
                var vm = DataContext as LoginViewModel;
                if (vm != null)
                    vm.UpdateAdminModeVisibilityByUserId();

                // 포커스 들어온 순간의 CapsLock 상태로 경고 표시/숨김
                CapsLockWarning.Visibility =
                    Console.CapsLock ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        // ── 캡스락 체크 ② : 키를 뗄 때마다 ──
        // PreviewKeyUp 이벤트에 연결
        // PreviewKeyDown(누를 때)으로 하면 OS가 CapsLock 상태를 반전시키는 타이밍과
        // 어긋나서 2~3번 눌러야 정상 작동하는 현상이 생김
        // PreviewKeyUp(뗄 때)은 OS가 CapsLock 반전을 이미 완료한 이후이므로
        // Console.CapsLock을 그냥 읽으면 항상 정확함 (!반전 트릭 불필요)
        private async void PasswordBox_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                // 키를 뗀 시점 = OS가 CapsLock 상태 반전 완료 후
                // → Console.CapsLock 그대로 읽으면 정확
                CapsLockWarning.Visibility =
                    Console.CapsLock ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        // ── 포커스 떠날 때 경고 숨김 ──
        private void PasswordBox_HideCaps(object sender, RoutedEventArgs e)
        {
            CapsLockWarning.Visibility = Visibility.Collapsed;
        }


        // 누르는 동안 비밀번호 보임
        private void BtnShowPassword_MouseDown(object sender, MouseButtonEventArgs e)
        {
            txtPasswordVisible.Text = txtPassword.Password;
            txtPassword.Visibility = Visibility.Collapsed;
            txtPasswordVisible.Visibility = Visibility.Visible;
        }

        // 떼면 다시 숨김
        private void BtnShowPassword_MouseUp(object sender, MouseButtonEventArgs e)
        {
            txtPassword.Visibility = Visibility.Visible;
            txtPasswordVisible.Visibility = Visibility.Collapsed;
        }



    }
}