using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;


namespace GW_Launcher
{
    public class AccountManager : IEnumerable<Account>
    {
        private Account[] accounts;
        private string filePath;

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
            if(filePath == null && this.filePath != null)
            {
                filePath = this.filePath;
            }
            string text = File.ReadAllText(filePath);
            accounts = JsonConvert.DeserializeObject<Account[]>(text);
        }

        public void Save(string filePath = null)
        {
            if (filePath == null && this.filePath != null)
            {
                filePath = this.filePath;
            }
            string text = JsonConvert.SerializeObject(accounts, Formatting.Indented);
            File.WriteAllText(filePath, text);
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
            return (IEnumerator<Account>)accounts.GetEnumerator();
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
