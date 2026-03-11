using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype.Scan_Page
{
    public class ScanModel
    {
        public List<Mat> CapturedImages { get; set; } = new List<Mat>();
    }
}
