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

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = DataContext as ChangePasswordModelView;
                if (vm == null) return;
                await vm.Save(NewPasswordBox.Password, ConfirmPasswordBox.Password);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = DataContext as ChangePasswordModelView;
                if (vm == null) { DialogResult = false; Close(); return; }
                vm.Cancel();
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
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

        // ── 눈 버튼: NEW PASSWORD ──
        private void BtnShowNewPw_MouseDown(object sender, MouseButtonEventArgs e)
        {
            NewPasswordVisible.Text = NewPasswordBox.Password;
            NewPasswordBox.Visibility = Visibility.Collapsed;
            NewPasswordVisible.Visibility = Visibility.Visible;
        }

        private void BtnShowNewPw_MouseUp(object sender, MouseButtonEventArgs e)
        {
            NewPasswordBox.Visibility = Visibility.Visible;
            NewPasswordVisible.Visibility = Visibility.Collapsed;
        }

        // ── 눈 버튼: CONFIRM PASSWORD ──
        private void BtnShowConfirmPw_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ConfirmPasswordVisible.Text = ConfirmPasswordBox.Password;
            ConfirmPasswordBox.Visibility = Visibility.Collapsed;
            ConfirmPasswordVisible.Visibility = Visibility.Visible;
        }

        private void BtnShowConfirmPw_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ConfirmPasswordBox.Visibility = Visibility.Visible;
            ConfirmPasswordVisible.Visibility = Visibility.Collapsed;
        }

        // ── LoginIdBox 포커스 ──
        private async void LoginIdBox_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoginIdBox.Focus();
                    LoginIdBox.SelectionStart = LoginIdBox.Text.Length;
                    LoginIdBox.SelectionLength = 0;
                }), DispatcherPriority.Input);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }
    }
}