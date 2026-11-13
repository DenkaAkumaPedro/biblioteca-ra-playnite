using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace BibliotecaRA.Models
{
    public class RaConsole
    {
        [JsonProperty("ID")]
        public int Id { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("IconURL")]
        public string IconUrl { get; set; }

        [JsonProperty("Active")]
        public bool Active { get; set; }

        [JsonProperty("IsGameSystem")]
        public bool IsGameSystem { get; set; }
    }

    public class RaGameListItem
    {
        [JsonProperty("Title")]
        public string Title { get; set; }

        [JsonProperty("ID")]
        public int Id { get; set; }

        [JsonProperty("ConsoleID")]
        public int ConsoleId { get; set; }

        [JsonProperty("ConsoleName")]
        public string ConsoleName { get; set; }

        [JsonProperty("ImageIcon")]
        public string ImageIcon { get; set; }

        [JsonProperty("NumAchievements")]
        public int NumAchievements { get; set; }

        [JsonProperty("Points")]
        public int Points { get; set; }

        [JsonProperty("Hashes")]
        public List<string> Hashes { get; set; } = new List<string>();
    }

    public class RaCatalogCache
    {
        [JsonProperty("SavedAtUtc")]
        public DateTime SavedAtUtc { get; set; }

        [JsonProperty("Consoles")]
        public List<RaConsole> Consoles { get; set; } = new List<RaConsole>();

        [JsonProperty("GamesByConsole")]
        public Dictionary<string, List<RaGameListItem>> GamesByConsole { get; set; }
            = new Dictionary<string, List<RaGameListItem>>();
    }
}
