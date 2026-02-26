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
        public bool Login_check(string loginId, string password, out string roleCode, out DateTime? passwordChangedAt, out string user_id)
        {
            roleCode = string.Empty;
            passwordChangedAt = null;
            user_id = string.Empty;

            using (SQLiteConnection conn = new SQLiteConnection("Data Source=" + Common.DB_PATH))
            {
                conn.Open();

                using (SQLiteCommand cmd = new SQLiteCommand(Query.LOGIN_HASH_CHECK, conn))
                {
                    cmd.Parameters.AddWithValue("@loginId", loginId);

                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            return false;

                        string storedHash = reader["PASSWORD_HASH"].ToString();
                        string storedSalt = reader["PASSWORD_SALT"].ToString();
                        string dbRoleCode = reader["ROLE_CODE"].ToString();

                        bool isPasswordCorrect = VerifyPassword(password, storedHash, storedSalt);
                        if (!isPasswordCorrect)
                            return false;

                        roleCode = dbRoleCode;

                        object v = reader["PASSWORD_CHANGED_AT"];
                        object e = reader["USER_ID"];

                        if (v != null && v != DBNull.Value)
                        {
                            DateTime parsed;
                            if (DateTime.TryParse(v.ToString(), out parsed))
                                passwordChangedAt = parsed;
                        }

                        if (e != null && e != DBNull.Value)
                        {
                            user_id = e.ToString();   
                        }

                        return true;
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
        public static string GenerateHash(string password, string salt)
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
        public static string GenerateSalt()
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

        public bool UpdatePassword(string loginId, string newPassword)
        {
            string passwordSalt = GenerateSalt();
            string passwordHash = GenerateHash(newPassword, passwordSalt);
            
            using (var conn = new SQLiteConnection("Data Source=" + Common.DB_PATH))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = Query.PASSWORD_EDIT;

                    cmd.Parameters.AddWithValue("@hash", passwordHash);
                    cmd.Parameters.AddWithValue("@salt", passwordSalt);
                    cmd.Parameters.AddWithValue("@password_changedDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@loginId", loginId);

                    int result = cmd.ExecuteNonQuery();
                    return result == 1;
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

        #region [ 비밀번호 검증 ]
        /// <summary>
        /// 비밀번호 유효성 검사
        /// </summary>
        /// <returns>유효하면 null, 실패하면 사유 문자열 반환</returns>
        public static string ValidatePassword(string password)
        {
            // 1. 최소 8자 이상
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return "비밀번호는 8자리 이상으로 입력해주세요.";

            // 2. 연속된 숫자 패턴 사용 불가 (예: 1234, 4321)
            for (int i = 0; i < password.Length - 3; i++)
            {
                char c1 = password[i];
                char c2 = password[i + 1];
                char c3 = password[i + 2];
                char c4 = password[i + 3];

                bool ascending = (c2 == c1 + 1) && (c3 == c1 + 2) && (c4 == c1 + 3);
                bool descending = (c2 == c1 - 1) && (c3 == c1 - 2) && (c4 == c1 - 3);

                if (ascending || descending)
                    return "연속된 문자/숫자 패턴은 사용할 수 없습니다. (예: 1234, abcd)";
            }

            // 3. 동일한 문자 4개 연속 사용 불가 (예: aaaa, 1111)
            for (int i = 0; i < password.Length - 3; i++)
            {
                if (password[i] == password[i + 1] &&
                    password[i] == password[i + 2] &&
                    password[i] == password[i + 3])
                    return "동일한 문자를 4개 이상 연속으로 사용할 수 없습니다. (예: aaaa, 1111)";
            }

            // 4. 대문자 / 소문자 / 숫자 / 특수문자 중 3가지 이상 포함
            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

            int categoryCount = (hasUpper ? 1 : 0)
                              + (hasLower ? 1 : 0)
                              + (hasDigit ? 1 : 0)
                              + (hasSpecial ? 1 : 0);

            if (categoryCount < 3)
                return "비밀번호는 대문자, 소문자, 숫자, 특수문자 중 3가지 이상을 포함해야 합니다.";

            // 모든 조건 통과
            return null;
        }
        #endregion

        #region [ 유저이름 및 아이디 검색 ]
        public List<UserModel> SearchUsers(string keyword)
        {
            var list = new List<UserModel>();
            string pattern = "%" + keyword.Trim() + "%";

            using (var conn = new SQLiteConnection("Data Source=" + Common.DB_PATH))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.SEARCH_USERID_NAME, conn))
                {
                    cmd.Parameters.AddWithValue("@keyword", pattern);

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
