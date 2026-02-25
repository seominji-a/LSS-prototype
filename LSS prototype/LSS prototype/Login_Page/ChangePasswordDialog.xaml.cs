using System;
using System.Windows;

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

                await vm.SaveAsync(NewPasswordBox.Password, ConfirmPasswordBox.Password);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "Save_Click function Check ( ChangePasswordDialog )");
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
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message + "Cancel_Click function Check ( ChangePasswordDialog ) ");
            }
            
        }
    }
}