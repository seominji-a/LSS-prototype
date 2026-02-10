using System.Windows.Input;

namespace LSS_prototype.User_Page
{
    public class UserViewModel
    {
        public ICommand AddUserCommand { get; }
        private readonly IDialogService _dialogService;

        public UserViewModel()
        {
            _dialogService = new Dialog();
            AddUserCommand = new RelayCommand(ExecuteAddUser);
        }

        private void ExecuteAddUser(object parameter)
        {
            var vm = new User_AddViewModel();
            var result = _dialogService.ShowDialog(vm);
        }


    }
}
