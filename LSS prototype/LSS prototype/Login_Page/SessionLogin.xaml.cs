using System;
using System.Windows;
using System.Windows.Input;

namespace LSS_prototype.Login_Page
{
    public partial class SessionLogin : Window
    {
        public SessionLogin()
        {
            InitializeComponent();
        }

        // ── 캡스락 체크 ① : PasswordBox 포커스 진입 시 ──
        private async void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                CapsLockWarning.Visibility =
                    Console.CapsLock ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ── 캡스락 체크 ② : 키를 뗄 때마다 (PreviewKeyUp = OS 반전 완료 후) ──
        private async void PasswordBox_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                CapsLockWarning.Visibility =
                    Console.CapsLock ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
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
