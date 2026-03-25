using FellowOakDicom.Imaging;
using LSS_prototype.Patient_Page;
using LSS_prototype.Scan_Page;
using LSS_prototype.VideoReview_Page;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LSS_prototype.ImageReview_Page
{
    public class ImageReviewViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<DateFolderItem> AvailableDates { get; } = new ObservableCollection<DateFolderItem>();

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

            LoadAvailableDates();
        }

        private void LoadAvailableDates()
        {
            AvailableDates.Clear();

            string patientFolder = GetPatientFolderPath();
            if (string.IsNullOrWhiteSpace(patientFolder) || !Directory.Exists(patientFolder))
                return;

            var dateDirs = Directory.GetDirectories(patientFolder)
                                    .OrderByDescending(x => x)
                                    .ToList();

            foreach (var dir in dateDirs)
            {
                string folderName = Path.GetFileName(dir);

                if (folderName.Length != 8 || !folderName.All(char.IsDigit))
                    continue;

                var dateItem = new DateFolderItem
                {
                    RawDate = folderName,
                    DisplayDate = FormatDate(folderName),
                    FolderPath = dir,
                    IsExpanded = false
                };

                LoadImagesForDate(dateItem);
                AvailableDates.Add(dateItem);
            }
        }

        private void LoadImagesForDate(DateFolderItem dateItem)
        {
            dateItem.Images.Clear();

            if (dateItem == null || !Directory.Exists(dateItem.FolderPath))
                return;

            var studyDirs = Directory.GetDirectories(dateItem.FolderPath)
                                     .OrderBy(x => x)
                                     .ToList();

            foreach (var studyDir in studyDirs)
            {
                string imageDir = Path.Combine(studyDir, "Image");
                if (!Directory.Exists(imageDir))
                    continue;

                var dcmFiles = Directory.GetFiles(imageDir, "*.dcm")
                                        .OrderBy(x => x)
                                        .ToList();

                foreach (var dcmPath in dcmFiles)
                {
                    dateItem.Images.Add(new ImageItem
                    {
                        Thumbnail = CreateDicomThumbnail(dcmPath),
                        Name = Path.GetFileNameWithoutExtension(dcmPath),
                        Date = dateItem.DisplayDate,
                        FilePath = dcmPath
                    });
                }
            }

            dateItem.OnPropertyChanged(nameof(DateFolderItem.ImageCountText));
        }

        private string GetPatientFolderPath()
        {
            if (SelectedPatient == null)
                return null;

            string baseDicomPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DICOM");
            string patientFolderName = string.Format("{0}_{1}", SelectedPatient.PatientName, SelectedPatient.PatientCode);

            return Path.Combine(baseDicomPath, patientFolderName);
        }

        private ImageSource CreateDicomThumbnail(string dcmPath)
        {
            try
            {
                var dicomImage = new DicomImage(dcmPath);

                using (var bitmap = dicomImage.RenderImage().AsClonedBitmap())
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Bmp);
                    ms.Position = 0;

                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();

                    return image;
                }
            }
            catch
            {
                return null;
            }
        }

        private string FormatDate(string rawDate)
        {
            if (string.IsNullOrWhiteSpace(rawDate) || rawDate.Length != 8)
                return rawDate;

            return rawDate.Substring(0, 4) + "-" +
                   rawDate.Substring(4, 2) + "-" +
                   rawDate.Substring(6, 2);
        }

        private void NavigateToPatient()
        {
            MainPage.Instance.NavigateTo(new Patient_Page.Patient());
        }

        private void NavigateToScan()
        {
            MainPage.Instance.NavigateTo(new Scan(SelectedPatient));
        }

        private void NavigateToVideoReview()
        {
            MainPage.Instance.NavigateTo(new VideoReview(SelectedPatient));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ImageItem
    {
        public ImageSource Thumbnail { get; set; }
        public string Name { get; set; }
        public string Date { get; set; }
        public string FilePath { get; set; }
    }

    public class DateFolderItem : INotifyPropertyChanged
    {
        public string RawDate { get; set; }
        public string DisplayDate { get; set; }
        public string FolderPath { get; set; }

        public ObservableCollection<ImageItem> Images { get; } = new ObservableCollection<ImageItem>();

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ImageCountText => $"{Images.Count}장";

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}