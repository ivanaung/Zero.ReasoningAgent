namespace ScheduleApp.Models;

public class OperationalFinanceTransaction
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int FinanceAccountId { get; set; }
    public int FinanceCategoryId { get; set; }
    public int? ProjectId { get; set; }
    public FinanceTransactionType Type { get; set; } = FinanceTransactionType.Expense;
    public FinanceScope Scope { get; set; } = FinanceScope.Personal;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.Today;
    public string? Description { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
