using LSS_prototype.User_Page;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype.DB_CRUD
{
    public partial class DB_Manager
    {
        #region [ 로그인 담당부 ]

        /// <summary>
        /// 로그인 검증 함수
        /// 입력받은 ID/PW로 DB 조회 후 해시 비교를 수행
        /// </summary>
        /// <returns>로그인 성공 여부 (true: 성공, false: 실패)</returns>
        public bool Login_check(string loginId, string password, out string roleCode)
        {
            roleCode = null;
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
                            // 1. DB에 저장된 해시값과 솔트값 가져오기 + 로그인 성공 대비 권한값(ROLE) 도 가져오기
                            string storedHash = reader["PASSWORD_HASH"].ToString();
                            string storedSalt = reader["PASSWORD_SALT"].ToString();
                            string dbRoleCode = reader["ROLE_CODE"].ToString();

                            // 2. 입력받은 비밀번호 검증
                            bool isPasswordCorrect = VerifyPassword(password, storedHash, storedSalt);

                            if (isPasswordCorrect)
                            {
                                roleCode = dbRoleCode;
                                return true;
                            }
                            return false;
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
        public bool InsertUser(string loginId, string userName, string userRole, string password, string device_id = "1001", string role_code = "N")
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

        public List<UserModel> GetAllUsers()
        {
            List<UserModel> list = new List<UserModel>();
            using (var conn = new SQLiteConnection("Data Source=" + Common.DB_PATH))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.SELECT_USERLIST, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new UserModel
                            {
                                UserId = Convert.ToInt32(reader["USER_ID"]),
                                Name = reader["USER_NAME"].ToString(),
                                UserCode = reader["LOGIN_ID"].ToString(),
                                Role = reader["USER_ROLE"].ToString(),
                                Department = reader["ROLE_CODE"].ToString()
                            });
                        }
                    }
                }
            }
            return list;
        }

        #endregion
    }
}
