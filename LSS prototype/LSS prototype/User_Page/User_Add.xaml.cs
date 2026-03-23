using System.Windows;
using System.Windows.Input;

namespace LSS_prototype.User_Page
{
    public partial class User_Add : Window
    {
        public User_Add()
        {
            InitializeComponent();
            // ✅ 세션 모니터 등록
            Loaded += (s, e) => App.ActivityMonitor?.RegisterWindow(this);
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is User_AddViewModel vm)
                await vm.ExecuteSubmit(txtPassword.Password, txtConfirmPassword.Password);
        }

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
    }
}