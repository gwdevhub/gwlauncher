using System.Collections.Generic;
using Newtonsoft.Json;
using GWCA.Memory;

namespace GW_Launcher
{
    public class Account
    {

        [JsonRequired]
        public string email;

        [JsonRequired]
        public string password;

        [JsonRequired]
        public string character;

        [JsonRequired]
        public string gwpath;

        public bool datfix;
        public bool elevated;
        public string extraargs;
        public List<Mod> mods;

        [JsonIgnore]
        public bool active;

        [JsonIgnore]
        public GWCAMemory process;
    }
}
