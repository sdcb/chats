using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ChatConfigMcp")]
[Index("ChatConfigId", Name = "IX_ChatConfigMcp_ChatConfigId")]
[Index("McpServerId", Name = "IX_ChatConfigMcp_McpServerId")]
public partial class ChatConfigMcp
{
    [Key]
    public int Id { get; set; }

    public int ChatConfigId { get; set; }

    public int McpServerId { get; set; }

    public string? CustomHeaders { get; set; }

    [ForeignKey("ChatConfigId")]
    [InverseProperty("ChatConfigMcps")]
    public virtual ChatConfig ChatConfig { get; set; } = null!;

    [ForeignKey("McpServerId")]
    [InverseProperty("ChatConfigMcps")]
    public virtual McpServer McpServer { get; set; } = null!;
}
