using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

[Table("AssetTransfers")]
public class AssetTransfer
{
    [Key]
    public int Id { get; set; }

    public int AssetId { get; set; }

    public DateTime TransferDate { get; set; }

    [StringLength(100)]
    public string? FromLocation { get; set; }

    [StringLength(50)]
    public string? FromBay { get; set; }

    [StringLength(100)]
    public string? FromDepartment { get; set; }

    [StringLength(100)]
    public string? ToLocation { get; set; }

    [StringLength(50)]
    public string? ToBay { get; set; }

    [StringLength(100)]
    public string? ToDepartment { get; set; }

    [StringLength(100)]
    public string? Reason { get; set; }
    public int? ReasonLookupValueId { get; set; }
    public LookupValue? ReasonLookupValue { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    [ForeignKey(nameof(AssetId))]
    public Asset? Asset { get; set; }
}
