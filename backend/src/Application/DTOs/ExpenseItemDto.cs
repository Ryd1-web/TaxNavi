namespace TaxNavi.Application.DTOs;

public class ExpenseItemDto
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = ExpenseCategories.Other;
    public decimal AnnualAmount { get; set; }
}
