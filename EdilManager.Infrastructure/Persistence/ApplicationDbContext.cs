using EdilManager.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace EdilManager.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Fornitore> Fornitori => Set<Fornitore>();
    public DbSet<Cantiere> Cantieri => Set<Cantiere>();
    public DbSet<Fattura> Fatture => Set<Fattura>();
    public DbSet<RigaFattura> RigheFattura => Set<RigaFattura>();
    public DbSet<Dipendente> Dipendenti => Set<Dipendente>();
    public DbSet<Presenza> Presenze => Set<Presenza>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Fornitore>(e =>
        {
            e.HasKey(f => f.Id);
            e.HasIndex(f => f.PartitaIva).IsUnique();
            e.Property(f => f.PartitaIva).IsRequired().HasMaxLength(20);
            e.Property(f => f.Denominazione).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Cantiere>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.CUP).IsUnique();
            e.Property(c => c.CUP).IsRequired().HasMaxLength(50);
            e.Property(c => c.Nome).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Fattura>(e =>
        {
            e.HasKey(f => f.Id);
            e.HasIndex(f => new { f.FornitoreId, f.NumeroDocumento, f.DataDocumento }).IsUnique();
            e.Property(f => f.NumeroDocumento).IsRequired().HasMaxLength(50);
            e.Property(f => f.TipoDocumento).HasMaxLength(10);
            e.Property(f => f.TotaleImponibile).HasPrecision(18, 2);
            e.Property(f => f.TotaleImposta).HasPrecision(18, 2);
            e.Property(f => f.TotaleDocumento).HasPrecision(18, 2);
            e.Property(f => f.ImportoPagamento).HasPrecision(18, 2);
            e.HasOne(f => f.Fornitore).WithMany(fo => fo.Fatture).HasForeignKey(f => f.FornitoreId);
            e.HasOne(f => f.Cantiere).WithMany(c => c.Fatture).HasForeignKey(f => f.CantiereId).IsRequired(false);
        });

        modelBuilder.Entity<RigaFattura>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Descrizione).IsRequired().HasMaxLength(500);
            e.Property(r => r.Quantita).HasPrecision(18, 4);
            e.Property(r => r.PrezzoUnitario).HasPrecision(18, 4);
            e.Property(r => r.Sconto).HasPrecision(5, 2);
            e.Property(r => r.AliquotaIVA).HasPrecision(5, 2);
            e.Property(r => r.PrezzoTotale).HasPrecision(18, 2);
            e.HasOne(r => r.Fattura).WithMany(f => f.Righe).HasForeignKey(r => r.FatturaId);
        });

        modelBuilder.Entity<Dipendente>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.CodiceFiscale).IsUnique();
            e.Property(d => d.Nome).IsRequired().HasMaxLength(100);
            e.Property(d => d.Cognome).IsRequired().HasMaxLength(100);
            e.Property(d => d.CodiceFiscale).IsRequired().HasMaxLength(16);
            e.Property(d => d.Mansione).HasMaxLength(100);
            e.Property(d => d.Livello).HasMaxLength(100);
            e.Property(d => d.Telefono).HasMaxLength(20);
            e.Property(d => d.Email).HasMaxLength(200);
        });

        modelBuilder.Entity<Presenza>(e =>
        {
            e.HasKey(p => p.Id);
            // Più presenze nello stesso giorno sono ammesse se su cantieri diversi: un cantiere al massimo una volta al giorno.
            // Quella senza cantiere (CantiereId nullo) la limita il servizio, perché in SQLite i NULL sono tutti distinti.
            e.HasIndex(p => new { p.DipendenteId, p.Data });
            e.HasIndex(p => new { p.DipendenteId, p.Data, p.CantiereId }).IsUnique().HasFilter("\"CantiereId\" IS NOT NULL");
            e.Property(p => p.OreOrdinarie).HasPrecision(5, 2);
            e.Property(p => p.OreStatordinarie).HasPrecision(5, 2);
            e.HasOne(p => p.Dipendente).WithMany(d => d.Presenze).HasForeignKey(p => p.DipendenteId);
            e.HasOne(p => p.Cantiere).WithMany(c => c.Presenze).HasForeignKey(p => p.CantiereId).IsRequired(false);
        });
    }
}
