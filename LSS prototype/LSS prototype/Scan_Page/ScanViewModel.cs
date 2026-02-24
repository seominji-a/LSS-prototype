using System.Windows.Input;

namespace LSS_prototype.Scan_Page
{
    public class ScanViewModel
    {
        public ICommand NavigatePatientCommand { get; private set; }

        public ScanViewModel()
        {
            NavigatePatientCommand = new RelayCommand(NavigateToPatient);

        }

        private void NavigateToPatient()
        {
            MainPage.Instance.NavigateTo(new Patient_Page.Patient());
        }
    }
}