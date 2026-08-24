using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FamilyTheater.Core.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Movie> Movies { get; set; } = null!;
        public DbSet<MovieTag> MovieTags { get; set; } = null!;
        public DbSet<Setting> Settings { get; set; } = null!;
        public DbSet<Picture> Pictures { get; set; } = null!;
        public DbSet<PictureTag> PictureTags { get; set; } = null!;
        public DbSet<Game> Games { get; set; } = null!;
        public DbSet<GameTag> GameTags { get; set; } = null!;
        public DbSet<Manga> Mangas { get; set; } = null!;
        public DbSet<MangaTag> MangaTags { get; set; } = null!;

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Username).IsUnique();
            });

            // Movie 配置
            modelBuilder.Entity<Movie>(entity =>
            {
                entity.HasIndex(m => m.FolderPath);
                entity.HasIndex(m => m.VideoFilePath).IsUnique();
                entity.HasIndex(m => m.Title);
            });

            // MovieTag 配置：合并标签字典+关联，联合唯一 (MovieId, TagName)
            modelBuilder.Entity<MovieTag>(entity =>
            {
                entity.HasIndex(mt => new { mt.MovieId, mt.TagName }).IsUnique();

                entity.HasOne(mt => mt.Movie)
                      .WithMany(m => m.MovieTags)
                      .HasForeignKey(mt => mt.MovieId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Picture 配置
            modelBuilder.Entity<Picture>(entity =>
            {
                entity.HasIndex(p => p.FilePath).IsUnique();
                entity.HasIndex(p => p.FileName);
            });

            // PictureTag 配置：联合唯一 (PictureId, TagName)
            modelBuilder.Entity<PictureTag>(entity =>
            {
                entity.HasIndex(pt => new { pt.PictureId, pt.TagName }).IsUnique();

                entity.HasOne(pt => pt.Picture)
                      .WithMany(p => p.PictureTags)
                      .HasForeignKey(pt => pt.PictureId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Game>(entity =>
            {
                entity.HasIndex(g => g.FolderPath).IsUnique();
                entity.HasIndex(g => g.Title);
            });

            modelBuilder.Entity<GameTag>(entity =>
            {
                entity.HasIndex(gt => new { gt.GameId, gt.TagName }).IsUnique();

                entity.HasOne(gt => gt.Game)
                      .WithMany(g => g.GameTags)
                      .HasForeignKey(gt => gt.GameId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Manga>(entity =>
            {
                entity.HasIndex(m => m.FilePath).IsUnique();
                entity.HasIndex(m => m.Title);
            });

            modelBuilder.Entity<MangaTag>(entity =>
            {
                entity.HasIndex(mt => new { mt.MangaId, mt.TagName }).IsUnique();

                entity.HasOne(mt => mt.Manga)
                      .WithMany(m => m.MangaTags)
                      .HasForeignKey(mt => mt.MangaId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
