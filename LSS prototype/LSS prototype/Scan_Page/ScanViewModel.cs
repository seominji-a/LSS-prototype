using System.Windows.Input;

namespace LSS_prototype.Scan_Page
{
    public class ScanViewModel
    {
        public ICommand NavigatePatientCommand { get; private set; }
        public ICommand LogoutCommand { get; }
        public ICommand ExitCommand { get; }

        public ScanViewModel()
        {
            NavigatePatientCommand = new RelayCommand(NavigateToPatient);
            LogoutCommand = new RelayCommand(Common.ExecuteLogout);
            ExitCommand = new RelayCommand(Common.ExcuteExit);

        }

        private void NavigateToPatient()
        {
            MainPage.Instance.NavigateTo(new Patient_Page.Patient());
        }
    }
}