using ScheduleApp.Models;

namespace ScheduleApp.Models.ViewModels;

public class FinanceDashboardViewModel
{
    public DateTime MonthStart { get; set; }
    public decimal MonthlyIncome { get; set; }
    public decimal MonthlyExpense { get; set; }
    public decimal Net => MonthlyIncome - MonthlyExpense;
    public decimal PersonalSpending { get; set; }
    public decimal BusinessSpending { get; set; }
    public int BudgetWarningCount { get; set; }
    public List<FinanceBudgetWarningItemViewModel> BudgetWarnings { get; set; } = new();
    public List<FinanceRecurringItemSummaryViewModel> UpcomingBills { get; set; } = new();
    public List<FinanceTransactionListItemViewModel> RecentTransactions { get; set; } = new();
    public List<FinanceTransactionListItemViewModel> ProjectLinkedTransactions { get; set; } = new();
    public List<FinanceAccount> Accounts { get; set; } = new();
    public List<FinanceCategory> Categories { get; set; } = new();
    public List<ProjectOptionItemViewModel> Projects { get; set; } = new();
    public List<FinanceBudget> Budgets { get; set; } = new();
    public List<FinanceRecurringItem> RecurringItems { get; set; } = new();
    public FinanceRecurringItemSummaryViewModel? NextUpcomingBill { get; set; }
}

public class FinanceTransactionFormViewModel
{
    public int Id { get; set; }
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
}

public class FinanceTransactionListItemViewModel
{
    public int Id { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public string Description { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public FinanceTransactionType Type { get; set; }
    public FinanceScope Scope { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
}

public class FinanceBudgetWarningItemViewModel
{
    public string CategoryName { get; set; } = string.Empty;
    public FinanceScope Scope { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal ActualAmount { get; set; }
}

public class FinanceRecurringItemSummaryViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? ProjectName { get; set; }
    public decimal Amount { get; set; }
    public FinanceScope Scope { get; set; }
    public FinanceRecurringFrequency Frequency { get; set; }
    public DateTime NextDueDate { get; set; }
}

public class FinanceSummaryWidgetViewModel
{
    public decimal MonthlyIncome { get; set; }
    public decimal MonthlyExpense { get; set; }
    public decimal Net => MonthlyIncome - MonthlyExpense;
    public int BudgetWarningCount { get; set; }
    public FinanceRecurringItemSummaryViewModel? NextUpcomingBill { get; set; }
}
