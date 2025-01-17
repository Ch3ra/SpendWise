using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SpendWise.Components.Models
{
    public class AppModel
    {
        public List<UserModel> Users { get; set; } = new();
        public List<Transaction> Transactions { get; set; } = new();

        public class UserModel
        {
            [Key]
            public string UserId { get; set; } = Guid.NewGuid().ToString();
            [Required]
            [MaxLength(100)]
            public string UserName { get; set; } = string.Empty;
            [EmailAddress]
            public string Email { get; set; } = string.Empty;
            [Required]
            public string Password { get; set; } = string.Empty;
        }

        public class Transaction
        {
            [Key]
            public string TransactionId { get; set; } = Guid.NewGuid().ToString();
            [Required]
            public decimal Amount { get; set; }
            [Required]
            public string Label { get; set; } = string.Empty;
            public string? Notes { get; set; }
            [Required]
            public TransactionType TransactionType { get; set; }
            [Required]
            public DateTime TransactionDateTime { get; set; } = DateTime.UtcNow;

            [ForeignKey("UserId")]
            public string UserId { get; set; }
            public string UserName { get; set; }

            // New fields for debt management
            public DateTime? DueDate { get; set; } // Optional due date for debts
            public bool IsCleared { get; set; } = true; // Tracks if the debt is cleared (default: true)
        }


        public enum TransactionType
        {
            Credit,
            Debit,
            Debt
        }
    }
}
