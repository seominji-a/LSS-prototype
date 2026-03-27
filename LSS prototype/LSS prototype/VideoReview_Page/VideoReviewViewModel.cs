using LSS_prototype.ImageReview_Page;
using LSS_prototype.Patient_Page;
using LSS_prototype.Scan_Page;
using OpenCvSharp;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LSS_prototype.VideoReview_Page
{
    public class VideoReviewViewModel
    {
        public ObservableCollection<VideoItem> Videos { get; } = new ObservableCollection<VideoItem>();
        public PatientModel SelectedPatient { get; }

        public ICommand NavigatePatientCommand { get; private set; }

        public ICommand NavigateScanCommand { get; private set; }

        public ICommand NavigateImageReviewCommand { get; private set; }

        private readonly string _emrcheck;
        private readonly string _studyId;

        public VideoReviewViewModel(PatientModel patient, string emrcheck, string studyid)
        {
            SelectedPatient = patient;
            _emrcheck = emrcheck;
            _studyId = studyid;

            NavigatePatientCommand = new RelayCommand(_ => NavigateToPatient());
            NavigateScanCommand = new RelayCommand(_ => NavigateToScan());
            NavigateImageReviewCommand = new RelayCommand(_ => NavigateToImageReview());
            

            Videos.Add(new VideoItem
            {
                Thumbnail = CreateVideoThumbnail(@"C:\Temp\test.avi"),
                Date = "2026-03-23"
            });

            Videos.Add(new VideoItem
            {
                Thumbnail = CreateVideoThumbnail(@"C:\Temp\test2.avi"),
                Date = "2026-03-23"
            });
        }

        private ImageSource CreateVideoThumbnail(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                using (var capture = new VideoCapture(path))
                using (var frame = new Mat())
                {
                    if (!capture.IsOpened())
                        return null;

                    if (!capture.Read(frame) || frame.Empty())
                        return null;

                    return ConvertMatToBitmapSource(frame);
                }
            }
            catch
            {
                return null;
            }
        }

        private BitmapSource ConvertMatToBitmapSource(Mat mat)
        {
            int stride = (int)mat.Step();
            int bufferSize = stride * mat.Height;

            return BitmapSource.Create(
                mat.Width,
                mat.Height,
                96, 96,
                PixelFormats.Bgr24,
                null,
                mat.Data,
                bufferSize,
                stride
            );
        }

        private void NavigateToPatient() =>
        MainPage.Instance.NavigateTo(new Patient_Page.Patient());

        private void NavigateToScan() =>
            MainPage.Instance.NavigateTo(new Scan(SelectedPatient, _emrcheck, _studyId));

        private void NavigateToImageReview() =>
            MainPage.Instance.NavigateTo(new ImageReview(SelectedPatient, _emrcheck, _studyId));

    }

    public class VideoItem
    {
        public ImageSource Thumbnail { get; set; }
        public string Name { get; set; }
        public string Date { get; set; }
    }
}