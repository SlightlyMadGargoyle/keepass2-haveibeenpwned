﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using KeePassLib;
using System.Net.Http;
using Newtonsoft.Json;
using KeePass.Forms;
using KeePass.Plugins;
using System.Threading.Tasks;

namespace HaveIBeenPwned
{
    public class HaveIBeenPwnedChecker : BaseChecker
    {
        public HaveIBeenPwnedChecker(PwDatabase database, HttpClient httpClient, IPluginHost pluginHost)
            : base(database, httpClient, pluginHost)
        {
        }

        public async override void CheckDatabase(bool expireEntries, bool oldEntriesOnly)
        {
            bool breachesFound = false;
            var breaches = await GetBreaches();
            var entries = passwordDatabase.RootGroup.GetEntries(true);
            StatusProgressForm progressForm = new StatusProgressForm();

            progressForm.InitEx("Checking Cloudbleed Breaches", true, false, pluginHost.MainWindow);
            progressForm.Show();
            progressForm.SetProgress(0);
            uint counter = 0;
            var entryCount = entries.Count();
            foreach (var entry in entries)
            {
                progressForm.SetProgress((uint)((double)counter / entryCount * 100));
                var url = entry.Strings.ReadSafe(PwDefs.UrlField).ToLower();
                progressForm.SetText(string.Format("Checking {0} for breaches", url), KeePassLib.Interfaces.LogStatusType.Info);
                var userName = entry.Strings.ReadSafe(PwDefs.UserNameField);
                var lastModified = entry.LastModificationTime;
                if(!string.IsNullOrEmpty(url))
                {
                    var domainBreaches = breaches.Where(b => !string.IsNullOrWhiteSpace(b.Domain) && url.Contains(b.Domain) && (!oldEntriesOnly || lastModified < b.BreachDate)).OrderBy(b => b.BreachDate);
                    if (domainBreaches.Any())
                    {
                        breachesFound = true;
                        MessageBox.Show(string.Format("Potentially pwned account details for: {0}\r\nBreached on: {1}\r\nThis entry was last modified on: {2}", url, domainBreaches.Last().BreachDate, lastModified), Resources.MessageTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        if(expireEntries)
                        {
                            ExpireEntry(entry);
                        }
                    }
                }
                counter++;
                if (progressForm.UserCancelled)
                {
                    break;
                }
            }
            progressForm.Hide();
            progressForm.Close();
            if (!breachesFound)
            {
                MessageBox.Show("No breached domains found.", Resources.MessageTitle);
            }
        }

        private async Task<List<HaveIBeenPwnedEntry>> GetBreaches()
        {
            StatusProgressForm progressForm = new StatusProgressForm();

            progressForm.InitEx("Downloading Have I Been Pwned? Breach List", true, false, pluginHost.MainWindow);
            progressForm.Show();
            progressForm.SetProgress(0);
            List<HaveIBeenPwnedEntry> breaches = null;
            HttpResponseMessage response = await client.GetAsync(new Uri("https://haveibeenpwned.com/api/v2/breaches"));
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                breaches = JsonConvert.DeserializeObject<List<HaveIBeenPwnedEntry>>(jsonString);
            }
            else
            {
                MessageBox.Show(string.Format("Unable to check haveibeenpwned.com (returned Status: {0})", response.StatusCode), Resources.MessageTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            progressForm.SetProgress(100);

            progressForm.Hide();
            progressForm.Close();
            return breaches;
        }
    }
}
