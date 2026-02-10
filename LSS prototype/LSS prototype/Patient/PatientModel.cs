using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype
{
    internal class PatientModel
    {
       
    }


    public class PatientRepository
    {
        public List<Patient> GetPatients()
        {
            return new List<Patient>
            {
                new Patient { PatientId = "P001", Name = "홍길동" }
            };
        }
    }
}
