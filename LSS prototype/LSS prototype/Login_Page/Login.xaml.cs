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
                vm?.LoadAdminIds();
            };
        }

        // ── EXIT 버튼 ──
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // ── 캡스락 체크 (GotKeyboardFocus + PreviewKeyUp 공용) ──
        private async void PasswordBox_CheckCaps(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = DataContext as LoginViewModel;
                if (vm != null)
                    vm.UpdateAdminModeVisibilityByUserId();

                // 캡스락 체크 — 포커스 진입 즉시
                CapsLockWarning.Visibility =
                    (Console.CapsLock && txtPassword.IsKeyboardFocusWithin)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
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