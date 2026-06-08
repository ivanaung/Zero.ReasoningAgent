using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public class FinanceAccount
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public FinanceAccountType Type { get; set; } = FinanceAccountType.Bank;

    public FinanceScope Scope { get; set; } = FinanceScope.Personal;

    public decimal OpeningBalance { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
