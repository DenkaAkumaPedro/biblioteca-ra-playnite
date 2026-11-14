using System.Collections.Generic;
using Playnite.SDK.Data;

namespace BibliotecaRA.Models
{
    public class RaSettings : ObservableObject
    {
        private string apiKey = string.Empty;
        private string username = string.Empty;
        private List<string> romFolders = new List<string>();
        private string romExtensions =
            ".md, .bin, .gen, .nes, .fds, .sfc, .smc, .gba, .gb, .gbc, .gg, .sms, .a26, .j64, .n64, .z64, .v64, .iso, .cue, .chd, .pbp, .cso, .7z, .zip";
        private int cacheTtlHours = 168;
        private bool detectInstalled = true;
        private bool syncActivity = true;
        private bool syncPlaytime = true;

        public string ApiKey
        {
            get => apiKey;
            set => SetValue(ref apiKey, value);
        }

        public string Username
        {
            get => username;
            set => SetValue(ref username, value);
        }

        public List<string> RomFolders
        {
            get => romFolders;
            set => SetValue(ref romFolders, value);
        }

        public string RomExtensions
        {
            get => romExtensions;
            set => SetValue(ref romExtensions, value);
        }

        public int CacheTtlHours
        {
            get => cacheTtlHours;
            set => SetValue(ref cacheTtlHours, value);
        }

        public bool DetectInstalled
        {
            get => detectInstalled;
            set => SetValue(ref detectInstalled, value);
        }

        public bool SyncActivity
        {
            get => syncActivity;
            set => SetValue(ref syncActivity, value);
        }

        public bool SyncPlaytime
        {
            get => syncPlaytime;
            set => SetValue(ref syncPlaytime, value);
        }
    }
}
