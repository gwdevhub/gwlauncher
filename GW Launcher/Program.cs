using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using System.Diagnostics;
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
        public bool encryptAccounts;
        public bool decryptAccounts;

        GlobalSettings()
        {
            encryptAccounts = false;
            decryptAccounts = false;
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

    static class Program
    {

        const string gwlMutexName = "gwl_instance_mutex";
        public static AccountManager accounts;
        public static Thread mainthread;
        public static Mutex mutex = new Mutex();
        public static Mutex gwlMutex;
        public static GlobalSettings settings;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// 
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        extern static IntPtr FindWindowW(string clsname, string winname);

        [DllImport("user32.dll")]
        extern static bool SetForegroundWindow(IntPtr hWNd);

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (Mutex.TryOpenExisting(gwlMutexName, out gwlMutex))
            {
                return;
            }
            else
            {
                gwlMutex = new Mutex(true, gwlMutexName);
            }

            settings =  GlobalSettings.Load();


            accounts = new AccountManager("Accounts.json");
            for (int i = 0; i < accounts.Length; ++i)
            {
                accounts[i].active = false;
            }

            MainForm mf = new MainForm();
            mf.Location = new System.Drawing.Point(-1000, -1000);
            mf.FormClosing += (object sender, FormClosingEventArgs e) =>
            {
                Program.settings.Save();
            };

            mainthread = new Thread(() =>
            {
                bool main_closed = false;
                mf.FormClosed += (s, a) => { main_closed = true; };
                while (!main_closed)
                {
                    int sleep = 5000;
                    while (mf.needtolaunch.Count > 0)
                    {
                        int i = mf.needtolaunch.Dequeue();
                        bool ok = true;
                        Account a = accounts[i];
                        GWCAMemory m = MulticlientPatch.LaunchClient(a.gwpath, " -email \"" + a.email + "\" -password \"" + a.password + "\" -character \"" + a.character + "\" " + a.extraargs, a.datfix, false, a.mods);

                        uint timelock = 0;
                        while(ok && m.process.MainWindowHandle == IntPtr.Zero) {
                            Thread.Sleep(1000);
                            timelock += 1;
                            if(timelock > 10) {
                                ok = false;
                                break;
                            }
                        }
                        if (!ok) continue;
                        a.process = m;
                        //m.WriteWString(GWMem.WinTitle, a.character + '\0');

                        mf.SetActive(i, true);
                        timelock = 0;
                        GWMem.FindAddressesIfNeeded(m);
                        while (ok && m.Read<ushort>(GWMem.CharnamePtr) == 0 && timelock < 60)
                        {
                            Thread.Sleep(1000);
                            timelock += 1;
                        }

                        Thread.Sleep(sleep);
                        sleep += 5000;
                    }


                    mutex.WaitOne();

                    for(int i = 0; i < accounts.Length; ++i)
                    {
                        if (accounts[i].active)
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
