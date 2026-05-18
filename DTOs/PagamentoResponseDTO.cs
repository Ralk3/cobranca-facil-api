public class PagamentoResponseDTO
{
    public long Id { get; set; }
    public string? MercadoPagoId { get; set; }
    public string? InitPoint { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public double UnitPrice { get; set; }
    public string? Status { get; set; }
    public DateTime DateOfExpiration { get; set; }
    public DateTime CreatedAt { get; set; }
}