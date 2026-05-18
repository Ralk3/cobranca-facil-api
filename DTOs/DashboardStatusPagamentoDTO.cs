namespace CobrancaFacil.Api.DTOs
{
    public class DashboardStatusPagamentoDTO
    {
        public string Status { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public double ValorTotal { get; set; }
    }
}