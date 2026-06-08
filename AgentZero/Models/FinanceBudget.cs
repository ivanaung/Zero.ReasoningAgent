namespace ScheduleApp.Models;

public class FinanceBudget
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int FinanceCategoryId { get; set; }

    public FinanceCategory? FinanceCategory { get; set; }

    public FinanceScope Scope { get; set; } = FinanceScope.Personal;

    public int Month { get; set; }

    public int Year { get; set; }

    public decimal BudgetAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
