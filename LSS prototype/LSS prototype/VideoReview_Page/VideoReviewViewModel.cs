using LSS_prototype.Patient_Page;
using OpenCvSharp;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace LSS_prototype.VideoReview_Page
{
    public class VideoReviewViewModel
    {
        public ObservableCollection<VideoItem> Videos { get; } = new ObservableCollection<VideoItem>();
        public PatientModel SelectedPatient { get; }

        public VideoReviewViewModel(PatientModel patient)
        {
            SelectedPatient = patient;

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
    }

    public class VideoItem
    {
        public ImageSource Thumbnail { get; set; }
        public string Name { get; set; }
        public string Date { get; set; }
    }
}