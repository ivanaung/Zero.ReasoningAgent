using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public class FinanceCategory
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public FinanceCategoryType Type { get; set; } = FinanceCategoryType.Expense;

    public FinanceScope Scope { get; set; } = FinanceScope.Personal;

    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
