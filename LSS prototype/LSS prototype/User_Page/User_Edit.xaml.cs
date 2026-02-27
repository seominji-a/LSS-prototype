using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LSS_prototype.User_Page
{
    public partial class User_Edit : UserControl
    {
        public User_Edit()
        {
            InitializeComponent();
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is User_EditViewModel vm)
                vm.ExecuteSubmit(txtNewPassword.Password, txtConfirmPassword.Password);
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
