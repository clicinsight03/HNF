using HealingNaturalFarms.Domain.Entities;
using HealingNaturalFarms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealingNaturalFarms.Infrastructure.Data.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.GuestId).HasMaxLength(64);
        b.HasIndex(x => x.GuestId);
        b.HasIndex(x => x.UserId);

        b.HasMany(x => x.Items)
            .WithOne(x => x.Cart)
            .HasForeignKey(x => x.CartId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.UnitPriceSnapshot).HasPrecision(10, 2);
        b.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique();
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.OrderNumber).HasMaxLength(32).IsRequired();
        b.HasIndex(x => x.OrderNumber).IsUnique();
        b.Property(x => x.Subtotal).HasPrecision(10, 2);
        b.Property(x => x.Tax).HasPrecision(10, 2);
        b.Property(x => x.ShippingCost).HasPrecision(10, 2);
        b.Property(x => x.Total).HasPrecision(10, 2);

        b.HasOne(x => x.ShippingAddress)
            .WithMany()
            .HasForeignKey(x => x.ShippingAddressId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Items)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Payments)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.ProductNameSnapshot).HasMaxLength(200).IsRequired();
        b.Property(x => x.UnitPrice).HasPrecision(10, 2);
        b.Property(x => x.LineTotal).HasPrecision(10, 2);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.ProviderReference).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.ProviderReference);
        b.Property(x => x.Amount).HasPrecision(10, 2);
        b.Property(x => x.RawResponseJson).HasColumnType("longtext");
    }
}

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Line1).HasMaxLength(200).IsRequired();
        b.Property(x => x.City).HasMaxLength(100).IsRequired();
        b.Property(x => x.State).HasMaxLength(100).IsRequired();
        b.Property(x => x.PostalCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.Country).HasMaxLength(2).IsRequired();

        // FK to the real Identity/EF user entity (AppUser) - Address
        // itself carries no navigation property to it; see the note on
        // Domain.Entities.Address.UserId.
        b.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DeviceRegistrationConfiguration : IEntityTypeConfiguration<DeviceRegistration>
{
    public void Configure(EntityTypeBuilder<DeviceRegistration> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.PushToken).HasMaxLength(512).IsRequired();
        b.HasIndex(x => x.PushToken).IsUnique();

        b.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
