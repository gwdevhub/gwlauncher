using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using System.Diagnostics;
using System.DirectoryServices;
using System.Runtime.InteropServices;
using System.Threading;
using GWCA.Memory;
using System.IO;
using Newtonsoft.Json;
using GWMC_CS;
using ThreadState = System.Diagnostics.ThreadState;

namespace GW_Launcher
{
    public class GlobalSettings
    {
        public bool EncryptAccounts { get; }
        public bool DecryptAccounts { get; }

        GlobalSettings()
        {
            EncryptAccounts = false;
            DecryptAccounts = false;
        }

        public void Save(string path = "Settings.json")
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this,Formatting.Indented));
        }

        public static GlobalSettings Load(string path = "Settings.json")
        {   
            try
            {
                return JsonConvert.DeserializeObject<GlobalSettings>(File.ReadAllText(path));
            }
            catch (FileNotFoundException)
            {
                return new GlobalSettings();
            }
            
        }
    }

    internal static class Program
    {
        private const string GwlMutexName = "gwl_instance_mutex";
        public static AccountManager accounts;
        public static Thread mainthread;
        public static Mutex mutex = new Mutex();
        public static Mutex gwlMutex;
        public static GlobalSettings settings;
        
        [STAThread]
        internal static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (Mutex.TryOpenExisting(GwlMutexName, out gwlMutex))
            {
                return;
            }

            gwlMutex = new Mutex(true, GwlMutexName);

            settings =  GlobalSettings.Load();


            accounts = new AccountManager("Accounts.json");
            foreach (Account t in accounts)
            {
                t.active = false;
            }

            using (var mf = new MainForm())
            {
                mf.Location = new System.Drawing.Point(-1000, -1000);
                mf.FormClosing += (object sender, FormClosingEventArgs e) => { Program.settings.Save(); };

                mainthread = new Thread(() =>
                {
                    var mainClosed = false;
                    mf.FormClosed += (s, a) => { mainClosed = true; };
                    while (!mainClosed)
                    {
                        var sleep = 5000;
                        while (mf.needtolaunch.Count > 0)
                        {
                            int i = mf.needtolaunch.Dequeue();
                            var ok = true;
                            Account a = accounts[i];
                            GWCAMemory m = MulticlientPatch.LaunchClient(a.gwpath,
                                " -email \"" + a.email + "\" -password \"" + a.password + "\" -character \"" +
                                a.character + "\" " + a.extraargs, a.datfix, false, a.elevated, a.mods);

                            uint timelock = 0;
                            while (m.process.MainWindowHandle == IntPtr.Zero)
                            {
                                Thread.Sleep(1000);
                                timelock += 1;
                                if (timelock <= 10) continue;
                                ok = false;
                                break;
                            }

                            if (!ok) continue;
                            a.process = m;

                            mf.SetActive(i, true);
                            timelock = 0;
                            GWMem.FindAddressesIfNeeded(m);
                            while (m.Read<ushort>(GWMem.CharnamePtr) == 0 && timelock < 60)
                            {
                                Thread.Sleep(1000);
                                timelock += 1;
                            }
                            Thread.Sleep(sleep);
                            sleep += 5000;
                        }

                        mutex.WaitOne();

                        for (var i = 0; i < accounts.Length; ++i)
                        {
                            if (!accounts[i].active) continue;
                            if (accounts[i].process.process.HasExited)
                            {
                                mf.SetActive(i, false);
                            }
                        }

                        mutex.ReleaseMutex();

                        Thread.Sleep(150);
                    }
                });
                Application.Run(mf);

                mainthread.Abort();
            }
        }
        
    }
}
