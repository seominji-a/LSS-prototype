using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype.User_Page
{
    public class DefaultModel
    {
        public double ExposureTime { get; set; }
        public double Gain { get; set; }
        public double Gamma { get; set; }
        public double Focus { get; set; } 
        public double Iris { get; set; } 
        public int Zoom { get; set; }
        public int Filter { get; set; }
    }
}
