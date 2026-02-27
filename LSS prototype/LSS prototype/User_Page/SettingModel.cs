using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype.User_Page
{
    /// <summary>
    /// PASC SETTING CLASS
    /// </summary>
    public class SettingModel
    {
        public string HospitalName { get; set; }

        public string CStoreAET { get; set; }
        public string CStoreIP { get; set; }
        public int CStorePort { get; set; }
        public string CStoreMyAET { get; set; }

        public string MwlAET { get; set; }
        public string MwlIP { get; set; }
        public int MwlPort { get; set; }
        public string MwlMyAET { get; set; }
    }
}
