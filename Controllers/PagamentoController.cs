using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
public class PagamentoController : ControllerBase
{
    // Contexto do banco de dados usado para acessar Usuarios e Pagamentos
    private readonly AppDbContext _context;

    public PagamentoController(AppDbContext context)
    {
        _context = context;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarPagamentoDTO dto)
    {
        // Pega o ID do usuário logado a partir do token JWT
        var userId = long.Parse(User.FindFirst("id")!.Value);

        // Busca o usuário no banco de dados
        var usuario = await _context.Usuarios.FindAsync(userId);

        // Se o usuário não existir, retorna não autorizado
        if (usuario == null)
            return Unauthorized("Usuário não encontrado.");

        // Valida se o usuário possui token/código do Mercado Pago cadastrado
        if (string.IsNullOrWhiteSpace(usuario.CodigoMercadoPago))
            return BadRequest("Usuário não possui código/token do Mercado Pago cadastrado.");

        // Ajusta a data de vencimento para o fim do dia no horário do Brasil
        // Isso evita problemas no Railway, que normalmente trabalha em UTC
        var dataExpiracaoBrasil = new DateTimeOffset(
            dto.DateOfExpiration.Date.AddHours(23).AddMinutes(59).AddSeconds(59),
            TimeSpan.FromHours(-3)
        );

        // Cria o pagamento localmente no banco antes de chamar o Mercado Pago
        // Assim temos um ID interno para usar como external_reference
        var pagamento = new Pagamento
        {
            Title = dto.Title,
            Description = dto.Description,
            UnitPrice = dto.UnitPrice,

            // Salva a data ajustada no banco
            DateOfExpiration = dataExpiracaoBrasil.DateTime,

            // Data de criação do registro no servidor
            CreatedAt = DateTime.Now,

            // Status inicial da cobrança
            Status = "pending",

            // Relaciona o pagamento com o usuário logado
            UserId = userId
        };

        // Adiciona o pagamento no contexto do Entity Framework
        _context.Pagamentos.Add(pagamento);

        // Salva no banco para gerar o ID do pagamento
        await _context.SaveChangesAsync();

        // Monta o payload que será enviado para o Mercado Pago
        var mercadoPagoPayload = new
        {
            // Referência interna do pagamento criado no banco
            external_reference = pagamento.Id.ToString(),

            // Envia a data no padrão aceito pelo Mercado Pago com timezone do Brasil
            date_of_expiration = dataExpiracaoBrasil.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),

            // Item da cobrança
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

        // Serializa o payload para JSON
        var json = JsonSerializer.Serialize(mercadoPagoPayload);

        // Cria o cliente HTTP para chamar a API do Mercado Pago
        using var httpClient = new HttpClient();

        // Cria a requisição POST para criar a preferência de pagamento
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.mercadopago.com/checkout/preferences"
        );

        // Adiciona o token do Mercado Pago do usuário no header Authorization
        request.Headers.Add("Authorization", $"Bearer {usuario.CodigoMercadoPago}");

        // Adiciona o JSON no corpo da requisição
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        // Envia a requisição para o Mercado Pago
        var response = await httpClient.SendAsync(request);

        // Lê o retorno da API do Mercado Pago
        var content = await response.Content.ReadAsStringAsync();

        // Se o Mercado Pago retornar erro, devolve o erro para o front
        if (!response.IsSuccessStatusCode)
            return BadRequest(content);

        // Converte o retorno do Mercado Pago para o DTO de resposta
        var mercadoPagoResponse = JsonSerializer.Deserialize<MercadoPagoPreferenceResponseDTO>(content);

        // Salva o ID gerado pelo Mercado Pago
        pagamento.MercadoPagoId = mercadoPagoResponse?.id;

        // Salva o link de pagamento
        // Em sandbox usa sandbox_init_point; em produção usa init_point
        pagamento.InitPoint = mercadoPagoResponse?.sandbox_init_point ?? mercadoPagoResponse?.init_point;

        // Atualiza o pagamento no banco com os dados retornados pelo Mercado Pago
        await _context.SaveChangesAsync();

        // Retorna para o front os dados da cobrança criada
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
        // Pega o ID do usuário logado pelo token JWT
        var userId = long.Parse(User.FindFirst("id")!.Value);

        // Busca apenas os pagamentos do usuário logado
        var pagamentos = await _context.Pagamentos
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        // Retorna a lista para o front
        return Ok(pagamentos);
    }

    [AllowAnonymous]
    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPublico(long id)
    {
        // Busca o pagamento pelo ID
        var pagamento = await _context.Pagamentos.FindAsync(id);

        // Se não encontrar, retorna 404
        if (pagamento == null)
            return NotFound();

        // Retorna os dados públicos do pagamento
        return Ok(pagamento);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(long id, [FromBody] AtualizarPagamentoDTO dto)
    {
        // Pega o ID do usuário logado
        var userId = long.Parse(User.FindFirst("id")!.Value);

        // Busca o pagamento pelo ID e garante que ele pertence ao usuário logado
        var pagamento = await _context.Pagamentos
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        // Se não encontrar, retorna 404
        if (pagamento == null)
            return NotFound("Pagamento não encontrado para este usuário.");

        // Atualiza os dados permitidos
        pagamento.Title = dto.Title;
        pagamento.Description = dto.Description;

        // Salva as alterações no banco
        await _context.SaveChangesAsync();

        // Retorna confirmação para o front
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
        // Pega o ID do usuário logado
        var userId = long.Parse(User.FindFirst("id")!.Value);

        // Busca o pagamento pelo ID e usuário logado
        var pagamento = await _context.Pagamentos
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        // Se não encontrar, retorna 404
        if (pagamento == null)
            return NotFound("Pagamento não encontrado para este usuário.");

        // Remove o pagamento do banco
        _context.Pagamentos.Remove(pagamento);

        // Salva a exclusão
        await _context.SaveChangesAsync();

        // Retorna confirmação para o front
        return Ok(new
        {
            mensagem = "Pagamento deletado com sucesso."
        });
    }
}