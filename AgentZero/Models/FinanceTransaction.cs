using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public class FinanceTransaction
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string UserId { get; set; } = string.Empty;

    public int FinanceAccountId { get; set; }

    public FinanceAccount? FinanceAccount { get; set; }

    public int FinanceCategoryId { get; set; }

    public FinanceCategory? FinanceCategory { get; set; }

    public int? ProjectId { get; set; }

    public Project? Project { get; set; }

    public FinanceTransactionType Type { get; set; } = FinanceTransactionType.Expense;

    public FinanceScope Scope { get; set; } = FinanceScope.Personal;

    public decimal Amount { get; set; }

    public DateTime TransactionDate { get; set; } = DateTime.Today;

    [MaxLength(250)]
    public string? Description { get; set; }

    [MaxLength(80)]
    public string? PaymentMethod { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
