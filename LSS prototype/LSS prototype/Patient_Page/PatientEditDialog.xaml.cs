using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace LSS_prototype.Patient_Page
{
    public partial class PatientEditDialog : Window
    {
        public PatientEditDialog()
        {
            InitializeComponent();
            // ✅ 세션 모니터 등록
            Loaded += (s, e) => App.ActivityMonitor?.RegisterWindow(this);
            this.PreviewMouseDown += Window_PreviewMouseDown;
        }

        private void DpBirthDate_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DatePicker datePicker)
            {
                datePicker.IsDropDownOpen = true;
                if (Mouse.Captured is DatePickerTextBox)
                    Mouse.Capture(null);
            }
        }

        private async void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is PatientEditViewModel vm)
            {
                if (vm.KeypadVm == null)
                    return;

                if (e.OriginalSource is DependencyObject source)
                {
                    if (FindParent<Keypad>(source) != null)
                        return;
                }

                if (vm.IsKeypadOpen)
                {
                    if (!await vm.KeypadVm.ValidateInput())
                        return;
                    vm.KeypadVm.ConfirmCommand.Execute(null);
                }

                if (vm.IsCodeKeypadOpen)
                {
                    if (!await vm.KeypadVm.ValidateInput())
                        return;
                    vm.KeypadVm.ConfirmCommand.Execute(null);
                }
            }
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }
}