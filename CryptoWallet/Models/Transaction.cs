namespace CryptoWallet.Models
{
    public class Transaction
    {
        public int Id { get; set; }
        public string CryptoCode { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public decimal CryptoAmount { get; set; }
        public decimal Money { get; set; }
        public DateTime DateTime { get; set; }
    }
}