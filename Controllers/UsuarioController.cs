using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/[controller]")]
public class UsuarioController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public UsuarioController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok("API funcionando");
    }

    [HttpPost("cadastro")]
    public async Task<IActionResult> Cadastro([FromBody] Usuario usuario)
    {
        var emailExiste = await _context.Usuarios
            .AnyAsync(u => u.Email == usuario.Email);

        if (emailExiste)
            return BadRequest("Já existe um usuário cadastrado com este e-mail.");

        var cpfExiste = await _context.Usuarios
            .AnyAsync(u => u.Cpf == usuario.Cpf);

        if (cpfExiste)
            return BadRequest("Já existe um usuário cadastrado com este CPF.");

        usuario.CreatedAt = DateTime.Now;

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        return Ok(usuario);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDTO login)
    {
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email == login.Email && u.Senha == login.Senha);

        if (usuario == null)
            return Unauthorized("E-mail ou senha inválidos.");

        var claims = new[]
        {
            new Claim("id", usuario.Id.ToString()),
            new Claim("email", usuario.Email ?? "")
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!)
        );

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddHours(2),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new
        {
            mensagem = "Login realizado com sucesso",
            token = tokenString,
            usuario = new
            {
                usuario.Id,
                usuario.Nome,
                usuario.Email,
                usuario.CodigoMercadoPago,
                usuario.CreatedAt
            }
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = long.Parse(User.FindFirst("id")!.Value);

        var usuario = await _context.Usuarios.FindAsync(userId);

        if (usuario == null)
            return NotFound("Usuário não encontrado.");

        return Ok(new
        {
            usuario.Id,
            usuario.Nome,
            usuario.Sobrenome,
            usuario.Email,
            usuario.Celular,
            usuario.Cpf,
            usuario.CodigoMercadoPago,
            usuario.CreatedAt
        });
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> AtualizarMe([FromBody] Usuario dados)
    {
        var userId = long.Parse(User.FindFirst("id")!.Value);

        var usuario = await _context.Usuarios.FindAsync(userId);

        if (usuario == null)
            return NotFound("Usuário não encontrado.");

        usuario.Nome = dados.Nome;
        usuario.Sobrenome = dados.Sobrenome;
        usuario.Celular = dados.Celular;
        usuario.CodigoMercadoPago = dados.CodigoMercadoPago;

        if (!string.IsNullOrWhiteSpace(dados.Senha))
            usuario.Senha = dados.Senha;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensagem = "Usuário atualizado com sucesso.",
            usuario = new
            {
                usuario.Id,
                usuario.Nome,
                usuario.Sobrenome,
                usuario.Email,
                usuario.Celular,
                usuario.Cpf,
                usuario.CodigoMercadoPago,
                usuario.CreatedAt
            }
        });
    }
}