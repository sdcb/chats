using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("ChatConfigMcp")]
public partial class ChatConfigMcp
{
    [Key]
    public int ChatConfigId { get; set; }

    public int McpId { get; set; }

    [ForeignKey("McpId")]
    [InverseProperty("ChatConfigMcps")]
    public virtual Mcp Mcp { get; set; } = null!;
}
