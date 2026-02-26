using LSS_prototype.DB_CRUD;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace LSS_prototype.User_Page
{

    /// <summary>
    /// INotifyPropertyChanged : UI에게 알릴 준비가 됐다는 I/F
    /// IDisposable : 자원정리 I/F
    /// </summary>
    public class UserViewModel : INotifyPropertyChanged, IDisposable
    {
        public ICommand AddUserCommand { get; }
        public ICommand EditUserCommand { get; }
        public ICommand DeleteUserCommand { get; }
        public ICommand SettingCommand { get; }
        public ICommand DefaultCommand { get; }
        

        private readonly SearchDebouncer _searchDebouncer;
        private readonly IDialogService _dialogService;

        private string _searchText;

        private ObservableCollection<UserModel> _users = new ObservableCollection<UserModel>();
        public ObservableCollection<UserModel> Users
        {
            get { return _users; }
            set
            {
                _users = value;
                OnPropertyChanged();
            }
        }
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();

                // 입력 바뀔 때마다 디바운서에 전달 → 0.5초 후 DB 검색
                _searchDebouncer.OnTextChanged(value);
            }
        }

        private UserModel _selectedUser;
        public UserModel SelectedUser
        {
            get { return _selectedUser; }
            set
            {
                _selectedUser = value;
                OnPropertyChanged();
            }
        }

        public UserViewModel()
        {
            _dialogService = new Dialog();
            AddUserCommand = new RelayCommand(ExecuteAddUser);
            EditUserCommand = new RelayCommand(ExecuteEditUser);  
            SettingCommand = new RelayCommand(ExecuteOpenSetting);
            DefaultCommand = new RelayCommand(ExecuteOpenDefault);
            DeleteUserCommand = new RelayCommand(ExecuteDeleteUser);
            _searchDebouncer = new SearchDebouncer(ExecuteSearch, delayMs: 500);

            LoadUsers();
        }
        private void ExecuteEditUser(object parameter)
        {
            if (SelectedUser == null)
            {
                CustomMessageWindow.Show("수정할 사용자를 선택해주세요.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 1,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }

            var vm = new User_EditViewModel(SelectedUser);
            var result = _dialogService.ShowDialog(vm);

            if (result == true)
                LoadUsers();
        }

        public void OnSearchTextChanged(string text)
        {
            _searchDebouncer.OnTextChanged(text);
        }

        private void ExecuteSearch(string keyword)
        {
            try
            {
                var repo = new DB_Manager();
                List<UserModel> data = string.IsNullOrWhiteSpace(keyword)
                    ? repo.GetAllUsers()
                    : repo.SearchUsers(keyword);

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    // 현재 선택된 유저 ID 기억
                    int? selectedId = SelectedUser?.UserId;

                    Users.Clear();
                    foreach (var user in data)
                        Users.Add(user);

                    // 같은 ID 가진 항목 다시 선택
                    if (selectedId.HasValue)
                        SelectedUser = Users.FirstOrDefault(u => u.UserId == selectedId.Value);
                });
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        public void Dispose()
        {
            _searchDebouncer?.Dispose();
        }


        // DB 로드하는 함수 

        public void LoadUsers()
        {
            try
            {
                var repo = new DB_Manager();
                List<UserModel> data = repo.GetAllUsers();
                Users = new ObservableCollection<UserModel>(data);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        private void ExecuteAddUser(object parameter)
        {
            var vm = new User_AddViewModel();
            var result = _dialogService.ShowDialog(vm);

            if (result == true)
                LoadUsers();
        }
        private void ExecuteOpenSetting(object parameter)  
        {
            _dialogService.ShowSetting();
        }

        private void ExecuteOpenDefault(object parameter)
        {
            _dialogService.ShowDefault();
        }


        private void ExecuteDeleteUser(object parameter)
        {
            try
            {
                if (SelectedUser == null)
                {
                    CustomMessageWindow.Show("삭제할 사용자를 선택해주세요.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                var result = CustomMessageWindow.Show(
                    $"{SelectedUser.Name} 사용자를 정말 삭제하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo,
                    0,
                    CustomMessageWindow.MessageIconType.Danger);

                if (result == CustomMessageWindow.MessageBoxResult.Yes)
                {
                    var db = new DB_Manager();
                    bool success = db.DeleteUser(Convert.ToString(SelectedUser.UserId));

                    if (success)
                    {
                        CustomMessageWindow.Show("삭제되었습니다.",
                            CustomMessageWindow.MessageBoxType.AutoClose, 1,
                            CustomMessageWindow.MessageIconType.Info);
                        LoadUsers();
                    }
                }
            }
            catch(Exception ex)
            {
                Common.WriteLog(ex);
            }

        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}