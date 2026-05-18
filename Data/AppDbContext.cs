using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Pagamento> Pagamentos { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

 protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<Usuario>()
        .HasIndex(u => u.Email)
        .IsUnique();

    modelBuilder.Entity<Usuario>()
        .HasIndex(u => u.Cpf)
        .IsUnique();

    modelBuilder.Entity<Pagamento>()
        .HasOne(p => p.Usuario)
        .WithMany(u => u.Pagamentos)
        .HasForeignKey(p => p.UserId);
}
}