public class Usuario
{
    public long Id { get; set; }

    public string? Nome { get; set; }
    public string? Sobrenome { get; set; }
    public string? Email { get; set; }
    public string? Celular { get; set; }
    public string? Cpf { get; set; }
    public string? Senha { get; set; }
    public string? CodigoMercadoPago { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<Pagamento> Pagamentos { get; set; } = new List<Pagamento>();
}