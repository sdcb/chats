using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ChatPreset")]
[Index("Name", Name = "IX_ChatPreset_Name")]
[Index("UserId", Name = "IX_ChatPreset_UserId")]
public partial class ChatPreset
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    public int UserId { get; set; }

    public DateTime UpdatedAt { get; set; }

    public short Order { get; set; }

    [InverseProperty("ChatPreset")]
    public virtual ICollection<ChatPresetSpan> ChatPresetSpans { get; set; } = new List<ChatPresetSpan>();

    [ForeignKey("UserId")]
    [InverseProperty("ChatPresets")]
    public virtual User User { get; set; } = null!;
}
