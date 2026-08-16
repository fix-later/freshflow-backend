using FreshFlow.Orders.Application.Dtos;

namespace FreshFlow.Orders.Application.Abstractions;

public interface IStatementPdfRenderer
{
    /// <summary>Renders a credit statement, including its line items, to a PDF byte stream.</summary>
    public byte[] Render(CreditStatementDto statement);
}
