using FellowOakDicom.Imaging;
using LSS_prototype.Patient_Page;
using LSS_prototype.Scan_Page;
using LSS_prototype.VideoReview_Page;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        public ICommand ToggleSortOrderCommand { get; private set; }

        private ImageSource _selectedImage;
        public ImageSource SelectedImage
        {
            get => _selectedImage;
            set
            {
                if (_selectedImage != value)
                {
                    _selectedImage = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _selectedImageDateText;
        public string SelectedImageDateText
        {
            get => _selectedImageDateText;
            set
            {
                if (_selectedImageDateText != value)
                {
                    _selectedImageDateText = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _selectedImageIndex;
        public int SelectedImageIndex
        {
            get => _selectedImageIndex;
            set
            {
                if (_selectedImageIndex != value)
                {
                    _selectedImageIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedImageIndexText));
                }
            }
        }

        private int _totalImageCount;
        public int TotalImageCount
        {
            get => _totalImageCount;
            set
            {
                if (_totalImageCount != value)
                {
                    _totalImageCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedImageIndexText));
                }
            }
        }

        public string SelectedImageIndexText => TotalImageCount <= 0
            ? "0 / 0"
            : $"{SelectedImageIndex} / {TotalImageCount}";

        public ICommand SelectImageCommand { get; private set; }

        public ImageReviewViewModel(PatientModel patient)
        {
            SelectedPatient = patient;

            NavigatePatientCommand = new RelayCommand(_ => NavigateToPatient());
            NavigateScanCommand = new RelayCommand(_ => NavigateToScan());
            NavigateVideoReviewCommand = new RelayCommand(_ => NavigateToVideoReview());
            SelectImageCommand = new RelayCommand(param => SelectImage(param as ImageItem));
            ToggleSortOrderCommand = new RelayCommand(_ => ToggleSortOrder());

            LoadAvailableDates();
            UpdateTotalImageCount();
        }

        //폴더 경로의 파일명에서 로드 가능한 날짜 리스트 생성 함수
        private void LoadAvailableDates()
        {
            AvailableDates.Clear();

            string patientFolder = GetPatientFolderPath();
            if (string.IsNullOrWhiteSpace(patientFolder) || !Directory.Exists(patientFolder))
                return;

            var dateDirs = Directory.GetDirectories(patientFolder).ToList();

            foreach (var dir in dateDirs)
            {
                string folderName = Path.GetFileName(dir);

                if (folderName.Length != 8 || !folderName.All(char.IsDigit))
                    continue;

                DateTime parsedDate;
                if (!DateTime.TryParseExact(
                        folderName,
                        "yyyyMMdd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out parsedDate))
                {
                    continue;
                }

                var dateItem = new DateFolderItem
                {
                    RawDate = folderName,
                    DisplayDate = FormatDate(folderName),
                    SortDate = parsedDate,
                    FolderPath = dir,
                    IsExpanded = false
                };

                LoadImagesForDate(dateItem);
                AvailableDates.Add(dateItem);
            }

            ApplyDateSorting();
        }



        //로드 가능한 날짜의 이미지 로드
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

        //폴더 경로의 파일명 구하는 함수
        private string GetPatientFolderPath()
        {
            if (SelectedPatient == null)
                return null;

            string baseDicomPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DICOM");
            string patientFolderName = string.Format("{0}_{1}", SelectedPatient.PatientName, SelectedPatient.PatientCode);

            return Path.Combine(baseDicomPath, patientFolderName);
        }

        //dicom 썸네일 생성 함수
        private ImageSource CreateDicomThumbnail(string dcmPath)
        {
            try
            {
                var dicomImage = new DicomImage(dcmPath);
                var renderedImage = dicomImage.RenderImage();

                int width = renderedImage.Width;
                int height = renderedImage.Height;

                // WPFImageManager를 쓰지 않는 현재 구조에서는 raw byte[]로 받기
                byte[] pixels = renderedImage.As<byte[]>();

                // fo-dicom raw image는 보통 BGRA 32비트 기준으로 다루면 맞음
                int stride = width * 4;

                var bitmap = BitmapSource.Create(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    pixels,
                    stride);

                bitmap.Freeze();
                return bitmap;
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

        private void SelectImage(ImageItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FilePath) || !File.Exists(item.FilePath))
                return;

            ClearThumbnailSelection();
            item.IsSelected = true;

            SelectedImage = CreateDicomPreview(item.FilePath);
            SelectedImageDateText = $"촬영일자 : {item.Date}";
            SelectedImageIndex = GetFlatImageIndex(item);
        }

        private int GetFlatImageIndex(ImageItem selectedItem)
        {
            if (selectedItem == null)
                return 0;

            int index = 0;

            foreach (var date in AvailableDates)
            {
                foreach (var image in date.Images)
                {
                    index++;

                    if (image.FilePath == selectedItem.FilePath)
                        return index;
                }
            }

            return 0;
        }

        private void UpdateTotalImageCount()
        {
            TotalImageCount = AvailableDates.Sum(x => x.Images.Count);
        }

        private ImageSource CreateDicomPreview(string dcmPath)
        {
            try
            {
                var dicomImage = new DicomImage(dcmPath);
                var renderedImage = dicomImage.RenderImage();

                int width = renderedImage.Width;
                int height = renderedImage.Height;
                byte[] pixels = renderedImage.As<byte[]>();
                int stride = width * 4;

                var bitmap = BitmapSource.Create(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    pixels,
                    stride);

                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private bool _isLatestFirst = true;
        public bool IsLatestFirst
        {
            get => _isLatestFirst;
            set
            {
                if (_isLatestFirst != value)
                {
                    _isLatestFirst = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SortButtonText));
                }
            }
        }

        public string SortButtonText => IsLatestFirst ? "날짜순 ▼" : "날짜순 ▲";

        private void ToggleSortOrder()
        {
            IsLatestFirst = !IsLatestFirst;
            ApplyDateSorting();
        }

        private void ApplyDateSorting()
        {
            if (AvailableDates == null || AvailableDates.Count == 0)
                return;

            var sorted = IsLatestFirst
                ? AvailableDates.OrderByDescending(x => x.SortDate).ToList()
                : AvailableDates.OrderBy(x => x.SortDate).ToList();

            AvailableDates.Clear();
            foreach (var item in sorted)
            {
                AvailableDates.Add(item);
            }
        }

        //
        //네비게이션 관련
        //
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

        //선택된 썸네일 표시
        private void ClearThumbnailSelection()
        {
            foreach (var date in AvailableDates)
            {
                foreach (var image in date.Images)
                {
                    image.IsSelected = false;
                }
            }
        }
    }



    //리스트에 포함된 이미지 관련 정보
    public class ImageItem : INotifyPropertyChanged
    {
        private ImageSource _thumbnail;
        public ImageSource Thumbnail
        {
            get => _thumbnail;
            set
            {
                _thumbnail = value;
                OnPropertyChanged();
            }
        }

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        private string _date;
        public string Date
        {
            get => _date;
            set
            {
                _date = value;
                OnPropertyChanged();
            }
        }

        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged();
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    //존재하는 이미지의 촬영 날짜 정보
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

        //촬영 날짜에 존재하는 촬영 건수
        public string ImageCountText => $"{Images.Count}장";

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private DateTime _sortDate;
        public DateTime SortDate
        {
            get => _sortDate;
            set
            {
                _sortDate = value;
                OnPropertyChanged();
            }
        }


    }

}