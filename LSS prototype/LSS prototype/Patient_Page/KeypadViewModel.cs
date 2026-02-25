using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LSS_prototype.Patient_Page
{
    public class KeypadViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private string _inputText = "";
        public string InputText
        {
            get => _inputText;
            set
            {
                _inputText = value;
                OnPropertyChanged();
            }
        }

        public DateTime? ResultDate { get; private set; }

        public ICommand NumberCommand { get; }
        public ICommand BackspaceCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public KeypadViewModel()
        {
            NumberCommand = new RelayCommand(param =>
            {
                if (param is string number)
                    AddNumber(number);
            });
            BackspaceCommand = new RelayCommand(RemoveLast);
            ConfirmCommand = new RelayCommand(Confirm);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void AddNumber(string number)
        {
            if (InputText.Length < 8)
                InputText += number;
        }

        private void RemoveLast()
        {
            if (InputText.Length > 0)
                InputText = InputText.Substring(0, InputText.Length - 1);
        }

        private void Confirm()
        {
            if (DateTime.TryParseExact(InputText, "yyyyMMdd",
                null,
                System.Globalization.DateTimeStyles.None,
                out DateTime date))
            {
                ResultDate = date;
                CloseRequested?.Invoke(true);
            }
        }

        private void Cancel()
        {
            CloseRequested?.Invoke(false);
        }

        public event Action<bool?> CloseRequested;
    }
}
