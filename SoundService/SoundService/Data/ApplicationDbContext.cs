using Microsoft.EntityFrameworkCore;
using SoundService.Models;

namespace SoundService.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
        Database.EnsureCreated();
    }
    public DbSet<AudioRecord> AudioRecords { get; set; }
    public DbSet<Author> Authors { get; set; }
    public DbSet<Album> Albums { get; set; }
    public DbSet<Genre> Genres { get; set; }
    public DbSet<AudioKeyword> Keywords { get; set; }
    public DbSet<AudioKeyword> AudioKeywords { get; set; }
    public DbSet<AudioThematicTag> ThematicTags { get; set; }
    public DbSet<AudioThematicTag> AudioThematicTags { get; set; }
    //public DbSet<TranscriptSegment> TranscriptSegments { get; set; }
    public DbSet<ModerationStatus> ModerationStatuses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Настройка составных первичных ключей
        modelBuilder.Entity<AudioKeyword>()
            .HasKey(ak => new { ak.AudioRecordId, ak.KeywordId });

         modelBuilder.Entity<AudioThematicTag>()
            .HasKey(at => new { at.AudioRecordId, at.ThematicTagId });
    }
}