using LSS_prototype.Auth;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace LSS_prototype
{
    /// <summary>
    /// 작성자 : 박한용
    /// 목적 : 경로, 파일명, 공통 사용 클래스 선언하기 위함
    /// 주의 : Const 사용 ( 변경 절대 불가 )
    /// </summary>
    public static class Common
    {
        // ===== 외부 접근 멤버 =====
        //public const string DB_PATH = "./LSS_TEST.db";                 // .db 경로 
        public static readonly string executablePath = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string DB_PATH = Path.Combine(executablePath, "LSS_TEST.db");
        public static readonly string DB_INIT_PATH = Path.Combine(executablePath, "DB", "db_init.sql");
        public static readonly string DB_SEED_PATH = Path.Combine(executablePath, "DB", "seed.sql");
        public static string CurrentUserId = string.Empty;            // 현재 로그인한 ID 
        public static string MwlDescriptionFilter = string.Empty;       // 현재 MWL FILTER 값 


        public const int DB_VERSION = 46; // DB Version 

        // ===== OTP 기능  =====
        public static bool VerifyMasterOtp(string inputId, string inputOtp)
            => OtpService.VerifyMasterOtp(inputId, inputOtp);

        public static void CleanupOldLogs()
            => LogService.CleanupOldLogs();

        public static void WriteLog(
            Exception ex,
            [CallerMemberName] string method = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int line = 0)
            => LogService.WriteLog(ex, method, filePath, line);

        public enum LogLevel { Info, Warning, Error } // (기존 위치/이름 유지)

        public static void WriteSessionLog(string message)
            => LogService.WriteSessionLog(message);

        public static void ExecuteLogout()
        {
            try
            {
                var result = CustomMessageWindow.Show(
                    "로그아웃 하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo,
                    0,
                    CustomMessageWindow.MessageIconType.Warning);

                if (result != CustomMessageWindow.MessageBoxResult.Yes) return;

                // 1. 타이머 정지 (세션 만료 체크 중단)
                App.ActivityMonitor.Stop(); // 

                // 2. 토큰 초기화
                AuthToken.SignOut();

                // 3. 세션 완전 종료 (열린 창 모두 닫기)
                SessionStateManager.ClearSession();

                // 4. 로그인 창 호출
                var login = new Login_Page.Login();
                login.Show();

                // 5. 나머지 창 모두 닫기
                foreach (Window window in Application.Current.Windows.OfType<Window>().ToList())
                {
                    if (!(window is Login_Page.Login))
                        window.Close();
                }
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }


        public static void ForceLogout()
        {
            try
            {
                // 1. 타이머 정지 (세션 만료 체크 중단)
                App.ActivityMonitor.Stop(); // 

                // 2. 토큰 초기화
                AuthToken.SignOut();

                // 3. 세션 완전 종료 (열린 창 모두 닫기)
                SessionStateManager.ClearSession();

                // 4. 로그인 창 호출
                var login = new Login_Page.Login();
                login.Show();

                // 5. 나머지 창 모두 닫기
                foreach (Window window in Application.Current.Windows.OfType<Window>().ToList())
                {
                    if (!(window is Login_Page.Login))
                        window.Close();
                }
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }



        public static void ExcuteExit()
        {
            var result = CustomMessageWindow.Show(
                    "프로그램을 종료하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo,
                    0,
                    CustomMessageWindow.MessageIconType.Warning);

            if (result != CustomMessageWindow.MessageBoxResult.Yes) return;

            Application.Current.Shutdown();
        }
    }


    /// <summary>
    /// OTP 관련 로직 (Common에서 호출 래핑)
    /// </summary>
    internal static class OtpService
    {

        private const int OTP_SLOT_MINUTES = 3; // OTP 유효시간 +- 3분

        /// <summary>
        /// 입력된 ID + OTP 6자리가 MASTER 계정 기준으로 유효한지 검증
        /// </summary>
        public static bool VerifyMasterOtp(string inputId, string inputOtp)
        {
            try
            {
                // 1) 환경변수에서 MASTER ID / KEY 읽기
                string masterId = Environment.GetEnvironmentVariable(
                    "MASTER_ID", EnvironmentVariableTarget.Machine);
                string masterKey = Environment.GetEnvironmentVariable(
                    "MASTER_KEY", EnvironmentVariableTarget.Machine);

                // 환경변수 미설정 시 검증 불가
                if (string.IsNullOrWhiteSpace(masterId) ||
                    string.IsNullOrWhiteSpace(masterKey))
                {
                    CustomMessageWindow.Show(
                       "MASTER_ID 또는 MASTER_KEY 환경변수가 설정되지 않았습니다.\n 설정 후 재시작 해주세요",
                       CustomMessageWindow.MessageBoxType.Ok,
                       0,
                       CustomMessageWindow.MessageIconType.Warning);

                    Application.Current.Shutdown(); //환경변수가 없다면 무조건 프로그램 종료 시켜야함 -> 최고계정이 활성화되지 않기 때문
                    return false;
                }



                // 2) ID 일치 확인 (대소문자 무시)
                // ID가 비어있으면 MASTER 검증 스킵
                if (string.IsNullOrWhiteSpace(inputId) || string.IsNullOrWhiteSpace(masterId))
                    return false;

                if (!string.Equals(inputId.Trim(), masterId.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                    return false;

                // 3) 현재 슬롯으로 OTP 생성 후 비교
                DateTime now = DateTime.Now;
                DateTime currentSlot = GetOtpSlot(now);
                string currentOtp = GenerateOtp(currentSlot, masterKey);

                if (currentOtp == inputOtp.Trim())
                    return true;

                // 4) 이전 슬롯(-3분)으로 재비교 
                DateTime prevSlot = currentSlot.AddMinutes(-OTP_SLOT_MINUTES);
                string prevOtp = GenerateOtp(prevSlot, masterKey);

                if (prevOtp == inputOtp.Trim())
                    return true;

                // 5) 둘 다 불일치
                return false;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return false;
            }
        }

        /// <summary>
        /// 현재 시간을 OTP_SLOT_MINUTES(3분) 단위로 내림하여 슬롯 반환
        /// 예) 14:07:34 → 14:06:00 / 14:05:59 → 14:03:00
        /// </summary>
        private static DateTime GetOtpSlot(DateTime time)
        {
            int slotMinute = (time.Minute / OTP_SLOT_MINUTES) * OTP_SLOT_MINUTES;
            return new DateTime(time.Year, time.Month, time.Day,
                                time.Hour, slotMinute, 0);
        }

        /// <summary>
        /// 슬롯 시간 + KEY 를 SHA256 해싱하여 6자리 숫자 OTP 생성
        /// 재료: "MMddHHmm"(슬롯기준) + masterKey
        /// </summary>
        private static string GenerateOtp(DateTime slot, string masterKey)
        {
            string material = slot.ToString("MMddHHmm") + masterKey;

            byte[] hash;
            using (var sha = SHA256.Create())
            {
                hash = sha.ComputeHash(Encoding.UTF8.GetBytes(material));
            }

            // 각 바이트 % 10 으로 숫자만 추출하여 앞 6자리 반환
            var sb = new StringBuilder();
            foreach (byte b in hash)
            {
                sb.Append(b % 10);
                if (sb.Length >= 6) break;
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// 로그 관련 로직 (Common에서 호출 래핑)
    /// </summary>
    internal static class LogService
    {
        private static readonly string LOG_DIR = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private static readonly string LOG_DIR_SESSION = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SESSION_LOG");

        // ── 로그 보관 일수 ──
        private const int LOG_RETENTION_DAYS = 30;

        // ── 파일 쓰기 잠금 ──
        private static readonly object _logLock = new object();

        public static void CleanupOldLogs()
        {
            try
            {
                if (!Directory.Exists(LOG_DIR)) return;

                var files = Directory.GetFiles(LOG_DIR, "*.log");
                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file); // "2026-01-25"
                    if (DateTime.TryParse(fileName, out DateTime fileDate))
                    {
                        if ((DateTime.Now.Date - fileDate.Date).TotalDays >= LOG_RETENTION_DAYS)
                        {
                            File.Delete(file);
                            Console.WriteLine($"[로그 정리] 삭제: {file}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("로그 정리 실패: " + ex.Message);
                throw; // 0225 박한용 : 로그 정리 및 생성은 절대 실패가 일어나면안돼서 테스트 차 throw 해놨음. 추후 충분한 테스트 후 삭제예정
            }
        }

        // ══════════════════════════════════════════
        // 예외 로그 기록
        // catch 블록에서 Common.WriteLog(ex) 로 호출
        // CallerMemberName / CallerFilePath / CallerLineNumber
        // → 호출한 메서드명, 파일경로, 라인번호 자동 수집
        // ══════════════════════════════════════════
        public static void WriteLog(
            Exception ex,
            string method,
            string filePath,
            int line)
        {
            try
            {
                if (!Directory.Exists(LOG_DIR))
                    Directory.CreateDirectory(LOG_DIR);

                string className = Path.GetFileNameWithoutExtension(filePath);
                string logFile = Path.Combine(LOG_DIR,
                    $"{DateTime.Now:yyyy-MM-dd}.log");

                var sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR]");
                sb.AppendLine($"  클래스  : {className}");
                sb.AppendLine($"  메서드  : {method}");
                sb.AppendLine($"  라인    : {line}");
                sb.AppendLine($"  메시지  : {ex.Message}");

                // InnerException 있으면 추가 기록
                if (ex.InnerException != null)
                    sb.AppendLine($"  내부오류 : {ex.InnerException.Message}");

                sb.AppendLine($"  StackTrace :");
                sb.AppendLine($"  {ex.StackTrace}");
                sb.AppendLine(new string('─', 60));
                sb.AppendLine();

                lock (_logLock)
                {
                    File.AppendAllText(logFile, sb.ToString(), Encoding.UTF8);
                }

                // 사용자에게 알려주기 위한 팝업 표시 
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    CustomMessageWindow.Show(
                        ex.Message,
                        CustomMessageWindow.MessageBoxType.Ok,
                        0,
                        CustomMessageWindow.MessageIconType.Danger);
                });
            }
            catch (Exception logEx)
            {
                // 로그 기록 자체가 실패해도 앱이 죽으면 안 됨
                Console.WriteLine("로그 기록 실패: " + logEx.Message);
            }
        }

        // 세션 로그 전용 함수
        public static void WriteSessionLog(string message)
        {
            try
            {
                if (!Directory.Exists(LOG_DIR_SESSION))
                    Directory.CreateDirectory(LOG_DIR_SESSION);

                string logFile = Path.Combine(LOG_DIR_SESSION,
                    $"{Common.CurrentUserId}({DateTime.Now:yyyyMMdd}).log");

                var sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ACTION]");
                sb.AppendLine($"  메시지  : {message}");
                sb.AppendLine(new string('─', 60));
                sb.AppendLine();

                lock (_logLock)
                {
                    File.AppendAllText(logFile, sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("세션 로그 기록 실패: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// 모든 쿼리는 해당 클래스 내에서 관리 ( 코드상 직접 기입 지양 )
    /// </summary>
    public static class Query
    {
        // ================================================
        // DB 버전 관리  -  DB_Manager.cs
        // ================================================
        public const string INSERT_DB_VERSION = "INSERT INTO DB_VERSION(version) VALUES (@version)";
        public const string SELECT_VERSION = "SELECT version FROM DB_VERSION";

        // ================================================
        // Login_Page  -  DB_Manager.User.cs / Login_Page
        // ================================================
        public const string LOGIN_HASH_CHECK = "SELECT PASSWORD_HASH, PASSWORD_SALT, ROLE_CODE, PASSWORD_CHANGED_AT, USER_ID FROM USER WHERE LOGIN_ID = @loginId";
        public const string ADMIN_ID_SEARCH = "SELECT LOGIN_ID FROM USER WHERE ROLE_CODE='A'"; // 관리자 모드 체크박스 노출 여부 판단용

        // ================================================
        // User_Page  -  DB_Manager.User.cs
        // ================================================
        public const string SELECT_USERLIST = "SELECT USER_ID, USER_NAME, LOGIN_ID, USER_ROLE, ROLE_CODE FROM USER ORDER BY USER_ID ASC";
        public const string SEARCH_USERID_NAME = "SELECT USER_ID, USER_NAME, LOGIN_ID, USER_ROLE, ROLE_CODE FROM USER WHERE USER_NAME LIKE @keyword OR LOGIN_ID LIKE @keyword ORDER BY USER_ID ASC";
        public const string INSERT_ADD_USER = "INSERT INTO USER(LOGIN_ID, PASSWORD_HASH, PASSWORD_SALT, USER_NAME, USER_ROLE, DEVICE_ID, ROLE_CODE)" +
                                                 " VALUES (@loginId, @hash, @salt, @userName, @userRole, @device_id, @role_code)";
        public const string DELETE_USER = "DELETE FROM USER WHERE USER_ID = @user_id  AND (ROLE_CODE != 'A' OR (SELECT COUNT(*) FROM USER WHERE ROLE_CODE = 'A') >= 2)";
        public const string DELEGATE_USER = "UPDATE USER SET ROLE_CODE = 'A' WHERE USER_ID = @user_id";                                                          // 관리자 권한 위임
        public const string DISMISS_USER = "UPDATE USER SET ROLE_CODE = 'U' WHERE USER_ID = @user_id AND (SELECT COUNT(*) FROM USER WHERE ROLE_CODE = 'A') >= 2"; // 관리자 권한 해임 (관리자 2명 이상일 때만)

        // ================================================
        // ChangePasswordDialog  -  DB_Manager.User.cs
        // ================================================
        public const string PASSWORD_EDIT = "UPDATE USER SET password_hash = @hash, password_salt = @salt, PASSWORD_CHANGED_AT = @password_changedDate WHERE login_id = @loginId";
        public const string CREDENTIAL_EDIT_WITH_ROLE = "UPDATE USER SET login_id = @newId, password_hash = @hash, password_salt = @salt, PASSWORD_CHANGED_AT = @password_changedDate, USER_ROLE = @role WHERE login_id = @oldId"; // 최초 로그인 시 ID+PW+Role 동시 변경
        public const string CREDENTIAL_EDIT = "UPDATE USER SET login_id = @newId, password_hash = @hash, password_salt = @salt, PASSWORD_CHANGED_AT = @password_changedDate WHERE login_id = @oldId"; // 관리자가 USER 비밀번호 및 ID 변경 쿼리 
        public const string ADMIN_UPDATE = "UPDATE USER SET password_hash = @hash, password_salt = @salt, PASSWORD_CHANGED_AT = @password_changedDate, login_id = @logiId WHERE login_id = @loginId";

        // ================================================
        // Patient_Page  -  DB_Manager.Patient.cs
        // ================================================
        public const string SELECT_PATIENTLIST = "SELECT * FROM PATIENT ORDER BY REG_DATE DESC";
        public const string SEARCH_PATIENT = "SELECT PATIENT_ID, PATIENT_CODE, PATIENT_NAME, BIRTH_DATE, SEX, REG_DATE FROM PATIENT WHERE PATIENT_NAME LIKE @keyword OR PATIENT_CODE LIKE @keyword ORDER BY PATIENT_ID ASC";
        public const string INSERT_PATIENT = "INSERT INTO PATIENT (PATIENT_NAME, PATIENT_CODE, BIRTH_DATE, SEX) VALUES (@PatientName, @PatientCode, @BirthDate, @Sex)";
        public const string EDIT_PATIENT = "UPDATE PATIENT SET PATIENT_NAME = @PatientName, PATIENT_CODE = @PatientCode, BIRTH_DATE = @BirthDate, SEX = @Sex WHERE PATIENT_ID = @Patient_id";
        public const string DELETE_PATIENT = "DELETE FROM PATIENT WHERE PATIENT_ID = @Patient_id";
        public const string PATIENT_CODE_SEARCH = "SELECT COUNT(1) FROM PATIENT WHERE PATIENT_CODE = @PatientCode";                                                  // 환자 코드 중복 체크
        public const string PATIENT_CODE_SEARCHSELF = "SELECT COUNT(1) FROM PATIENT WHERE PATIENT_CODE = @PatientCode AND PATIENT_ID <> @Patient_id";                   // 수정 시 자기 자신 제외 중복 체크

        // ================================================
        // User_Page > Default (카메라 기본값)  -  DB_Manager.Set.cs
        // ================================================
        public const string SELECT_DEFAULT = "SELECT EXPOSURE_TIME, GAIN, GAMMA, FOCUS, IRIS, ZOOM, FILTER FROM CAMERA_DEFAULT_SET";
        public const string UPDATE_DEFAULT = "UPDATE CAMERA_DEFAULT_SET SET EXPOSURE_TIME=@ExposureTime, GAIN=@Gain, GAMMA=@Gamma," +
                                             " FOCUS=@Focus, IRIS=@Iris, ZOOM=@Zoom, FILTER=@Filter";

        // ================================================
        // User_Page > Setting (PACS 설정)  -  DB_Manager.Set.cs
        // ================================================
        public const string SELECT_PACS = "SELECT * FROM PACS_SET LIMIT 1";
        public const string UPDATE_HOSPITAL = "UPDATE PACS_SET SET HOSPITAL_NAME=@HospitalName";
        public const string UPDATE_CSTORE = "UPDATE PACS_SET SET CSTORE_AET=@CStoreAET, CSTORE_IP=@CStoreIP, CSTORE_PORT=@CStorePort, CSTORE_MY_AET=@CStoreMyAET";
        public const string UPDATE_MWL = "UPDATE PACS_SET SET MWL_AET=@MwlAET, MWL_IP=@MwlIP, MWL_PORT=@MwlPort, MWL_MY_AET=@MwlMyAET";
        public const string UPDATE_MWL_FILTER = "UPDATE PACS_SET SET MWL_DESCRIPTION_FILTER = @MwlDescriptionFilter";

        // ================================================
        // Recycle  -  DB_Manager.Recycle.cs
        // ================================================
        public const string INSERT_IMAGE_DELETE_LOG ="INSERT INTO DELETE_LOG (DELETED_BY, FILE_TYPE, IMAGE_PATH, PATIENT_CODE, PATIENT_NAME) VALUES (@DeletedBy, @FileType, @ImagePath, @PatientCode, @PatientName)";
        public const string SELECT_DELETE_LOGS = "SELECT DELETE_ID, DELETED_BY, DELETED_AT, FILE_TYPE, IMAGE_PATH, AVI_PATH, DICOM_PATH, PATIENT_CODE, IS_RECOVERED, RECOVERED_AT, PATIENT_NAME  FROM DELETE_LOG ORDER BY DELETED_AT DESC";
        public const string UPDATE_RECOVERED ="UPDATE DELETE_LOG SET IS_RECOVERED = 'Y', RECOVERED_AT = datetime('now', 'localtime') WHERE DELETE_ID = @DeleteId";
    }
}

/// <summary>
/// 버튼 중복 클릭 및 페이지 중복실행 막는 클래스 
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object, Task> _execute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<object, Task> execute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public bool CanExecute(object parameter) => true;

    public async void Execute(object parameter)
    {
        // 중복터치 방지
        if (_isExecuting) return;

        _isExecuting = true;
        try
        {
            await _execute(parameter);
        }
        finally
        {
            _isExecuting = false;
        }
    }

    public event EventHandler CanExecuteChanged
    {
        add { }
        remove { }
    }
}


/// <summary>
/// 검색 입력에 디바운싱을 적용하는 공용 함수 
/// 마지막 입력으로부터 지정된 딜레이(기본 500ms)가 지난 후에만 콜백을 실행
/// 작성자 : 박한용
/// </summary>
public class SearchDebouncer : IDisposable
{
    private readonly Action<string> _callback;  // 디바운싱 후 실행할 검색 콜백
    private readonly int _delayMs;              // 딜레이 (밀리초)
    private Timer _timer;
    private bool _disposed = false;

    /// <param name="callback">딜레이 후 실행할 검색 함수 (검색어를 인자로 받음)</param>
    /// <param name="delayMs">디바운싱 딜레이 (기본값 500ms)</param>
    public SearchDebouncer(Action<string> callback, int delayMs = 500)
    {
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        _delayMs = delayMs;
    }

    /// <summary>
    /// 검색어가 변경될 때마다 호출. 타이머를 리셋하여 마지막 호출 기준으로 딜레이를 적용
    /// </summary>
    /// <param name="searchText">변경된 검색어</param>
    public void OnTextChanged(string searchText)
    {
        if (_disposed) return;

        // 기존 타이머 취소 후 재시작 (딜레이 리셋)
        _timer?.Dispose();
        _timer = new Timer(
            _ => _callback(searchText),
            null,
            _delayMs,
            Timeout.Infinite    // 단발성 실행 (반복 없음)
        );
    }

    /// <summary>
    /// 진행 중인 디바운싱을 즉시 취소 (예: 창 닫힐 때)
    /// </summary>
    public void Cancel()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cancel();
    }


}