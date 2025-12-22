using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.DB;

[Table("UserModel")]
[Index("ModelId", Name = "IX_UserModel2_ModelId")]
[Index("UserId", Name = "IX_UserModel_UserId")]
public partial class UserModel
{
    [Key]
    public int Id { get; set; }

    public short ModelId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public int TokenBalance { get; set; }

    public int CountBalance { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UserId { get; set; }

    [ForeignKey("ModelId")]
    [InverseProperty("UserModels")]
    public virtual Model Model { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("UserModels")]
    public virtual User User { get; set; } = null!;
}
