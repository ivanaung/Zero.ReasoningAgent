namespace ScheduleApp.Models;

public enum FinanceScope
{
    Personal,
    Business,
    Both
}

public enum FinanceAccountType
{
    Cash,
    Bank,
    CreditCard,
    Savings,
    Other
}

public enum FinanceCategoryType
{
    Income,
    Expense
}

public enum FinanceTransactionType
{
    Income,
    Expense,
    Transfer
}

public enum FinanceRecurringFrequency
{
    Weekly,
    Monthly,
    Yearly
}
