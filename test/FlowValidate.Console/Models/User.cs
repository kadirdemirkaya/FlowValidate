using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowValidate.Console.Models
{
    public class User
    {
        public string Name { get; set; }
        public int Age { get; set; }

        public DateTime? PastTime { get; set; }
        public List<string> Emails { get; set; }
        public string Email { get; set; }


        public UserCustomer UserCustomer { get; set; }

        public List<UserBasket> UserBaskets { get; set; }
    }
}
