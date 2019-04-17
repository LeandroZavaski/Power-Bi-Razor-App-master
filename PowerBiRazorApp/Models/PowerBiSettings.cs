using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PowerBiRazorApp.Models
{
    public class PowerBiSettings
    {
        public string MainAddress { get; set; }
        public string ResourceAddress { get; set; }
        public string MasterUser { get; set; }
        public string MasterKey { get; set; }
        public string GroupId { get; set; }
    }
}
