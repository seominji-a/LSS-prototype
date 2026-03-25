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
        private PatientModel _selectedPatient;

        public ImageReview(PatientModel patient)
        {
            InitializeComponent();
            _selectedPatient = patient;
            DataContext = new ImageReviewViewModel(patient);
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
            MainPage.Instance.NavigateTo(new Scan(_selectedPatient));
        }

        private void VideoReviewButton_Click(object sender, RoutedEventArgs e)
        {
            MainPage.Instance.NavigateTo(new VideoReview(_selectedPatient));
        }
    }
}