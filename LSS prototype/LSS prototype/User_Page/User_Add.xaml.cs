using System.Windows.Controls;


namespace LSS_prototype.User_Page
{
    /// <summary>
    /// User_Add.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class User_Add : UserControl
    {
        public User_Add()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is User_AddViewModel vm)
                vm.ExecuteSubmit(txtPassword.Password, txtConfirmPassword.Password); 
        }
    }
}
