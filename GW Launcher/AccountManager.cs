using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using Logos.Utility.Security.Cryptography;

namespace GW_Launcher
{
    public class AccountManager : IEnumerable<Account>
    {
        private Account[] accounts;
        private string filePath;
        private byte[] cryptPass = null;
        private readonly byte[] salsaIV = { 0xc8, 0x93, 0x48, 0x45, 0xcf, 0xa0, 0xfa, 0x85 };
        private Salsa20 crypt = new Salsa20();
        public int Length => accounts.Length;


        public AccountManager(string filePath = null)
        {
            if (filePath != null)
            {
                this.filePath = filePath;
                this.Load(filePath);
            }
        }
        
        public void Load(string filePath = null)
        {
            if (cryptPass == null && (Program.settings.encryptAccounts || Program.settings.decryptAccounts))
            {
                Forms.CryptPassForm form = new Forms.CryptPassForm();
                form.ShowDialog();
                cryptPass = form.Password;
            }

            if (filePath == null && this.filePath != null)
            {
                filePath = this.filePath;
            }

            if (!Program.settings.decryptAccounts)
            {
                try
                {
                    string text = File.ReadAllText(filePath);
                    accounts = JsonConvert.DeserializeObject<Account[]>(text);
                }
                catch (FileNotFoundException e)
                {

                    // silent
                    File.WriteAllText(e.FileName, "[]");
                    accounts = new Account[0];
                }
            }
            else
            {

                try
                {
                    byte[] textBytes = File.ReadAllBytes(filePath);
                    byte[] cryptBytes = new byte[textBytes.Length];
                    using (var decrypt = crypt.CreateDecryptor(cryptPass, salsaIV))
                        decrypt.TransformBlock(textBytes, 0, textBytes.Length, cryptBytes, 0);
                    string rawJson = Encoding.UTF8.GetString(cryptBytes);
                    if (!rawJson.StartsWith("SHIT"))
                    {
                        System.Windows.Forms.MessageBox.Show("Doesn't look like the inputted password is it bud.\n Restart launcher and try again.", "GW Launcher - Invalid Password");
                    }
                    accounts = JsonConvert.DeserializeObject<Account[]>(rawJson.Substring(4));
                }
                catch (FileNotFoundException e)
                {

                    // silent
                    byte[] bytes = Encoding.UTF8.GetBytes("SHIT[]");
                    byte[] cryptBytes = new byte[bytes.Length];
                    using (var encrypt = crypt.CreateEncryptor(cryptPass, salsaIV))
                        encrypt.TransformBlock(bytes, 0, bytes.Length, cryptBytes, 0);
                    File.WriteAllBytes(filePath, cryptBytes);
                    accounts = new Account[0];
                }
            }

        }

        public void Save(string filePath = null)
        {
            if (filePath == null && this.filePath != null)
            {
                filePath = this.filePath;
            }

            string text = JsonConvert.SerializeObject(accounts, Formatting.Indented);
            if (!Program.settings.encryptAccounts)
            {
                File.WriteAllText(filePath, text);
            }
            else
            {
                text = "SHIT" + text;
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                byte[] cryptBytes = new byte[bytes.Length];
                using (var encrypt = crypt.CreateEncryptor(cryptPass, salsaIV))
                    encrypt.TransformBlock(bytes, 0, bytes.Length, cryptBytes, 0);
                File.WriteAllBytes(filePath, cryptBytes);
            }
        }

        public void Add(Account acc)
        {
            Account[] newAccounts = new Account[accounts.Length+1];
            accounts.CopyTo(newAccounts, 0);
            newAccounts[accounts.Length] = acc;
            accounts = newAccounts;
            if (filePath != null)
            {
                Save(filePath);
            }
        }

        public void Remove(int index)
        {
            Account[] newAccounts = new Account[accounts.Length - 1];

            for (int i = 0, j = 0; i < accounts.Length; i++, j++)
            {
                if (index == i) i++;

                newAccounts[j] = accounts[i];
            }
            accounts = newAccounts;
            if (filePath != null)
            {
                Save(filePath);
            }
        }

        public void Remove(string email)
        {
            for(int i = 0; i < accounts.Length; i++)
            {
                if (accounts[i].email == email)
                {
                    Remove(i);
                    return;
                }
            }
        }

        public int GetIndexOf(string email)
        {
            for (int i = 0; i < accounts.Length; i++)
            {
                if (accounts[i].email == email)
                {
                    return i;
                }
            }
            return -1;
        }

        IEnumerator<Account> IEnumerable<Account>.GetEnumerator()
        {
            return ((IEnumerable<Account>)accounts).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return accounts.GetEnumerator();
        }

        public Account this[int index]
        {
            get
            {
                return this.accounts[index];
            }
            set
            {
                accounts[index] = value;
                if(filePath != null)
                {
                    Save(filePath);
                }
            }
        }

        public Account this[string email]
        {
            get
            {
                int index = GetIndexOf(email);
                if (index == -1)
                    return null;
                else
                    return this[index];
            }
            set
            {
                int index = GetIndexOf(email);
                if (index != -1)
                   this[index] = value;
            }
        }

    }
}
