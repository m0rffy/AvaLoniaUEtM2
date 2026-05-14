using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UETM2;

public class AppData
{
    public Dictionary<string, string> Passwords { get; set; } = new();
    public List<DeviceInfo> Devices { get; set; } = new();
}
