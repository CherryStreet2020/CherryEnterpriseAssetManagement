using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abs.FixedAssets.Data
{
    /// <summary>
    /// EF Core helper that maps a <c>byte[]? RowVersion</c> property on an
    /// entity to PostgreSQL's built-in <c>xmin</c> system column. The
    /// 4-byte big-endian conversion lets the public API expose RowVersion
    /// as a base64 ETag while EF uses xmin natively as the concurrency
    /// token.
    ///
    /// Used by every entity in a state-machine-managed transition surface
    /// where two concurrent updates would otherwise silently overwrite
    /// each other (Asset, PurchaseOrder, MaintenanceEvent, GoodsReceipt,
    /// VendorInvoice, CipProject — see audit S1-8 / S2-8).
    ///
    /// xmin is a system column on every PG row, no DDL is required.
    /// Migrations that "add" a RowVersion are no-op markers for snapshot
    /// alignment only.
    /// </summary>
    public static class XminRowVersionExtensions
    {
        public static void MapXminRowVersion<TEntity>(
            this EntityTypeBuilder<TEntity> e,
            System.Linq.Expressions.Expression<Func<TEntity, byte[]?>> propertySelector)
            where TEntity : class
        {
            e.Property(propertySelector)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken()
                .HasConversion(
                    v => v == null || v.Length != 4
                        ? 0u
                        : ((uint)v[0] << 24) | ((uint)v[1] << 16) | ((uint)v[2] << 8) | (uint)v[3],
                    v => new byte[]
                    {
                        (byte)((v >> 24) & 0xFF),
                        (byte)((v >> 16) & 0xFF),
                        (byte)((v >> 8)  & 0xFF),
                        (byte)( v        & 0xFF)
                    },
                    new ValueComparer<byte[]?>(
                        (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
                        v => v == null ? 0 : v.Aggregate(0, (h, x) => HashCode.Combine(h, x)),
                        v => v == null ? null : v.ToArray()));
        }
    }
}
