using LSS_prototype.User_Page;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LSS_prototype.Patient_Page
{
    /// <summary>
    /// Patient.xaml에 대한 상호 작용 논리
    /// 2026-02-09 서민지
    /// </summary>
    public partial class Patient : UserControl
    {
        private string _lastSearchText = string.Empty;
        public Patient()
        {
            InitializeComponent();
            Unloaded += (s, e) => (DataContext as PatientListViewModel)?.Dispose(); // 사용자 입력 감지 타이머 종료 ( 자원 관리 차 ) 
            txtSearch.TextChanged += OnSearchTextChanged;
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            MainPage.Instance.NavigateTo(new User());
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
           CheckBox cb = sender as CheckBox;

            // 사용자가 클릭해서 체크가 된 상태일 때만 화면 전환
            if (cb.IsChecked == false)
            {
                MainPage.Instance.NavigateTo(new EmrPatient());
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            //마지막 값 비교 이유
            //SEARCH 텍스트박스에서 사용자 행을 클릭하는 순간, 해당 함수가 실행되면서, 선택 상태가 풀리는 버그를 잡기 위해 
            //0226 박한용
            string current = txtSearch.Text;
            if (current == _lastSearchText) return;

            _lastSearchText = current;
            if (DataContext is PatientListViewModel vm)
                vm.OnSearchTextChanged(current);
        }
    }
}
