using System.Windows;
using System.Windows.Controls;

namespace LSS_prototype.User_Page
{
    public partial class User : UserControl
    {
        private string _lastSearchText = string.Empty;

        public User()
        {
            InitializeComponent();
            Unloaded += (s, e) => (DataContext as UserViewModel)?.Dispose(); // 사용자 입력 감지 타이머 종료 ( 자원 관리 차 ) 
            txtSearch.TextChanged += OnSearchTextChanged;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }


        private void Home_Click(object sender, RoutedEventArgs e)
        {
            MainPage.Instance.NavigateTo(new Patient_Page.Patient());
        }


        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            //마지막 값 비교 이유
            //SEARCH 텍스트박스에서 사용자 행을 클릭하는 순간, 해당 함수가 실행되면서, 선택 상태가 풀리는 버그를 잡기 위해 
            //0226 박한용
           string current = txtSearch.Text;
            if (current == _lastSearchText) return;  

            _lastSearchText = current;
            if (DataContext is UserViewModel vm)
                vm.OnSearchTextChanged(current);
        }



    }
}