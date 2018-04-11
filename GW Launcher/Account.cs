using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using GWCA.Memory;

namespace GW_Launcher
{
    public class Account
    {
        public string email;
        public string password;
        public string character;
        public string gwpath;
        public bool datfix;
        public string extraargs;

        [JsonIgnore]
        public bool active;

        [JsonIgnore]
        public GWCAMemory process;
    }
}
