using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using Logos.Utility.Security.Cryptography;

namespace GW_Launcher
{
    public class AccountManager : IEnumerable<Account>, IDisposable
    {
        private List<Account> accounts;
        private readonly string _filePath;
        private byte[] _cryptPass = null;
        private readonly byte[] _salsaIv = { 0xc8, 0x93, 0x48, 0x45, 0xcf, 0xa0, 0xfa, 0x85 };
        private readonly Salsa20 _crypt = new();
        public int Length => accounts.Count;


        public AccountManager(string? filePath = null)
        {
            accounts = new List<Account>();
            if (filePath == null) return;
            _filePath = filePath;
            Load(filePath);
        }
        
        public void Load(string filePath = null)
        {
            if (_cryptPass == null && Program.settings.Encrypt)
            {
                var form = new Forms.CryptPassForm();
                form.ShowDialog();
                _cryptPass = form.Password;
            }

            if (filePath == null && _filePath != null)
            {
                filePath = _filePath;
            }

            if (!Program.settings.Encrypt)
            {
                try
                {
                    var text = File.ReadAllText(filePath);
                    accounts = JsonConvert.DeserializeObject<List<Account>>(text);
                }
                catch (FileNotFoundException e)
                {

                    // silent
                    File.WriteAllText(e.FileName, "[]");
                    accounts.Clear();
                }
            }
            else
            {

                try
                {
                    var textBytes = File.ReadAllBytes(filePath);
                    var cryptBytes = new byte[textBytes.Length];
                    using (var decrypt = _crypt.CreateDecryptor(_cryptPass, _salsaIv))
                        decrypt.TransformBlock(textBytes, 0, textBytes.Length, cryptBytes, 0);
                    var rawJson = Encoding.UTF8.GetString(cryptBytes);
                    if (!rawJson.StartsWith("SHIT"))
                    {
                        System.Windows.Forms.MessageBox.Show("Incorrect password.\n Restart launcher and try again.", "GW Launcher - Invalid Password");
                    }
                    accounts = JsonConvert.DeserializeObject<List<Account>>(rawJson.Substring(4));
                }
                catch (FileNotFoundException)
                {

                    // silent
                    var bytes = Encoding.UTF8.GetBytes("SHIT[]");
                    var cryptBytes = new byte[bytes.Length];
                    using (var encrypt = _crypt.CreateEncryptor(_cryptPass, _salsaIv))
                        encrypt.TransformBlock(bytes, 0, bytes.Length, cryptBytes, 0);
                    File.WriteAllBytes(filePath, cryptBytes);
                    accounts.Clear();
                }
            }

        }

        public void Save(string filePath = null)
        {
            if (filePath == null && _filePath != null)
            {
                filePath = _filePath;
            }

            var text = JsonConvert.SerializeObject(accounts, Formatting.Indented);
            if (!Program.settings.Encrypt)
            {
                File.WriteAllText(filePath, text);
            }
            else
            {
                text = "SHIT" + text;
                var bytes = Encoding.UTF8.GetBytes(text);
                var cryptBytes = new byte[bytes.Length];
                using (var encrypt = _crypt.CreateEncryptor(_cryptPass, _salsaIv))
                    encrypt.TransformBlock(bytes, 0, bytes.Length, cryptBytes, 0);
                File.WriteAllBytes(filePath, cryptBytes);
            }
        }

        public void Add(Account acc)
        {
            accounts.Add(acc);
            if (_filePath != null)
            {
                Save(_filePath);
            }
        }

        public void Remove(int index)
        {
            accounts.RemoveAt(index);
            if (_filePath != null)
            {
                Save(_filePath);
            }
        }

        public void Remove(string email)
        {
            for (var i = 0; i < accounts.Count; i++)
            {
                if (accounts[i].email != email) continue;
                Remove(i);
                return;
            }
        }

        public int GetIndexOf(string email)
        {
            for (var i = 0; i < accounts.Count; i++)
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
            get => accounts[index];
            set
            {
                accounts[index] = value;
                if (_filePath != null)
                {
                    Save(_filePath);
                }
            }
        }

        public Account this[string email]
        {
            get
            {
                var index = GetIndexOf(email);
                return index == -1 ? null : this[index];
            }
            set
            {
                var index = GetIndexOf(email);
                if (index != -1)
                   this[index] = value;
            }
        }

        void IDisposable.Dispose()
        {
            _crypt.Dispose();
        }
    }
}
