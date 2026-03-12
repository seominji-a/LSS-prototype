using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace LSS_prototype.Login_Page
{
    public partial class ChangePasswordDialog : Window
    {
        public ChangePasswordDialog(ChangePasswordModelView vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseAction = ok =>
            {
                DialogResult = ok;
                Close();
            };
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = DataContext as ChangePasswordModelView;
                if (vm == null) return;
                vm.Save(NewPasswordBox.Password, ConfirmPasswordBox.Password);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = DataContext as ChangePasswordModelView;
                if (vm == null)
                {
                    DialogResult = false;
                    Close();
                    return;
                }
                vm.Cancel();
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        private void NewPasswordBox_CheckCaps(object sender, RoutedEventArgs e)
        {
            CapsLockWarningNew.Visibility = Keyboard.IsKeyToggled(Key.CapsLock)? Visibility.Visible : Visibility.Collapsed;
        }

        private void ConfirmPasswordBox_CheckCaps(object sender, RoutedEventArgs e)
        {
            CapsLockWarningConfirm.Visibility = Keyboard.IsKeyToggled(Key.CapsLock) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NewPasswordBox_HideCaps(object sender, RoutedEventArgs e)
        {
            CapsLockWarningNew.Visibility = Visibility.Collapsed;
        }

        private void ConfirmPasswordBox_HideCaps(object sender, RoutedEventArgs e)
        {
            CapsLockWarningConfirm.Visibility = Visibility.Collapsed;
        }

        private void LoginIdBox_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoginIdBox.Focus();

                    // 커서를 텍스트 맨 뒤로 이동 (선택 없이)
                    LoginIdBox.SelectionStart = LoginIdBox.Text.Length;
                    LoginIdBox.SelectionLength = 0;

                }), DispatcherPriority.Input);
            }
            catch(Exception ex)
            {
                Common.WriteLog(ex);
            }
 
        }
    }
}