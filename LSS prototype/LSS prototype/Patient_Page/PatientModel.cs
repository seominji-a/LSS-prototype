using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype
{
    public class PatientModel
    {
        public string PatientId { get; set; }
        public int PatientCode { get; set; }
        public string Name { get; set; }

        public DateTime BRITH_DATE { get; set; }

        public char Sex { get; set; }

        public DateTime Reg_Date { get; set; }
    }
}
