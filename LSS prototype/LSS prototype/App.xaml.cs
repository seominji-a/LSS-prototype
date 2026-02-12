
using LSS_prototype.Auth;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace LSS_prototype
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        public static SessionActivityMonitor ActivityMonitor { get; } = new SessionActivityMonitor();
    }
}
