using LSS_prototype.User_Page;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            #if DEBUG // 배포 시 안보임, vs에서 실행하면 home 화면 보임 ( 테스트 차 ) 
                HomeButton.Visibility = Visibility.Visible;
            #else
                HomeButton.Visibility = Visibility.Collapsed;
            #endif

            Unloaded += (s, e) => (DataContext as PatientViewModel)?.Dispose(); // 사용자 입력 감지 타이머 종료 ( 자원 관리 차 ) 
            txtSearch.TextChanged += OnSearchTextChanged;
            var vm = new PatientViewModel();
            DataContext = vm;
            Loaded += async (s, e) => await vm.InitializeAsync();

        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            MainPage.Instance.NavigateTo(new User());
        }


        public void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            //마지막 값 비교 이유
            //SEARCH 텍스트박스에서 사용자 행을 클릭하는 순간, 해당 함수가 실행되면서, 선택 상태가 풀리는 버그를 잡기 위해 .
            //0226 박한용
            string current = txtSearch.Text;
            if (current == _lastSearchText) return;

            _lastSearchText = current;
            if (DataContext is PatientViewModel vm)
                vm.OnSearchTextChanged(current);
        }

        /// <summary>
        /// 우측 selectbox 클릭 시, 선택된 카드의 위치로 스크롤이 자동으로 이동 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectedPatientBorder_Click(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as PatientViewModel;
            if (vm?.SelectedPatient == null) return;
            PatientListBox.ScrollIntoView(vm.SelectedPatient);
        }
    }
}
