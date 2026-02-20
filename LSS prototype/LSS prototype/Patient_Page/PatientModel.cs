using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype.Patient_Page
{
    public class PatientModel
    {
        public int PatientId { get; set; }
        public int PatientCode { get; set; }
        public string Name { get; set; }

        public DateTime BRITH_DATE { get; set; }

        public string Sex { get; set; }

        public DateTime Reg_Date { get; set; }
    }
}
