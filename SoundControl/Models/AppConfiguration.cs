using System.Collections.Generic;

namespace SoundControl.Models
{
    public class AppConfiguration
    {
        //path to Sound Files
        public string MediaPath { get; set; }
        //path to logs catalogue
        public string LogPath { get; set; }
        //path to Alert files
        public string AlertPath { get; set; }
        //Names of audio outputs assigned to rooms
        public List<string> RoomNames { get; set; }
        //List of alerts
        public List<Alert> Alerts { get; set; }
    }

    public class Alert
    {
        public string Name { get; set; }
        public string FileName { get; set; }
    }
}