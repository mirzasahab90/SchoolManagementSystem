import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Plus } from "lucide-react";
import { toast } from "sonner";
import { Modal } from "@/components/modal";
import {
  Btn,
  DataTable,
  EmptyState,
  Field,
  PageHeader,
  Panel,
  SearchField,
  StatusPill,
  TextInput,
  Toolbar,
} from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import type { ModulePermissionCell, RoleDetail, RoleListItem } from "@/lib/types";

type ActionKey = "view" | "add" | "edit" | "delete";

export function RolesPage() {
  const { can } = useAuth();
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
  const [query, setQuery] = useState("");
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

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return items;
    return items.filter((r) =>
      [r.name, r.description ?? ""].join(" ").toLowerCase().includes(q),
    );
  }, [items, query]);

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

  function toggle(moduleKey: string, action: ActionKey) {
    setMatrix((prev) =>
      prev.map((row) => {
        if (row.moduleKey !== moduleKey) return row;
        const next = { ...row, [action]: !row[action] };
        // Enabling Add/Edit/Delete implies View
        if (action !== "view" && next[action]) next.view = true;
        // Disabling View clears other CRUD
        if (action === "view" && !next.view) {
          next.add = false;
          next.edit = false;
          next.delete = false;
        }
        return next;
      }),
    );
  }

  function setColumn(action: ActionKey, value: boolean) {
    setMatrix((prev) =>
      prev.map((row) => {
        const next = { ...row, [action]: value };
        if (action !== "view" && value) next.view = true;
        if (action === "view" && !value) {
          next.add = false;
          next.edit = false;
          next.delete = false;
        }
        return next;
      }),
    );
  }

  function grantAll() {
    setMatrix((prev) =>
      prev.map((row) => ({ ...row, view: true, add: true, edit: true, delete: true })),
    );
  }

  function clearAll() {
    setMatrix((prev) =>
      prev.map((row) => ({ ...row, view: false, add: false, edit: false, delete: false })),
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

  async function remove(role: RoleListItem) {
    if (role.isSystem) {
      toast.error("System roles cannot be deleted");
      return;
    }
    if (!confirm(`Delete role “${role.name}”?`)) return;
    try {
      await unwrap(api.delete(`/roles/${role.id}`));
      toast.success("Role deleted");
      await load();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Delete failed");
    }
  }

  const isSuperAdminRole = detail?.name === "SuperAdmin";
  const grantedCount = matrix.reduce(
    (sum, row) => sum + [row.view, row.add, row.edit, row.delete].filter(Boolean).length,
    0,
  );

  return (
    <div>
      <PageHeader
        title="Roles & permissions"
        description="Decide which menus each role can View / Add / Edit / Delete. Users only see menus granted here for their campuses."
        actions={
          can("roles", "Add") ? (
            <Btn type="button" onClick={() => setCreateOpen(true)}>
              <Plus className="mr-2 h-4 w-4" />
              Add role
            </Btn>
          ) : null
        }
      />

      <Panel>
        <Toolbar>
          <SearchField value={query} onChange={setQuery} placeholder="Search roles…" />
          <div className="text-sm text-[var(--muted-foreground)]">
            {filtered.length} of {items.length}
          </div>
        </Toolbar>

        {loading ? (
          <div className="text-sm text-[var(--muted-foreground)]">Loading…</div>
        ) : filtered.length === 0 ? (
          <EmptyState title="No roles" description="Restart API so system roles can seed." />
        ) : (
          <DataTable>
            <thead className="bg-[var(--muted)] text-xs uppercase tracking-wide text-[var(--muted-foreground)]">
              <tr>
                <th className="px-4 py-3">Role</th>
                <th className="px-4 py-3">Users</th>
                <th className="px-4 py-3">Permissions</th>
                <th className="px-4 py-3">Type</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((role) => (
                <tr key={role.id} className="border-t border-[var(--border)]">
                  <td className="px-4 py-3">
                    <div className="font-medium">{role.name}</div>
                    <div className="text-xs text-[var(--muted-foreground)]">{role.description || "—"}</div>
                  </td>
                  <td className="px-4 py-3">{role.userCount}</td>
                  <td className="px-4 py-3">
                    {role.name === "SuperAdmin" ? "Full access" : `${role.permissionCount} grants`}
                  </td>
                  <td className="px-4 py-3">
                    <StatusPill tone={role.isSystem ? "info" : "neutral"}>
                      {role.isSystem ? "System" : "Custom"}
                    </StatusPill>
                  </td>
                  <td className="px-4 py-3">
                    <StatusPill tone={role.isActive ? "success" : "neutral"} dot>
                      {role.isActive ? "Active" : "Inactive"}
                    </StatusPill>
                  </td>
                  <td className="px-4 py-3 text-right">
                    {can("roles", "View") || can("roles", "Edit") ? (
                      <button
                        type="button"
                        className="mr-3 text-[var(--primary)]"
                        onClick={() => openEdit(role.id)}
                      >
                        Permissions
                      </button>
                    ) : null}
                    {!role.isSystem && can("roles", "Delete") ? (
                      <button type="button" className="text-red-600" onClick={() => remove(role)}>
                        Delete
                      </button>
                    ) : null}
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
            : `Tick CRUD rights per menu. Currently ${grantedCount} grants selected.`
        }
        onClose={() => setOpen(false)}
        footer={
          <>
            <Btn type="button" variant="ghost" onClick={() => setOpen(false)}>
              Cancel
            </Btn>
            {can("roles", "Edit") ? (
              <Btn type="submit" form="role-form" disabled={saving}>
                {saving ? "Saving…" : "Save"}
              </Btn>
            ) : null}
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
                disabled={detail?.isSystem || !can("roles", "Edit")}
              />
            </Field>
            <Field label="Active">
              <label className="flex h-10 items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={isActive}
                  disabled={isSuperAdminRole || !can("roles", "Edit")}
                  onChange={(e) => setIsActive(e.target.checked)}
                />
                Role is active
              </label>
            </Field>
          </div>
          <Field label="Description">
            <TextInput
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              disabled={!can("roles", "Edit")}
            />
          </Field>

          {!isSuperAdminRole ? (
            <div className="space-y-3">
              {can("roles", "Edit") ? (
                <div className="flex flex-wrap gap-2">
                  <Btn type="button" variant="secondary" className="h-8 text-xs" onClick={grantAll}>
                    Grant all
                  </Btn>
                  <Btn type="button" variant="ghost" className="h-8 text-xs" onClick={clearAll}>
                    Clear all
                  </Btn>
                </div>
              ) : null}
              <div className="overflow-hidden rounded-xl border border-[var(--border)]">
                <table className="w-full text-left text-sm">
                  <thead className="bg-[var(--muted)] text-xs uppercase tracking-wide text-[var(--muted-foreground)]">
                    <tr>
                      <th className="px-3 py-2">Menu</th>
                      {(["view", "add", "edit", "delete"] as const).map((action) => (
                        <th key={action} className="px-3 py-2 text-center">
                          <div>{action}</div>
                          {can("roles", "Edit") ? (
                            <button
                              type="button"
                              className="mt-1 text-[10px] font-normal normal-case text-[var(--primary)]"
                              onClick={() =>
                                setColumn(
                                  action,
                                  !matrix.every((r) => r[action]),
                                )
                              }
                            >
                              toggle
                            </button>
                          ) : null}
                        </th>
                      ))}
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
                              disabled={!can("roles", "Edit")}
                              onChange={() => toggle(row.moduleKey, action)}
                            />
                          </td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ) : (
            <div className="rounded-xl border border-[var(--border)] bg-[var(--muted)]/50 px-4 py-3 text-sm text-[var(--muted-foreground)]">
              SuperAdmin bypasses the permission matrix in code — every menu and action is allowed.
            </div>
          )}
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
