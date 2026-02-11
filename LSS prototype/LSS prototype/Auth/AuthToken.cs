using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype.Login_Page
{
    /// <summary>
    /// 로그인 성공 시 세션 및 토큰 생성 클래스 
    /// </summary>
    public static class AuthToken
    {
        // ===== 상태 =====
        public static bool IsAuthenticated { get; private set; }
        public static string Token { get; private set; }
        public static string LoginId { get; private set; }
        public static string RoleCode { get; private set; }
        public static DateTime LastActivity { get; private set; }
        //public static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(10); 추후 세션타임은 정해지면 수정 
        public static readonly TimeSpan SessionTimeout = TimeSpan.FromSeconds(10);  // 10초로 테스트용 

        // ===== 로그인 =====
        public static void SignIn(string loginId, string roleCode)
        {
            IsAuthenticated = true;
            LoginId = loginId;
            RoleCode = roleCode;
            Token = Guid.NewGuid().ToString("N");
            Touch();
        }

        public static void SignOut()
        {
            IsAuthenticated = false;
            Token = null;
            LoginId = null;
            RoleCode = null;
            LastActivity = DateTime.MinValue;
        }

        // ===== 세션 =====
        public static void Touch()
        {
            LastActivity = DateTime.Now;
        }

        public static bool IsExpired()
        {
            if (!IsAuthenticated) return true;
            return DateTime.Now - LastActivity > SessionTimeout;
        }

        // ===== Guard (토큰 유효확인)  =====
        public static bool EnsureAuthenticated()
        {
            if (IsExpired())
            {
                SignOut();
                return false;
            }
            if (string.IsNullOrEmpty(Token))
                return false;

           
            return true;
        }

        public static bool EnsureRole(params string[] roles)
        {
            if (!EnsureAuthenticated())
                return false;
            if (RoleCode == "S")
                return true;
            foreach (var r in roles)
                if (RoleCode == r)
                    return true;
            return false;
        }
    }



}
