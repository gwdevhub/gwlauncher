using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace GW_Launcher.Forms
{

    public struct GitHubAssetUploader
    {
        public string login;
        public uint id;
        public string node_id;
        public string avatar_url;
        public string gravatar_id;
        public string url;
        public string html_url;
        public string followers_url;
        public string following_url;
        public string gists_url;
        public string starred_url;
        public string subscriptions_url;
        public string organizations_url;
        public string repos_url;
        public string events_url;
        public string received_events_url;
        public string type;
        public string site_admin;
    }

    public struct GitHubAsset
    {
        public string url;
        public uint id;
        public string node_id;
        public string name;
        public string label;
        public GitHubAssetUploader uploader;
        public string content_type;
        public string state;
        public uint size;
        public uint download_count;
        public string created_at;
        public string updated_at;
        public string browser_download_url;
    }

    public struct GitHubAuthor
    {
        public string login;
        public uint id;
        public string node_id;
        public string avatar_url;
        public string gravatar_id;
        public string url;
        public string html_url;
        public string followers_url;
        public string following_url;
        public string gists_url;
        public string starred_url;
        public string subscriptions_url;
        public string organizations_url;
        public string repos_url;
        public string events_url;
        public string received_events_url;
        public string type;
        public bool site_admin;
    }

    public struct GitHubRelease
    {
        public string url;
        public string assets_url;
        public string upload_url;
        public string html_url;
        public uint id;
        public string node_id;
        public string tag_name;
        public string target_commitish;
        public string name;
        public bool draft;
        public GitHubAuthor author;
        public bool prerelease;
        public string created_at;
        public string published_at;
        public GitHubAsset[] assets;
        public string tarball_url;
        public string zipball_url;
        public string body;
    }

    public partial class AutoUpdateForm : Form
    {
        const string THIS_RELEASE_TAG = "r10";
        const string GITHUB_RELEASE_URL = "https://api.github.com/repos/GregLando113/GWLauncher/releases/latest";
        GitHubRelease gitHubRelease;

        HttpClient client = new HttpClient();


        async public Task<bool> requireUpdate()
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            string str = await client.GetStringAsync(GITHUB_RELEASE_URL);
            gitHubRelease = (GitHubRelease)JsonConvert.DeserializeObject(str);

            return gitHubRelease.tag_name == THIS_RELEASE_TAG;
        }

        public AutoUpdateForm()
        {
            InitializeComponent();
        }

        private void AutoUpdateForm_Load(object sender, EventArgs e)
        {
            
        }
    }
}
