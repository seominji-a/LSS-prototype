using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LSS_prototype
{
    /// <summary>
    //  작성자 : 박한용
    /// 목적 : 경로, 파일명, 공통 사용 클래스 선언하기 위함
    /// 주의 : Const 사용 ( 변경 절대 불가 )
    /// </summary>
    public static class Common
    {
        public const string DB_PATH = "./LSS_TEST.db"; // .db 경로 
        public const string DB_INIT_PATH = "../../../DB/db_init.sql"; // 초기 DB 테이블 생성 파일 경로 
        public const string DB_SEED_PATH = "../../../DB/seed.sql"; // 초기 DB 테이블 데이터 생성 경로 

        public const int DB_VERSION = 33; // DB Version 
        private const int OTP_SLOT_MINUTES = 3; // OTP 유효시간 +- 3분

        /// <summary>
        /// 입력된 ID + OTP 6자리가 MASTER 계정 기준으로 유효한지 검증
        /// </summary>
        /// <param name="inputId">사용자가 입력한 ID</param>
        /// <param name="inputOtp">사용자가 입력한 OTP 6자리</param>
        /// <returns>MASTER ID 일치 + OTP 유효 → true / 그 외 → false</returns>
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
                    Console.WriteLine("LSS_MASTER_ID 또는 LSS_MASTER_KEY 환경변수가 설정되지 않았습니다.");
                    return false;
                }

                // 2) ID 일치 확인 (대소문자 무시)
                if (!string.Equals(inputId.Trim(), masterId.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                    return false;

                // 3) 현재 슬롯으로 OTP 생성 후 비교
                DateTime now = DateTime.Now;
                DateTime currentSlot = GetOtpSlot(now);
                string currentOtp = GenerateOtp(currentSlot, masterKey);

                if (currentOtp == inputOtp.Trim())
                    return true;

                // 4) 이전 슬롯(-3분)으로 재비교 (경계값 보호)
                DateTime prevSlot = currentSlot.AddMinutes(-OTP_SLOT_MINUTES);
                string prevOtp = GenerateOtp(prevSlot, masterKey);

                if (prevOtp == inputOtp.Trim())
                    return true;

                // 5) 둘 다 불일치
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " VerifyMasterOtp Function Check");
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
    /// 모든 쿼리는 해당 클래스 내에서 관리 ( 코드상 직접 기입 지양 )
    /// </summary>
    public static class Query
    {
        public const string INSERT_DB_VERSION = "INSERT INTO DB_VERSION(version) VALUES (@version)"; // DB VERSION INSERT QUERY 
        public const string SELECT_VERSION = "SELECT version FROM DB_VERSION"; // DB 버전 SELECT QUERY
        public const string LOGIN_HASH_CHECK = "SELECT PASSWORD_HASH, PASSWORD_SALT,ROLE_CODE, PASSWORD_CHANGED_AT, USER_ID FROM USER WHERE LOGIN_ID = @loginId"; // 로그인 시 해싱및 솔트값 확인 쿼리문
        public const string INSERT_PATIENT = "INSERT INTO PATIENT (PATIENT_NAME,PATIENT_CODE, BIRTH_DATE, SEX ) VALUES (@PatientName, @PatientCode, @BirthDate, @Sex)";
        public const string SELECT_PATIENT_LIST = "SELECT * FROM PATIENT";
        public const string INSERT_ADD_USER = "INSERT INTO USER(LOGIN_ID, PASSWORD_HASH,PASSWORD_SALT, USER_NAME, USER_ROLE, DEVICE_ID, ROLE_CODE)" +
                                              " VALUES (@loginId, @hash, @salt, @userName, @userRole, @device_id, @role_code)"; // 사용자 추가
        public const string EDIT_PATIENT = "UPDATE PATIENT SET PATIENT_NAME = @PatientName, PATIENT_CODE= @PatientCode, BIRTH_DATE = @BirthDate, SEX = @Sex WHERE PATIENT_ID = @Patient_id";
        public const string SELECT_PATIENTLIST = "SELECT * FROM PATIENT ORDER BY REG_DATE DESC"; // 최신순 데이터 
        public const string DELETE_PATIENT = "DELETE FROM PATIENT WHERE PATIENT_ID = @Patient_id";
        public const string SELECT_USERLIST = "SELECT USER_ID, USER_NAME, LOGIN_ID, USER_ROLE, ROLE_CODE FROM USER ORDER BY USER_ID ASC"; // 유저조회 쿼리문 
        public const string ADMIN_ID_SEARCH = "SELECT LOGIN_ID FROM USER WHERE USER_ROLE='ADMIN'"; // ADMIN 권한을 가진 ID 조회 
        public const string PASSWORD_EDIT = @"UPDATE USER SET password_hash = @hash, password_salt = @salt, PASSWORD_CHANGED_AT = @password_changedDate WHERE login_id = @loginId";// 비밀번호변경 쿼리문 

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



}