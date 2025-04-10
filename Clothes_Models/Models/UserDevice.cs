using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.Models
{
    public class UserDevice
    {
        public Guid? Id { get; set; } = Guid.NewGuid();

        [ForeignKey("User")]
        public string? UserId { get; set; }
        public string? DeviceName { get; set; }
        public string? DeviceType { get; set; }
        public string? OS { get; set; }
        public string? Browser { get; set; }
        public string? IpAddress { get; set; }
        public string? Location { get; set; }
        public DateTime? FirstLoginDate { get; set; } = DateTime.Now;
        public DateTime? LastLoginDate { get; set; } = DateTime.Now;
        public string? DeviceToken { get; set; }
        public virtual ApplicationUser? User { get; set; }
    }
}
