using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype.User_Page
{
    public class UserModel
    {
        public int UserId { get; set; }       // USER_ID ( 사용자마다 자동으로 부여되는 고유한 ID ) 
        public string LoginId { get; set; }   // LOGIN_ID ( 실제로쓰는 사용자의 ID ) 
        public string UserName { get; set; }  // USER_NAME
        public string UserRole { get; set; }  // USER_ROLE
        public string RoleCode { get; set; }  // ROLE_CODE
    }
}
