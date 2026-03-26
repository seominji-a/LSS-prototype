using LSS_prototype.Auth;
using LSS_prototype.DB_CRUD;
using LSS_prototype.Login_Page;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
        #region 필드

        private readonly SearchDebouncer _searchDebouncer;
        private readonly IDialogService _dialogService;

        private string _searchText;
        private UserModel _selectedUser;
        private bool _isMenuOpen;
        private DateTime _menuLastClosed = DateTime.MinValue;

        #endregion

        #region 프로퍼티

        public ObservableCollection<UserModel> Users { get; private set; }
            = new ObservableCollection<UserModel>();

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                // ── 디바운서는 여기서만 호출 (코드비하인드 OnSearchTextChanged 중복 제거)
                _searchDebouncer.OnTextChanged(value);
            }
        }

        public UserModel SelectedUser
        {
            get => _selectedUser;
            set { _selectedUser = value; OnPropertyChanged(); }
        }

        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            set
            {
                if (_isMenuOpen && !value) _menuLastClosed = DateTime.UtcNow;
                _isMenuOpen = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region 커맨드

        public ICommand AddUserCommand { get; }
        public ICommand EditUserCommand { get; }
        public ICommand DeleteUserCommand { get; }
        public ICommand DelegateCommand { get; }
        public ICommand DismissCommand { get; }
        public ICommand SettingCommand { get; }
        public ICommand DefaultCommand { get; }
        public ICommand RecoveryCommand { get; }
        public ICommand LockCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand ToggleMenuCommand { get; }

        #endregion

        #region 생성자

        public UserViewModel()
        {
            _dialogService = new Dialog();

            AddUserCommand = new RelayCommand(async _ => await ExecuteAddUser());
            EditUserCommand = new RelayCommand(async _ => await ExecuteEditUser());
            DeleteUserCommand = new RelayCommand(async _ => await ExecuteDeleteUser());
            DelegateCommand = new RelayCommand(async _ => await ExecuteDelegate());
            DismissCommand = new RelayCommand(async _ => await ExecuteDismiss());
            SettingCommand = new RelayCommand(async _ => await ExecuteOpenSetting());
            DefaultCommand = new RelayCommand(async _ => await ExecuteOpenDefault());
            RecoveryCommand = new RelayCommand(_ => MainPage.Instance.NavigateTo(new Recovery()));
            LockCommand = new AsyncRelayCommand(async _ => await ExecuteLock());
            LogoutCommand = new AsyncRelayCommand(async _ => await ExecuteLogout());
            ExitCommand = new AsyncRelayCommand(async _ => await ExecuteExit());
            ToggleMenuCommand = new RelayCommand(_ => ToggleMenu());

            _searchDebouncer = new SearchDebouncer(async keyword => await ExecuteSearch(keyword), delayMs: 500);
        }

        #endregion

        #region 초기화

        public async Task InitializeAsync()
        {
            await LoadUsers();
        }

        #endregion

        #region 사용자 목록 로드

        public async Task LoadUsers()
        {
            try
            {
                var repo = new DB_Manager();
                var data = repo.GetAllUsers();
                Users = new ObservableCollection<UserModel>(data);
                OnPropertyChanged(nameof(Users));
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        #endregion

        #region 검색

        /// <summary>
        /// 코드비하인드에서 TextChanged 이벤트로 직접 호출하는 경우 사용
        /// SearchText setter와 중복 호출되지 않도록 주의
        /// </summary>
        public void OnSearchTextChanged(string text)
        {
            _searchDebouncer.OnTextChanged(text);
        }

        private async Task ExecuteSearch(string keyword)
        {
            try
            {
                var repo = new DB_Manager();
                List<UserModel> data = string.IsNullOrWhiteSpace(keyword)
                    ? repo.GetAllUsers()
                    : repo.SearchUsers(keyword);

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    int? selectedId = SelectedUser?.UserId;

                    Users.Clear();
                    foreach (var user in data)
                        Users.Add(user);

                    // 검색 후 기존 선택 항목 유지
                    if (selectedId.HasValue)
                        SelectedUser = Users.FirstOrDefault(u => u.UserId == selectedId.Value);
                });
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        #endregion

        #region 사용자 CRUD

        private async Task ExecuteAddUser()
        {
            try
            {
                var vm = new User_AddViewModel();
                var result = await _dialogService.ShowDialogAsync(vm);
                if (result == true)
                    await LoadUsers();
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task ExecuteEditUser()
        {
            try
            {
                if (SelectedUser == null)
                {
                    await CustomMessageWindow.ShowAsync(
                        "수정할 사용자를 선택해주세요.",
                        CustomMessageWindow.MessageBoxType.Ok, 1,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                // AuthToken.RoleCode == "M" 으로 MASTER 여부 판단
                // 로그인 시점에 이미 SignIn(UserId, "M") 으로 세팅되어 있음
                bool isMaster = AuthToken.RoleCode == "M";

                var vm = new User_EditViewModel(SelectedUser, isMaster);
                var result = await _dialogService.ShowDialogAsync(vm);
                if (result == true)
                    await LoadUsers();
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task ExecuteDeleteUser()
        {
            try
            {
                if (SelectedUser == null)
                {
                    await CustomMessageWindow.ShowAsync(
                        "삭제할 사용자를 선택해주세요.",
                        CustomMessageWindow.MessageBoxType.Ok, 1,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                string msg = SelectedUser.LoginId == Common.CurrentUserId
                    ? $"자신({SelectedUser.LoginId})을 정말 삭제하시겠습니까?\n되돌릴 수 없는 명령입니다.\n로그인 페이지로 이동합니다."
                    : $"{SelectedUser.UserName} 사용자를 정말 삭제하시겠습니까?\n되돌릴 수 없는 명령입니다.";

                var result = await CustomMessageWindow.ShowAsync(
                    msg,
                    CustomMessageWindow.MessageBoxType.YesNo, 0,
                    CustomMessageWindow.MessageIconType.Info);

                if (result != CustomMessageWindow.MessageBoxResult.Yes) return;

                var db = new DB_Manager();
                bool success = db.DeleteUser(SelectedUser.UserId);

                if (success)
                {
                    await CustomMessageWindow.ShowAsync(
                        $"{SelectedUser.UserName} 삭제",
                        CustomMessageWindow.MessageBoxType.Ok, 2,
                        CustomMessageWindow.MessageIconType.Info);

                    if (SelectedUser.LoginId == Common.CurrentUserId)
                        await Common.ForceLogout();
                    else
                        await LoadUsers();
                }
                else
                {
                    await CustomMessageWindow.ShowAsync(
                        "최소 1명의 관리자는 유지되어야 합니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 1,
                        CustomMessageWindow.MessageIconType.Warning);
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        #endregion

        #region 권한 관리 (DELEGATE / DISMISS)

        private async Task ExecuteDelegate()
        {
            try
            {
                if (SelectedUser == null)
                {
                    await CustomMessageWindow.ShowAsync(
                        "권한을 부여할 사용자를 선택해 주세요.",
                        CustomMessageWindow.MessageBoxType.Ok, 1,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                if (SelectedUser.RoleCode == "A")
                {
                    await CustomMessageWindow.ShowAsync(
                        "이미 관리자 권한입니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 1,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                var result = await CustomMessageWindow.ShowAsync(
                    $"{SelectedUser.UserName} 사용자에게 관리자 권한을\n부여하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo, 0,
                    CustomMessageWindow.MessageIconType.Warning);

                if (result != CustomMessageWindow.MessageBoxResult.Yes) return;

                var db = new DB_Manager();
                bool success = db.DelegateUser(SelectedUser.UserId);

                if (success)
                {
                    await CustomMessageWindow.ShowAsync(
                        $"{SelectedUser.UserName} 관리자 권한 부여",
                        CustomMessageWindow.MessageBoxType.Ok, 1,
                        CustomMessageWindow.MessageIconType.Info);
                    await LoadUsers();
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task ExecuteDismiss()
        {
            try
            {
                if (SelectedUser == null)
                {
                    await CustomMessageWindow.ShowAsync(
                        "권한 해임할 사용자를 선택해 주세요.",
                        CustomMessageWindow.MessageBoxType.Ok, 1,
                        CustomMessageWindow.MessageIconType.Info);
                    return;
                }

                if (SelectedUser.RoleCode != "A")
                {
                    await CustomMessageWindow.ShowAsync(
                        "이미 일반 권한입니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 1,
                        CustomMessageWindow.MessageIconType.Info);
                    return;
                }

                string msg = SelectedUser.LoginId == Common.CurrentUserId
                    ? $"자신({SelectedUser.LoginId})의 관리자 권한을\n해임하시겠습니까?\n로그인 페이지로 이동합니다."
                    : $"{SelectedUser.UserName} 사용자의 관리자 권한을\n해임하시겠습니까?";

                var result = await CustomMessageWindow.ShowAsync(
                    msg,
                    CustomMessageWindow.MessageBoxType.YesNo, 0,
                    CustomMessageWindow.MessageIconType.Info);

                if (result != CustomMessageWindow.MessageBoxResult.Yes) return;

                var db = new DB_Manager();
                bool success = db.DismissUser(SelectedUser.UserId);

                if (success)
                {
                    await CustomMessageWindow.ShowAsync(
                        $"{SelectedUser.UserName} 관리자 해임",
                        CustomMessageWindow.MessageBoxType.Ok, 2,
                        CustomMessageWindow.MessageIconType.Info);

                    if (SelectedUser.LoginId == Common.CurrentUserId)
                        await Common.ForceLogout();
                    else
                        await LoadUsers();
                }
                else
                {
                    await CustomMessageWindow.ShowAsync(
                        "최소 1명의 관리자는 유지되어야 합니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 1,
                        CustomMessageWindow.MessageIconType.Warning);
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        #endregion

        #region 시스템 설정

        private async Task ExecuteOpenSetting()
        {
            await _dialogService.ShowSetting();
        }

        private async Task ExecuteOpenDefault()
        {
            await _dialogService.ShowDefault();
        }

        #endregion

        #region 메뉴 (Lock / Logout / Exit)

        private void ToggleMenu()
        {
            if (!IsMenuOpen && (DateTime.UtcNow - _menuLastClosed).TotalMilliseconds < 200)
                return;
            IsMenuOpen = !IsMenuOpen;
        }

        private async Task ExecuteLock()
        {
            IsMenuOpen = false;

            var result = await CustomMessageWindow.ShowAsync(
                "프로그램을 잠금하시겠습니까?",
                CustomMessageWindow.MessageBoxType.YesNo, 0,
                CustomMessageWindow.MessageIconType.Info);

            if (result != CustomMessageWindow.MessageBoxResult.Yes) return;

            App.ActivityMonitor.Stop();
            SessionStateManager.SuspendSession();
            var sessionLoginWindow = new SessionLogin();
            sessionLoginWindow.Show();
            Application.Current.MainWindow = sessionLoginWindow;
        }

        private async Task ExecuteLogout()
        {
            IsMenuOpen = false;
            await Common.ExecuteLogout();
        }

        private async Task ExecuteExit()
        {
            IsMenuOpen = false;
            await Common.ExcuteExit();
        }

        #endregion

        #region 자원 정리

        public void Dispose()
        {
            _searchDebouncer?.Dispose();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}
