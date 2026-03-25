using System;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LSS_prototype.Patient_Page;

namespace LSS_prototype.ImageReview_Page
{
    public class ImageReviewViewModel
    {
        public ObservableCollection<ImageItem> Images { get; } = new ObservableCollection<ImageItem>();

        public PatientModel SelectedPatient { get; }

        public ImageReviewViewModel(PatientModel patient)
        {
            SelectedPatient = patient;

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
    }

    public class ImageItem
    {
        public ImageSource Thumbnail { get; set; }
        public string Name { get; set; }
        public string Date { get; set; }
    }
}