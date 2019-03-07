using System.Runtime.Serialization;

namespace SoundControl.Models
{
    [DataContract]
    public class Device
    {
        [DataMember]
        public string Location { get; set; }
        [DataMember]
        public string ManufacturerGuid { get; set; }
        [DataMember]
        public string NameGuid { get; set; }
        [DataMember]
        public string ProductGuid { get; set; }
        [DataMember]
        public string ProductName { get; set; }

    }
}