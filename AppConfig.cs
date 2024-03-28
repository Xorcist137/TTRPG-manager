﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace TTRPG_manager
{
    public class AppConfig
    {


        // Removed the standalone Resolution and BackgroundPath properties.

        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>()
    {
        // Prepopulate the dictionary with default values for Resolution and BackgroundPath.
        {"Resolution", "1280x720"},
        {"BackgroundPath", ""},
        {"LibraryPaths", "" }
    };

        public int selectedPartyIndex { get; set; } = 0;
        
        public ObservableCollection<Party> Parties { get; set; } = new ObservableCollection<Party>();

        public ObservableCollection<Item> Items { get; set; } = new ObservableCollection<Item>();

        public ObservableCollection<Skill> Skills { get; set; } = new ObservableCollection<Skill>();

        public ObservableCollection<Character> Characters { get; set; } = new ObservableCollection<Character>();

        public ObservableCollection<StatusEffect> StatusEffects { get; set; } = new ObservableCollection<StatusEffect>();

        // Convenience methods to access specific settings easily
        [JsonIgnore]
        public string Resolution
        {
            get => Settings.TryGetValue("Resolution", out var resolution) ? resolution.ToString() : "1280x720";
            set => Settings["Resolution"] = value;
        }
        [JsonIgnore]
        public string BackgroundPath
        {
            get => Settings.TryGetValue("BackgroundPath", out var backgroundPath) ? backgroundPath.ToString() : "";
            set => Settings["BackgroundPath"] = value;
        }
        [JsonIgnore]
        public string LibraryPaths
        {
            get => Settings.TryGetValue("LibraryPaths", out var librarypaths) ? librarypaths.ToString() : "";
            set => Settings["LibraryPaths"] = value;
        }
        

    }
}
