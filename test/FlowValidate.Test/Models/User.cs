using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowValidate.Test.Models
{
    public class User
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public DateTime? PastTime { get; set; }
        public string Email { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public string? Nickname { get; set; }

        public UserDetails? UserDetails { get; set; }

        public List<UserBasket>? UserBaskets { get; set; } = new();
    }
}
