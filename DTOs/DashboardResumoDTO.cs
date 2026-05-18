namespace CobrancaFacil.Api.DTOs
{
    public class DashboardResumoDTO
    {
        public int TotalUsuarios { get; set; }
        public int TotalPagamentos { get; set; }
        public int TotalAprovados { get; set; }
        public int TotalPendentes { get; set; }

        public int TotalExpirado { get; set; }

        public int TotalCancelado { get; set; }
        public double ValorTotalAprovado { get; set; }
    }
}