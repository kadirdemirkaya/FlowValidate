namespace FlowValidate.Api.Models
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
