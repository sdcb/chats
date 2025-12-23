using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("File")]
[Index("ClientInfoId", Name = "IX_File_ClientInfo")]
[Index("CreateUserId", Name = "IX_File_CreateUser")]
[Index("FileServiceId", "StorageKey", Name = "IX_File_StorageKey")]
public partial class File
{
    [Key]
    public int Id { get; set; }

    [StringLength(200)]
    public string FileName { get; set; } = null!;

    public int FileServiceId { get; set; }

    [StringLength(300)]
    public string StorageKey { get; set; } = null!;

    public int Size { get; set; }

    public int ClientInfoId { get; set; }

    public int CreateUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    [StringLength(100)]
    public string MediaType { get; set; } = null!;

    [ForeignKey("ClientInfoId")]
    [InverseProperty("Files")]
    public virtual ClientInfo ClientInfo { get; set; } = null!;

    [ForeignKey("CreateUserId")]
    [InverseProperty("Files")]
    public virtual User CreateUser { get; set; } = null!;

    [InverseProperty("File")]
    public virtual FileImageInfo? FileImageInfo { get; set; }

    [ForeignKey("FileServiceId")]
    [InverseProperty("Files")]
    public virtual FileService FileService { get; set; } = null!;

    [InverseProperty("File")]
    public virtual ICollection<StepContentFile> StepContentFiles { get; set; } = new List<StepContentFile>();
}
