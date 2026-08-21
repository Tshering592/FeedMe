using System;
using System.ComponentModel.DataAnnotations;

namespace FeedMe.Models
{
    public enum TransactionType
    {
        TopUp,      
        Purchase,   
        Refund,
        VaultDeposit
    }

    public class TokenTransaction
    {
        [Key]
        public int Id { get; set; }

        public string SenderUserId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;

        public string ReceiverUserId { get; set; } = string.Empty;
        public string ReceiverName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public TransactionType Type { get; set; }

        public string Description { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}