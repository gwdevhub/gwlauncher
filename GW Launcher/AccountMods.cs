using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GW_Launcher
{
    public enum ModType
    {
        kModTypeTexmod,
        kModTypeDLL
    }

    public class Mod
    {
        public ModType type;
        public string fileName;
        public bool active;
    }
}
