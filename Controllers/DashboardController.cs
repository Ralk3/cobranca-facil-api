using CobrancaFacil.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CobrancaFacil.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("resumo")]
        public async Task<ActionResult<DashboardResumoDTO>> GetResumo()
        {
            var resumo = new DashboardResumoDTO
            {
                TotalUsuarios = await _context.Usuarios.CountAsync(),

                TotalPagamentos = await _context.Pagamentos.CountAsync(),

                TotalAprovados = await _context.Pagamentos
                    .CountAsync(p => p.Status == "approved"),

                TotalPendentes = await _context.Pagamentos
                    .CountAsync(p => p.Status == "pending"),

                TotalExpirado = await _context.Pagamentos
                    .CountAsync(p => p.Status == "expired"),

                TotalCancelado = await _context.Pagamentos
                    .CountAsync(p => p.Status == "canceled"),

                ValorTotalAprovado = await _context.Pagamentos
                    .Where(p => p.Status == "approved")
                    .SumAsync(p => p.UnitPrice)
            };

            return Ok(resumo);
        }

        [HttpGet("pagamentos-por-status")]
        public async Task<ActionResult<List<DashboardStatusPagamentoDTO>>> GetPagamentosPorStatus()
        {
            var dados = await _context.Pagamentos
                .GroupBy(p => p.Status)
                .Select(g => new DashboardStatusPagamentoDTO
                {
                    Status = g.Key ?? "sem_status",
                    Quantidade = g.Count(),
                    ValorTotal = g.Sum(p => p.UnitPrice)
                })
                .ToListAsync();

            return Ok(dados);
        }

        [HttpGet("transacoes-por-dia")]
        public async Task<ActionResult<List<DashboardTransacoesPorDiaDTO>>> GetTransacoesPorDia()
        {
            var dados = await _context.Pagamentos
                .GroupBy(p => p.CreatedAt.Date)
                .Select(g => new DashboardTransacoesPorDiaDTO
                {
                    Data = g.Key,
                    Quantidade = g.Count(),
                    ValorTotal = g.Sum(p => p.UnitPrice)
                })
                .OrderBy(x => x.Data)
                .ToListAsync();

            return Ok(dados);
        }



        [HttpGet("clientes-por-dia")]
public async Task<ActionResult<List<DashboardClientesPorDiaDTO>>> GetClientesPorDia()
{
    var dados = await _context.Usuarios
        .GroupBy(u => u.CreatedAt.Date)
        .Select(g => new DashboardClientesPorDiaDTO
        {
            Data = g.Key,
            Quantidade = g.Count()
        })
        .OrderBy(x => x.Data)
        .ToListAsync();

    return Ok(dados);
}
    }
}