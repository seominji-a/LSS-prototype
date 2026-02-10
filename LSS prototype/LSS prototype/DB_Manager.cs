using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Threading;
using System.Security.Cryptography;

/// <summary>
//  작성자 : 박한용
/// 목적 : SQLite DB 테이블 구조 공유
/// 설명 : init.sql ( CREATE 구문 ), SEED.sql( INSERT 구문 )
/// 주의 : 테이블 내 값들은 공유안됨. 
/// </summary>
namespace LSS_prototype
{
    class DB_Manager
    {
        #region [ 전역변수 선언부 ]
        private readonly string _dbPath = Common.DB_PATH; // .db 파일 위치 
        private readonly string _db_init_Path = Common.DB_INIT_PATH; // .init 파일 위치 
        private readonly string _db_seed_Path = Common.DB_SEED_PATH; // .seed 파일 위치
        private readonly int _db_version = Common.DB_VERSION; // 로컬에 저장된 db 버전
        #endregion

        #region [ DB 생성 및 버전확인 ] 
        public void InitDB()
        {
            bool first_create = false;

            try
            {
                bool dbExists = File.Exists(_dbPath);

                if (!dbExists) // DB 파일 미존재 시, DB 및 테이블 생성 
                {
                    SQLiteConnection.CreateFile(_dbPath);
                    first_create = true;
                }
                else // DB파일 존재 시 버전 확인 -> 사용자에게 업데이트 YES/NO 물어보고 진행 
                {
                    bool needRecreate = false;

                    using (var conn_ = new SQLiteConnection($"Data Source={Common.DB_PATH};"))
                    {
                        conn_.Open();

                        using (var cmd = new SQLiteCommand(Query.SELECT_VERSION, conn_))
                        {
                            int dbVersion = Convert.ToInt32(cmd.ExecuteScalar());

                            if (dbVersion < this._db_version)
                            {
                                var result = MessageBox.Show(
                                    "DB 버전이 다릅니다.\n기존 로컬 DB가 삭제되고 신규 DB가 생성됩니다.\n진행하시겠습니까?",
                                    "DB 업데이트 확인",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Warning);

                                if (result == MessageBoxResult.Yes) // 진짜 삭제 후 db를 재설치 하시겠습니까? ( 이 때, 기존 테이블까지 다 delete 됨 주의 )  
                                    needRecreate = true;
                                else
                                    return;
                            }
                        }
                    }

                    if (needRecreate)
                    {
                        SQLiteConnection.ClearAllPools();
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();

                        Thread.Sleep(300); // 자원이 정리될 최소한의 시간 확보

                        if (File.Exists(Common.DB_PATH))
                            File.Delete(Common.DB_PATH);

                        InitDB(); // 자원 해제 및 기존 db 삭제 후 재귀방식으로 InitDB 함수 재호출
                        return;
                    }
                }

                if (first_create)
                {
                    using (var conn = new SQLiteConnection($"Data Source={_dbPath};"))
                    {
                        conn.Open();

                        string initSql = File.ReadAllText(_db_init_Path);
                        using (var initCmd = new SQLiteCommand(initSql, conn))
                            initCmd.ExecuteNonQuery();

                        string seedSql = File.ReadAllText(_db_seed_Path);
                        using (var seedCmd = new SQLiteCommand(seedSql, conn))
                            seedCmd.ExecuteNonQuery();

                        using (var verCmd = new SQLiteCommand(Query.INSERT_DB_VERSION, conn))
                        {
                            verCmd.Parameters.AddWithValue("@version", this._db_version);
                            verCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("DB Init Error : " + ex.Message);

                if (first_create && File.Exists(_dbPath))
                    File.Delete(_dbPath);

                throw;
            }
        }
        #endregion

        #region [ 로그인 담당부 ]

        /// <summary>
        /// 로그인 검증 함수
        /// 입력받은 ID/PW로 DB 조회 후 해시 비교를 수행
        /// </summary>
        /// <returns>로그인 성공 여부 (true: 성공, false: 실패)</returns>
        public bool Login_check(string loginId, string password)
        {
            // using 문을 사용하여 DB 연결 자동 해제 
            using (SQLiteConnection conn = new SQLiteConnection("Data Source=" + Common.DB_PATH))
            {
                conn.Open();

                // DB에서 해당 LOGIN_ID의 해시값과 솔트값 조회
                using (SQLiteCommand cmd = new SQLiteCommand(Query.LOGIN_HASH_CHECK, conn))
                {
                    cmd.Parameters.AddWithValue("@loginId", loginId);

                    // 쿼리 실행 및 결과 읽기
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        // 해당 ID를 가진 사용자가 존재하는지 확인
                        if (reader.Read())
                        {
                            // 1. DB에 저장된 해시값과 솔트값 가져오기
                            string storedHash = reader["PASSWORD_HASH"].ToString();
                            string storedSalt = reader["PASSWORD_SALT"].ToString();

                            // 2. 입력받은 비밀번호 검증
                            bool isPasswordCorrect = VerifyPassword(password, storedHash, storedSalt);

                            return isPasswordCorrect;
                        }
                        else
                        {
                            // 해당 ID를 가진 사용자가 DB에 없음
                            return false;
                        }
                    }
                }
            }
        }
        #endregion

        #region [ 해싱 및 솔트 ]

        /// <summary>
        /// 입력된 비밀번호가 저장된 해시값과 일치하는지 검증
        /// </summary>
        /// <param name="inputPassword">사용자가 입력한 비밀번호 (평문)</param>
        /// <param name="storedHash">DB에 저장된 해시값</param>
        /// <param name="storedSalt">DB에 저장된 솔트값</param>
        /// <returns>비밀번호 일치 여부 (true: 일치, false: 불일치)</returns>
        private static bool VerifyPassword(string inputPassword, string storedHash, string storedSalt)
        {
            // 1. 입력받은 비밀번호를 DB의 솔트값으로 해싱
            string inputHash = GenerateHash(inputPassword, storedSalt);
            // 2. 생성된 해시값과 DB의 해시값 비교
            return inputHash == storedHash;
        }

        /// <summary>
        /// 비밀번호와 솔트를 결합하여 SHA256 해시값을 생성
        /// </summary>
        /// <param name="password">해싱할 비밀번호</param>
        /// <param name="salt">솔트값</param>
        /// <returns>생성된 해시값</returns>
        private static string GenerateHash(string password, string salt)
        {
            using (var sha256 = SHA256.Create())
            {
                string combined = password + salt;
                byte[] bytes = Encoding.UTF8.GetBytes(combined);
                byte[] hashBytes = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
        /// <summary>
        /// salt 생성 함수 
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        private static string GenerateSalt()
        {
            byte[] saltBytes = new byte[32];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
        }
        #endregion

        #region [ User_Page DB CRUD ]
        /// <summary>
        /// 사용자 추가 쿼리문 및 비밀번호 솔트 및 해싱 이용 암호화 저장 
        /// </summary>
        /// <param name="loginId"></param>
        /// <param name="userName"></param>
        /// <param name="userRole"></param>
        /// <param name="password"></param
        /// <param name="device_id">테스트 위해 기본값 1001 </param>
        /// <param name="device_id">테스트 위해 기본값 N </param>
        /// <returns></returns>
        public bool InsertUser(string loginId, string userName, string userRole, string password, string device_id = "1001", string role_code ="N")
        {

            string passwordSalt = GenerateSalt();
            string passwordHash = GenerateHash(password, passwordSalt);

            using (var conn = new SQLiteConnection("Data Source=" + Common.DB_PATH))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = Query.INSERT_ADD_USER;

                    cmd.Parameters.AddWithValue("@loginId", loginId);
                    cmd.Parameters.AddWithValue("@hash", passwordHash);
                    cmd.Parameters.AddWithValue("@salt", passwordSalt);
                    cmd.Parameters.AddWithValue("@userName", userName);
                    cmd.Parameters.AddWithValue("@userRole", userRole);
                    cmd.Parameters.AddWithValue("@device_id", device_id);
                    cmd.Parameters.AddWithValue("@role_code", role_code);


                    int result = cmd.ExecuteNonQuery();
                    bool isSuccess = result == 1;

                    return isSuccess;
                }
            }
        }


        #endregion


    }
}
