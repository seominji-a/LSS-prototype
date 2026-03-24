using System.Windows;
using System.Windows.Input;

namespace LSS_prototype.User_Page
{
    public partial class User_Edit : Window
    {
        public User_Edit()
        {
            InitializeComponent();
            Loaded += (s, e) => App.ActivityMonitor?.RegisterWindow(this);
        }

        private async void Submit_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is User_EditViewModel vm)
                await vm.ExecuteSubmit(txtNewPassword.Password, txtConfirmPassword.Password);
        }

        // ── CapsLock ──
        private void NewPasswordBox_CheckCaps(object sender, RoutedEventArgs e)
        {
            CapsLockWarningNew.Visibility = Keyboard.IsKeyToggled(Key.CapsLock)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NewPasswordBox_HideCaps(object sender, RoutedEventArgs e)
        {
            CapsLockWarningNew.Visibility = Visibility.Collapsed;
        }

        private void ConfirmPasswordBox_CheckCaps(object sender, RoutedEventArgs e)
        {
            CapsLockWarningConfirm.Visibility = Keyboard.IsKeyToggled(Key.CapsLock)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ConfirmPasswordBox_HideCaps(object sender, RoutedEventArgs e)
        {
            CapsLockWarningConfirm.Visibility = Visibility.Collapsed;
        }

        // ── 눈 버튼: NEW PASSWORD ──
        private void BtnShowNewPw_MouseDown(object sender, MouseButtonEventArgs e)
        {
            txtNewPasswordVisible.Text = txtNewPassword.Password;
            txtNewPassword.Visibility = Visibility.Collapsed;
            txtNewPasswordVisible.Visibility = Visibility.Visible;
        }

        private void BtnShowNewPw_MouseUp(object sender, MouseButtonEventArgs e)
        {
            txtNewPassword.Visibility = Visibility.Visible;
            txtNewPasswordVisible.Visibility = Visibility.Collapsed;
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