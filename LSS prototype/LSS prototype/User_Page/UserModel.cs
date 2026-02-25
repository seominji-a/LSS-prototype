using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype.User_Page
{
    public class UserModel
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string UserCode { get; set; }
        public string Role { get; set; }
        public string Department { get; set; }
    }
}
