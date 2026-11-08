using SchoolManagementSystem.Domain.Common;

namespace SchoolManagementSystem.Domain.Entities.Identity;

/// <summary>A menu entry / screen. Sub-menus point to their parent through ParentId.</summary>
public class Module : Entity
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Route { get; set; }
    public string? Icon { get; set; }
    public int? ParentId { get; set; }
    public Module? Parent { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Module> Children { get; set; } = new List<Module>();
}
