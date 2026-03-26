using System;
using System.Windows;
using System.Windows.Input;

namespace LSS_prototype.User_Page
{
    public partial class User_Add : Window
    {
        public User_Add()
        {
            InitializeComponent();
            Loaded += (s, e) => App.ActivityMonitor?.RegisterWindow(this);
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is User_AddViewModel vm)
                await vm.ExecuteSubmit(txtPassword.Password, txtConfirmPassword.Password);
        }

        // ── CapsLock ──
        private async void NewPasswordBox_CheckCaps(object sender, RoutedEventArgs e)
        {
            try
            {
                CapsLockWarningNew.Visibility =
                    Console.CapsLock ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async void NewPasswordBox_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                CapsLockWarningNew.Visibility =
                    Console.CapsLock ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private void NewPasswordBox_HideCaps(object sender, RoutedEventArgs e)
        {
            CapsLockWarningNew.Visibility = Visibility.Collapsed;
        }

        private async void ConfirmPasswordBox_CheckCaps(object sender, RoutedEventArgs e)
        {
            try
            {
                CapsLockWarningConfirm.Visibility =
                    Console.CapsLock ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async void ConfirmPasswordBox_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                CapsLockWarningConfirm.Visibility =
                    Console.CapsLock ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private void ConfirmPasswordBox_HideCaps(object sender, RoutedEventArgs e)
        {
            CapsLockWarningConfirm.Visibility = Visibility.Collapsed;
        }

        // ── 눈 버튼: PASSWORD ──
        private void BtnShowPw_MouseDown(object sender, MouseButtonEventArgs e)
        {
            txtPasswordVisible.Text = txtPassword.Password;
            txtPassword.Visibility = Visibility.Collapsed;
            txtPasswordVisible.Visibility = Visibility.Visible;
        }

        private void BtnShowPw_MouseUp(object sender, MouseButtonEventArgs e)
        {
            txtPassword.Visibility = Visibility.Visible;
            txtPasswordVisible.Visibility = Visibility.Collapsed;
        }

        // ── 눈 버튼: CONFIRM PASSWORD ──
        private void BtnShowConfirmPw_MouseDown(object sender, MouseButtonEventArgs e)
        {
            txtConfirmPasswordVisible.Text = txtConfirmPassword.Password;
            txtConfirmPassword.Visibility = Visibility.Collapsed;
            txtConfirmPasswordVisible.Visibility = Visibility.Visible;
        }

        private void BtnShowConfirmPw_MouseUp(object sender, MouseButtonEventArgs e)
        {
            txtConfirmPassword.Visibility = Visibility.Visible;
            txtConfirmPasswordVisible.Visibility = Visibility.Collapsed;
        }
    }
}