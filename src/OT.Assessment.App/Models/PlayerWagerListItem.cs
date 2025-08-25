namespace OT.Assessment.App.Models
{
    public class PlayerWagerListItem
    {
        public Guid WagerId { get; set; }
        public Guid AccountId { get; set; }
        public string Game { get; set; } = "";
        public string Provider { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
    }
}
