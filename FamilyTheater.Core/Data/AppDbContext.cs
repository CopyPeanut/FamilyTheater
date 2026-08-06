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
        public DbSet<Tag> Tags { get; set; } = null!;
        public DbSet<MovieTag> MovieTags { get; set; } = null!;
        public DbSet<Setting> Settings { get; set; } = null!;

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
                entity.HasIndex(m => m.FolderPath).IsUnique();
                entity.HasIndex(m => m.Title);
            });

            // Tag 配置
            modelBuilder.Entity<Tag>(entity =>
            {
                entity.HasIndex(t => t.Name).IsUnique();
            });

            // MovieTag 多对多关联
            modelBuilder.Entity<MovieTag>(entity =>
            {
                entity.HasKey(mt => new { mt.MovieId, mt.TagId });

                entity.HasOne(mt => mt.Movie)
                      .WithMany(m => m.MovieTags)
                      .HasForeignKey(mt => mt.MovieId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(mt => mt.Tag)
                      .WithMany(t => t.MovieTags)
                      .HasForeignKey(mt => mt.TagId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
