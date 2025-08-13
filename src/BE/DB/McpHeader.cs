using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("McpHeader")]
[Index("HeaderName", Name = "IX_McpHeader_HeaderName")]
[Index("McpId", Name = "IX_McpHeader_McpId")]
[Index("McpId", "HeaderName", Name = "UQ_McpHeader", IsUnique = true)]
public partial class McpHeader
{
    [Key]
    public int Id { get; set; }

    public int McpId { get; set; }

    [StringLength(100)]
    public string HeaderName { get; set; } = null!;

    public string HeaderValue { get; set; } = null!;

    [ForeignKey("McpId")]
    [InverseProperty("McpHeaders")]
    public virtual Mcp Mcp { get; set; } = null!;
}
