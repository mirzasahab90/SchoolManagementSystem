import { useEffect, useState, type FormEvent } from "react";
import { Plus } from "lucide-react";
import { toast } from "sonner";
import { Modal } from "@/components/modal";
import { Btn, DataTable, EmptyState, Field, PageHeader, Panel, TextInput } from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import type { ModulePermissionCell, RoleDetail, RoleListItem } from "@/lib/types";

export function RolesPage() {
  const [items, setItems] = useState<RoleListItem[]>([]);
  const [detail, setDetail] = useState<RoleDetail | null>(null);
  const [matrix, setMatrix] = useState<ModulePermissionCell[]>([]);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [open, setOpen] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [newName, setNewName] = useState("");
  const [newDescription, setNewDescription] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  async function load() {
    setLoading(true);
    try {
      setItems((await unwrap(api.get("/roles"))) as RoleListItem[]);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to load roles");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  async function openEdit(id: number) {
    try {
      const role = (await unwrap(api.get(`/roles/${id}`))) as RoleDetail;
      setDetail(role);
      setName(role.name);
      setDescription(role.description ?? "");
      setIsActive(role.isActive);
      setMatrix(role.matrix.map((c) => ({ ...c })));
      setOpen(true);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to load role");
    }
  }

  function toggle(moduleKey: string, action: keyof Pick<ModulePermissionCell, "view" | "add" | "edit" | "delete">) {
    setMatrix((prev) =>
      prev.map((row) => (row.moduleKey === moduleKey ? { ...row, [action]: !row[action] } : row)),
    );
  }

  async function saveRole(e: FormEvent) {
    e.preventDefault();
    if (!detail) return;
    setSaving(true);
    try {
      await unwrap(
        api.put(`/roles/${detail.id}`, {
          name,
          description,
          isActive,
          matrix,
        }),
      );
      toast.success("Role permissions saved");
      setOpen(false);
      await load();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  async function createRole(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      const created = (await unwrap(
        api.post("/roles", { name: newName, description: newDescription }),
      )) as RoleDetail;
      toast.success("Role created");
      setCreateOpen(false);
      setNewName("");
      setNewDescription("");
      await load();
      await openEdit(created.id);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Create failed");
    } finally {
      setSaving(false);
    }
  }

  const isSuperAdminRole = detail?.name === "SuperAdmin";

  return (
    <div>
      <PageHeader
        title="Roles & permissions"
        description="Decide which menus each role can View / Add / Edit / Delete. Users then only see allowed menus for their campuses."
        actions={
          <Btn type="button" onClick={() => setCreateOpen(true)}>
            <Plus className="mr-2 h-4 w-4" />
            Add role
          </Btn>
        }
      />

      <Panel>
        {loading ? (
          <div className="text-sm text-[var(--muted-foreground)]">Loading…</div>
        ) : items.length === 0 ? (
          <EmptyState title="No roles" description="Restart API so system roles can seed." />
        ) : (
          <DataTable>
            <thead className="bg-[var(--muted)] text-xs uppercase tracking-wide text-[var(--muted-foreground)]">
              <tr>
                <th className="px-4 py-3">Role</th>
                <th className="px-4 py-3">Users</th>
                <th className="px-4 py-3">Permissions</th>
                <th className="px-4 py-3">Type</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {items.map((role) => (
                <tr key={role.id} className="border-t border-[var(--border)]">
                  <td className="px-4 py-3">
                    <div className="font-medium">{role.name}</div>
                    <div className="text-xs text-[var(--muted-foreground)]">{role.description || "—"}</div>
                  </td>
                  <td className="px-4 py-3">{role.userCount}</td>
                  <td className="px-4 py-3">{role.permissionCount}</td>
                  <td className="px-4 py-3">{role.isSystem ? "System" : "Custom"}</td>
                  <td className="px-4 py-3 text-right">
                    <button type="button" className="text-[var(--primary)]" onClick={() => openEdit(role.id)}>
                      Permissions
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </DataTable>
        )}
      </Panel>

      <Modal
        open={open}
        wide
        title={detail ? `Permissions · ${detail.name}` : "Permissions"}
        description={
          isSuperAdminRole
            ? "SuperAdmin always has full access. Description can still be edited."
            : "Tick CRUD rights per menu. Campus users will only see menus granted here."
        }
        onClose={() => setOpen(false)}
        footer={
          <>
            <Btn type="button" variant="ghost" onClick={() => setOpen(false)}>
              Cancel
            </Btn>
            <Btn type="submit" form="role-form" disabled={saving}>
              {saving ? "Saving…" : "Save"}
            </Btn>
          </>
        }
      >
        <form id="role-form" className="space-y-4" onSubmit={saveRole}>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Name">
              <TextInput
                value={name}
                onChange={(e) => setName(e.target.value)}
                required
                disabled={detail?.isSystem}
              />
            </Field>
            <Field label="Active">
              <label className="flex h-10 items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={isActive}
                  disabled={isSuperAdminRole}
                  onChange={(e) => setIsActive(e.target.checked)}
                />
                Role is active
              </label>
            </Field>
          </div>
          <Field label="Description">
            <TextInput value={description} onChange={(e) => setDescription(e.target.value)} />
          </Field>

          {!isSuperAdminRole ? (
            <div className="overflow-hidden rounded-xl border border-[var(--border)]">
              <table className="w-full text-left text-sm">
                <thead className="bg-[var(--muted)] text-xs uppercase tracking-wide text-[var(--muted-foreground)]">
                  <tr>
                    <th className="px-3 py-2">Menu</th>
                    <th className="px-3 py-2 text-center">View</th>
                    <th className="px-3 py-2 text-center">Add</th>
                    <th className="px-3 py-2 text-center">Edit</th>
                    <th className="px-3 py-2 text-center">Delete</th>
                  </tr>
                </thead>
                <tbody>
                  {matrix.map((row) => (
                    <tr key={row.moduleKey} className="border-t border-[var(--border)]">
                      <td className="px-3 py-2 font-medium">{row.moduleName}</td>
                      {(["view", "add", "edit", "delete"] as const).map((action) => (
                        <td key={action} className="px-3 py-2 text-center">
                          <input
                            type="checkbox"
                            checked={row[action]}
                            onChange={() => toggle(row.moduleKey, action)}
                          />
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </form>
      </Modal>

      <Modal
        open={createOpen}
        title="Add role"
        description="Create a role, then set its menu permissions."
        onClose={() => setCreateOpen(false)}
        footer={
          <>
            <Btn type="button" variant="ghost" onClick={() => setCreateOpen(false)}>
              Cancel
            </Btn>
            <Btn type="submit" form="create-role-form" disabled={saving}>
              {saving ? "Creating…" : "Create"}
            </Btn>
          </>
        }
      >
        <form id="create-role-form" className="space-y-3" onSubmit={createRole}>
          <Field label="Name">
            <TextInput value={newName} onChange={(e) => setNewName(e.target.value)} required />
          </Field>
          <Field label="Description">
            <TextInput value={newDescription} onChange={(e) => setNewDescription(e.target.value)} />
          </Field>
        </form>
      </Modal>
    </div>
  );
}
