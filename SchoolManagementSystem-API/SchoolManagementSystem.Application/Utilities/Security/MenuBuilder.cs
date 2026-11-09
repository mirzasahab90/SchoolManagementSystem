using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs.Auth;
using SchoolManagementSystem.Domain.Entities.Identity;

namespace SchoolManagementSystem.Application.Utilities.Security;

public static class MenuBuilder
{
    /// <summary>
    /// Menus the user may view. "My campus" needs a working campus (hidden for HQ on All campuses);
    /// "Campuses" (manage every branch) is head-office only — campus users use My campus instead.
    /// </summary>
    public static IReadOnlyList<MenuItemDto> Build(IReadOnlyList<Module> modules, UserAccessDto access, bool isHq, int? activeCampusId)
    {
        bool Hidden(Module m) =>
            (m.Key == AppModules.MyCampus && activeCampusId == null)
            || (m.Key == AppModules.Campuses && !isHq);

        bool CanSee(Module m) => !Hidden(m) && (access.IsSuperAdmin || access.Can(m.Key, "View"));

        var visibleKeys = modules.Where(CanSee).Select(m => m.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Parent stays if any child is visible (Settings group).
        foreach (var parent in modules.Where(m => m.ParentId == null))
        {
            var childVisible = modules.Any(c => c.ParentId == parent.Id && visibleKeys.Contains(c.Key));
            if (childVisible)
            {
                visibleKeys.Add(parent.Key);
            }
        }

        var visible = modules.Where(m => visibleKeys.Contains(m.Key)).ToList();

        MenuItemDto Map(Module module) =>
            new(module.Key, module.Name, module.Route, module.Icon, module.DisplayOrder,
                visible.Where(c => c.ParentId == module.Id).OrderBy(c => c.DisplayOrder).Select(Map).ToList());

        return visible.Where(m => m.ParentId == null).OrderBy(m => m.DisplayOrder).Select(Map).ToList();
    }
}
