using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ProviderFileMapping")]
[Index("Status", "ExpiresAt", Name = "IX_ProviderFileMapping_ExpiresAt")]
public partial class ProviderFileMapping
{
    [Key]
    public long Id { get; set; }

    public int FileId { get; set; }

    public short ModelKeyId { get; set; }

    [StringLength(32)]
    [Unicode(false)]
    public string Protocol { get; set; } = null!;

    [StringLength(64)]
    [Unicode(false)]
    public string Purpose { get; set; } = null!;

    [StringLength(256)]
    [Unicode(false)]
    public string RemoteFileId { get; set; } = null!;

    public byte Status { get; set; }

    public DateTime UploadedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    [ForeignKey("FileId")]
    [InverseProperty("ProviderFileMappings")]
    public virtual File File { get; set; } = null!;

    [ForeignKey("ModelKeyId")]
    [InverseProperty("ProviderFileMappings")]
    public virtual ModelKey ModelKey { get; set; } = null!;
}
