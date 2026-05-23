using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
public class PagamentoController : ControllerBase
{
    // Contexto do banco utilizado pelo Entity Framework
    private readonly AppDbContext _context;

    public PagamentoController(AppDbContext context)
    {
        _context = context;
    }

    // Endpoint responsável por criar uma nova cobrança
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarPagamentoDTO dto)
    {
        // Recupera o ID do usuário logado através do JWT
        var userId = long.Parse(User.FindFirst("id")!.Value);

        // Busca o usuário no banco
        var usuario = await _context.Usuarios.FindAsync(userId);

        // Valida se o usuário existe
        if (usuario == null)
            return Unauthorized("Usuário não encontrado.");

        // Valida se o usuário possui token do Mercado Pago
        if (string.IsNullOrWhiteSpace(usuario.CodigoMercadoPago))
            return BadRequest("Usuário não possui código/token do Mercado Pago cadastrado.");

        // =========================================================
        // AJUSTE IMPORTANTE PARA RAILWAY / UTC
        // =========================================================
        // Aqui criamos manualmente a data no timezone brasileiro.
        // Isso evita erro de UTC Offset no Railway.
        // Também colocamos o vencimento no fim do dia.
        // =========================================================
        var dataExpiracaoBrasil = new DateTimeOffset(
            dto.DateOfExpiration.Year,
            dto.DateOfExpiration.Month,
            dto.DateOfExpiration.Day,
            23,
            59,
            59,
            TimeSpan.FromHours(-3)
        );

        // Cria o objeto de pagamento local
        var pagamento = new Pagamento
        {
            Title = dto.Title,
            Description = dto.Description,
            UnitPrice = dto.UnitPrice,

            // Salva a data ajustada
            DateOfExpiration = dataExpiracaoBrasil.DateTime,

            // Data de criação do registro
            CreatedAt = DateTime.Now,

            // Status inicial
            Status = "pending",

            // Relaciona ao usuário logado
            UserId = userId
        };

        // Adiciona no contexto do Entity Framework
        _context.Pagamentos.Add(pagamento);

        // Salva no banco para gerar ID
        await _context.SaveChangesAsync();

        // =========================================================
        // PAYLOAD ENVIADO AO MERCADO PAGO
        // =========================================================
        var mercadoPagoPayload = new
        {
            // ID interno do sistema
            external_reference = pagamento.Id.ToString(),

            // Data formatada corretamente com timezone do Brasil
            date_of_expiration = dataExpiracaoBrasil.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),

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

        // Converte payload para JSON
        var json = JsonSerializer.Serialize(mercadoPagoPayload);

        // Cliente HTTP usado para chamar a API do Mercado Pago
        using var httpClient = new HttpClient();

        // Cria requisição POST
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.mercadopago.com/checkout/preferences"
        );

        // Adiciona token do usuário
        request.Headers.Add("Authorization", $"Bearer {usuario.CodigoMercadoPago}");

        // Adiciona body JSON
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        // Executa chamada HTTP
        var response = await httpClient.SendAsync(request);

        // Lê retorno da API
        var content = await response.Content.ReadAsStringAsync();

        // =========================================================
        // SE MERCADO PAGO RETORNAR ERRO
        // =========================================================
        if (!response.IsSuccessStatusCode)
        {
            return BadRequest(new
            {
                mensagem = "Erro ao criar preferência no Mercado Pago.",
                retornoMercadoPago = content
            });
        }

        // Desserializa resposta
        var mercadoPagoResponse =
            JsonSerializer.Deserialize<MercadoPagoPreferenceResponseDTO>(content);

        // Salva ID do Mercado Pago
        pagamento.MercadoPagoId = mercadoPagoResponse?.id;

        // Salva link de pagamento
        pagamento.InitPoint =
            mercadoPagoResponse?.sandbox_init_point
            ?? mercadoPagoResponse?.init_point;

        // Atualiza registro no banco
        await _context.SaveChangesAsync();

        // Retorna resposta para o front
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

    // Lista os pagamentos do usuário logado
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        // Recupera usuário logado
        var userId = long.Parse(User.FindFirst("id")!.Value);

        // Busca pagamentos ordenados do mais novo para o mais antigo
        var pagamentos = await _context.Pagamentos
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        // Retorna lista
        return Ok(pagamentos);
    }

    // Endpoint público para visualizar pagamento
    [AllowAnonymous]
    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPublico(long id)
    {
        // Busca pagamento pelo ID
        var pagamento = await _context.Pagamentos.FindAsync(id);

        // Se não existir retorna 404
        if (pagamento == null)
            return NotFound();

        // Retorna pagamento
        return Ok(pagamento);
    }

    // Atualiza título e descrição do pagamento
    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(
        long id,
        [FromBody] AtualizarPagamentoDTO dto
    )
    {
        // Usuário logado
        var userId = long.Parse(User.FindFirst("id")!.Value);

        // Busca pagamento do usuário
        var pagamento = await _context.Pagamentos
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        // Se não existir retorna 404
        if (pagamento == null)
            return NotFound("Pagamento não encontrado para este usuário.");

        // Atualiza dados
        pagamento.Title = dto.Title;
        pagamento.Description = dto.Description;

        // Salva alterações
        await _context.SaveChangesAsync();

        // Retorna confirmação
        return Ok(new
        {
            mensagem = "Pagamento atualizado com sucesso.",
            pagamento
        });
    }

    // Remove um pagamento
    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Deletar(long id)
    {
        // Usuário logado
        var userId = long.Parse(User.FindFirst("id")!.Value);

        // Busca pagamento do usuário
        var pagamento = await _context.Pagamentos
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        // Se não encontrar retorna 404
        if (pagamento == null)
            return NotFound("Pagamento não encontrado para este usuário.");

        // Remove pagamento
        _context.Pagamentos.Remove(pagamento);

        // Salva exclusão
        await _context.SaveChangesAsync();

        // Retorna confirmação
        return Ok(new
        {
            mensagem = "Pagamento deletado com sucesso."
        });
    }
}