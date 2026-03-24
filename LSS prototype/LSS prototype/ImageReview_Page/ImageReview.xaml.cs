using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using LSS_prototype.Patient_Page;
using LSS_prototype.Scan_Page;
using LSS_prototype.VideoReview_Page;

namespace LSS_prototype.ImageReview_Page
{
    public partial class ImageReview : UserControl
    {
        private bool _navOpen = false;

        public ImageReview()
        {
            InitializeComponent();
            DataContext = new ImageReviewViewModel();
        }

        private void ToggleNav_Click(object sender, RoutedEventArgs e)
        {
            ((Storyboard)Resources[_navOpen ? "NavOut" : "NavIn"]).Begin();
            _navOpen = !_navOpen;
            ToggleBtn.Content = _navOpen ? "❮" : "❯";
        }

        private void PatientButton_Click(object sender, RoutedEventArgs e)
            => MainPage.Instance.NavigateTo(new Patient());

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            // selectedPatient가 있으면 그걸 넘기고,
            // 없으면 임시 환자 전달 구조를 따로 맞춰야 함
            // 현재는 예시
            // MainPage.Instance.NavigateTo(new Scan(selectedPatient));
        }

        private void VideoReviewButton_Click(object sender, RoutedEventArgs e)
        {
            // 실제 VideoReview UserControl 이름에 맞게 수정
            // MainPage.Instance.NavigateTo(new VideoReview());
        }
    }
}