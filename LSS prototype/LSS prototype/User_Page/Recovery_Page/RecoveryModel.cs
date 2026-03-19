public class RecoveryModel : System.ComponentModel.INotifyPropertyChanged
{
    public int DeleteId { get; set; }
    public string DeletedBy { get; set; }
    public string DeletedAt { get; set; }
    public string FileType { get; set; }
    public string ImagePath { get; set; }
    public string AviPath { get; set; }
    public string DicomPath { get; set; }
    public int PatientCode { get; set; }
    public string RecoveredAt { get; set; }
    public string PatientName { get; set; }


    private string _isRecovered;
    public string IsRecovered
    {
        get => _isRecovered;
        set { _isRecovered = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCheckable)); }
    }

    private string _isForceDeleted;
    public string IsForceDeleted
    {
        get => _isForceDeleted;
        set { _isForceDeleted = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCheckable)); }
    }

    private string _remainText;
    public string RemainText
    {
        get => _remainText;
        set { _remainText = value; OnPropertyChanged(); }
    }

    private bool _isExpired;
    public bool IsExpired
    {
        get => _isExpired;
        set { _isExpired = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCheckable)); }
    }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set { _isChecked = value; OnPropertyChanged(); }
    }

    // 체크박스 활성화 조건
    public bool IsCheckable
    {
        get
        {
            if (IsExpired) return false;
            if (IsRecovered == "Y") return false;
            if (IsForceDeleted == "Y") return false;
            return true;
        }
    }

    public string DisplayName => PatientName?.Replace("^", " ") ?? "";

    public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}