namespace FlowValidate.Api.Models
{
    public class User
    {
        public string Name { get; set; } = null!;
        public int Age { get; set; }

        public DateTime? PastTime { get; set; }
        public List<string> Emails { get; set; } = null!;
        public string Email { get; set; } = null!;


        public UserCustomer UserCustomer { get; set; } = null!;

        public List<UserBasket> UserBaskets { get; set; } = null!;
    }
}
