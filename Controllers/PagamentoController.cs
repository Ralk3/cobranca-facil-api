using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;


[ApiController]
[Route("api/[controller]")]
public class PagamentoController : ControllerBase
{
    private readonly AppDbContext _context;

    public PagamentoController(AppDbContext context)
    {
        _context = context;
    }


[Authorize]
[HttpPost]
public async Task<IActionResult> Criar([FromBody] CriarPagamentoDTO dto)
{
    var userId = long.Parse(User.FindFirst("id")!.Value);

    var usuario = await _context.Usuarios.FindAsync(userId);

    if (usuario == null)
        return Unauthorized("Usuário não encontrado.");

    if (string.IsNullOrWhiteSpace(usuario.CodigoMercadoPago))
        return BadRequest("Usuário não possui código/token do Mercado Pago cadastrado.");

    var pagamento = new Pagamento
    {
        Title = dto.Title,
        Description = dto.Description,
        UnitPrice = dto.UnitPrice,
        DateOfExpiration = dto.DateOfExpiration,
        CreatedAt = DateTime.Now,
        Status = "pending",
        UserId = userId
    };

    _context.Pagamentos.Add(pagamento);
    await _context.SaveChangesAsync();

    var mercadoPagoPayload = new
    {
        external_reference = pagamento.Id.ToString(),
        date_of_expiration = dto.DateOfExpiration.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
        items = new[]
        {
            new
            {
                title = dto.Title,
                quantity = 1,
                currency_id = "BRL",
                unit_price = dto.UnitPrice
            }
        }
    };

    var json = JsonSerializer.Serialize(mercadoPagoPayload);

    using var httpClient = new HttpClient();

    var request = new HttpRequestMessage(
        HttpMethod.Post,
        "https://api.mercadopago.com/checkout/preferences"
    );

    request.Headers.Add("Authorization", $"Bearer {usuario.CodigoMercadoPago}");
    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

    var response = await httpClient.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
        return BadRequest(content);

    var mercadoPagoResponse = JsonSerializer.Deserialize<MercadoPagoPreferenceResponseDTO>(content);

    pagamento.MercadoPagoId = mercadoPagoResponse?.id;
    pagamento.InitPoint = mercadoPagoResponse?.sandbox_init_point ?? mercadoPagoResponse?.init_point;

    await _context.SaveChangesAsync();

    return Ok(new PagamentoResponseDTO
    {
        Id = pagamento.Id,
        MercadoPagoId = pagamento.MercadoPagoId,
        InitPoint = pagamento.InitPoint,
        Title = pagamento.Title,
        Description = pagamento.Description,
        UnitPrice = pagamento.UnitPrice,
        Status = pagamento.Status,
        DateOfExpiration = pagamento.DateOfExpiration,
        CreatedAt = pagamento.CreatedAt
    });
}

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var userId = long.Parse(User.FindFirst("id")!.Value);

        var pagamentos = await _context.Pagamentos
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        return Ok(pagamentos);
    }

    [AllowAnonymous]
    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPublico(long id)
    {
        var pagamento = await _context.Pagamentos.FindAsync(id);

        if (pagamento == null)
            return NotFound();

        return Ok(pagamento);
    }




    [Authorize]
[HttpPut("{id}")]
public async Task<IActionResult> Atualizar(long id, [FromBody] AtualizarPagamentoDTO dto)
{
    var userId = long.Parse(User.FindFirst("id")!.Value);

    var pagamento = await _context.Pagamentos
        .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

    if (pagamento == null)
        return NotFound("Pagamento não encontrado para este usuário.");

    pagamento.Title = dto.Title;
    pagamento.Description = dto.Description;

    await _context.SaveChangesAsync();

    return Ok(new
    {
        mensagem = "Pagamento atualizado com sucesso.",
        pagamento
    });
}



[Authorize]
[HttpDelete("{id}")]
public async Task<IActionResult> Deletar(long id)
{
    var userId = long.Parse(User.FindFirst("id")!.Value);

    var pagamento = await _context.Pagamentos
        .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

    if (pagamento == null)
        return NotFound("Pagamento não encontrado para este usuário.");

    _context.Pagamentos.Remove(pagamento);
    await _context.SaveChangesAsync();

    return Ok(new
    {
        mensagem = "Pagamento deletado com sucesso."
    });
}


}