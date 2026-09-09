using HealingNaturalFarms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealingNaturalFarms.Infrastructure.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();

        b.HasOne(x => x.ParentCategory)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Sku).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.Sku).IsUnique();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.ShortDescription).HasMaxLength(500);
        b.Property(x => x.MatureHeightCm).HasPrecision(6, 2);
        b.Property(x => x.PotSizeCm).HasPrecision(6, 2);

        b.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Images)
            .WithOne(x => x.Product)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.RegionListings)
            .WithOne(x => x.Product)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Url).HasMaxLength(1000).IsRequired();
    }
}

public class ProductRegionListingConfiguration : IEntityTypeConfiguration<ProductRegionListing>
{
    public void Configure(EntityTypeBuilder<ProductRegionListing> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Price).HasPrecision(10, 2);
        b.Property(x => x.CompareAtPrice).HasPrecision(10, 2);

        // A product can appear at most once per region.
        b.HasIndex(x => new { x.ProductId, x.Region }).IsUnique();
    }
}
