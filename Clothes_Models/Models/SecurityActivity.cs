using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.Models
{
    public class SecurityActivity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; }
        public string ActivityType { get; set; }
        public string Description { get; set; }
        public DateTime ActivityDate { get; set; } = DateTime.Now;
        public string IpAddress { get; set; }
    }
}
