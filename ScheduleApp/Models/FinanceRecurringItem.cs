using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public class FinanceRecurringItem
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string UserId { get; set; } = string.Empty;

    public int? FinanceAccountId { get; set; }

    public FinanceAccount? FinanceAccount { get; set; }

    public int? FinanceCategoryId { get; set; }

    public FinanceCategory? FinanceCategory { get; set; }

    public int? ProjectId { get; set; }

    public Project? Project { get; set; }

    public FinanceCategoryType Type { get; set; } = FinanceCategoryType.Expense;

    public FinanceScope Scope { get; set; } = FinanceScope.Personal;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public FinanceRecurringFrequency Frequency { get; set; } = FinanceRecurringFrequency.Monthly;

    public DateTime NextDueDate { get; set; } = DateTime.Today;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
