using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("FileServiceType")]
public partial class FileServiceType
{
    [Key]
    public byte Id { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Name { get; set; } = null!;

    [StringLength(500)]
    public string InitialConfig { get; set; } = null!;

    [InverseProperty("FileServiceType")]
    public virtual ICollection<FileService> FileServices { get; set; } = new List<FileService>();
}
