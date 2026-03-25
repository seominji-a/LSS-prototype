using LSS_prototype.Patient_Page;
using LSS_prototype.Scan_Page;
using LSS_prototype.VideoReview_Page;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LSS_prototype.ImageReview_Page
{
    public class ImageReviewViewModel
    {
        public ObservableCollection<ImageItem> Images { get; } = new ObservableCollection<ImageItem>();

        public PatientModel SelectedPatient { get; }

        public ICommand NavigatePatientCommand { get; private set; }

        public ICommand NavigateScanCommand { get; private set; }

        public ICommand NavigateVideoReviewCommand { get; private set; }

        public ImageReviewViewModel(PatientModel patient)
        {
            SelectedPatient = patient;
            NavigatePatientCommand = new RelayCommand(_ => NavigateToPatient());
            NavigateScanCommand = new RelayCommand(_ => NavigateToScan());
            NavigateVideoReviewCommand = new RelayCommand(_ => NavigateToVideoReview());


            // 임시 테스트 데이터
            Images.Add(new ImageItem
            {
                Thumbnail = CreateImage(@"C:\Temp\test.jpg"),
                Date = "2026-03-23"
            });

            Images.Add(new ImageItem
            {
                Thumbnail = CreateImage(@"C:\Temp\test2.jpg"),
                Date = "2026-03-23"
            });
        }

        private ImageSource CreateImage(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private void NavigateToPatient() =>
       MainPage.Instance.NavigateTo(new Patient_Page.Patient());

        private void NavigateToScan() =>
            MainPage.Instance.NavigateTo(new Scan(SelectedPatient));

        private void NavigateToVideoReview() =>
            MainPage.Instance.NavigateTo(new VideoReview(SelectedPatient));
    }

    public class ImageItem
    {
        public ImageSource Thumbnail { get; set; }
        public string Name { get; set; }
        public string Date { get; set; }
    }
}