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
  SectionLabel,
  StatusPill,
  TextInput,
  Toolbar,
} from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import type { Campus, RoleListItem, SaveUserRequest, UserListItem } from "@/lib/types";

const emptyForm: SaveUserRequest = {
  firstName: "",
  lastName: "",
  email: "",
  phone: "",
  isActive: true,
  isHq: false,
  roleIds: [],
  campusIds: [],
  password: "",
};

export function UsersPage() {
  const { can } = useAuth();
  const [items, setItems] = useState<UserListItem[]>([]);
  const [roles, setRoles] = useState<RoleListItem[]>([]);
  const [campuses, setCampuses] = useState<Campus[]>([]);
  const [form, setForm] = useState<SaveUserRequest>(emptyForm);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  async function load() {
    setLoading(true);
    try {
      const [users, roleList, campusList] = await Promise.all([
        unwrap(api.get("/users")) as Promise<UserListItem[]>,
        unwrap(api.get("/roles")) as Promise<RoleListItem[]>,
        unwrap(api.get("/campuses")) as Promise<Campus[]>,
      ]);
      setItems(users);
      setRoles(roleList.filter((r) => r.isActive));
      setCampuses(campusList.filter((c) => c.isActive));
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to load users");
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
    return items.filter((u) =>
      [u.fullName, u.email, u.roles.map((r) => r.name).join(" ")]
        .join(" ")
        .toLowerCase()
        .includes(q),
    );
  }, [items, query]);

  function openCreate() {
    setEditingId(null);
    setForm(emptyForm);
    setOpen(true);
  }

  function openEdit(user: UserListItem) {
    setEditingId(user.id);
    setForm({
      firstName: user.firstName,
      lastName: user.lastName,
      email: user.email,
      phone: user.phone ?? "",
      isActive: user.isActive,
      isHq: user.isHq,
      roleIds: user.roles.map((r) => r.id),
      campusIds: user.campusIds,
      password: "",
    });
    setOpen(true);
  }

  function toggleId(list: number[], id: number) {
    return list.includes(id) ? list.filter((x) => x !== id) : [...list, id];
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (form.roleIds.length === 0) {
      toast.error("Select at least one role");
      return;
    }
    if (!form.isHq && form.campusIds.length === 0) {
      toast.error("Select at least one campus, or enable HQ access");
      return;
    }
    setSaving(true);
    try {
      const payload: SaveUserRequest = {
        ...form,
        password: form.password?.trim() ? form.password : undefined,
      };
      if (editingId) {
        await unwrap(api.put(`/users/${editingId}`, payload));
        toast.success("User updated");
      } else {
        await unwrap(api.post("/users", payload));
        toast.success("User created");
      }
      setOpen(false);
      await load();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  async function remove(id: number) {
    if (!confirm("Delete this user?")) return;
    try {
      await unwrap(api.delete(`/users/${id}`));
      toast.success("User deleted");
      await load();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Delete failed");
    }
  }

  const campusName = (ids: number[]) =>
    ids
      .map((id) => campuses.find((c) => c.id === id)?.code)
      .filter(Boolean)
      .join(", ") || "—";

  return (
    <div>
      <PageHeader
        title="Users"
        description="Create HQ / campus admin logins, assign a role, and choose which campuses they can access. Menus come from the role’s permissions."
        actions={
          can("users", "Add") ? (
            <Btn type="button" onClick={openCreate}>
              <Plus className="mr-2 h-4 w-4" />
              Add user
            </Btn>
          ) : null
        }
      />

      <Panel>
        <Toolbar>
          <SearchField value={query} onChange={setQuery} placeholder="Search name, email, role…" />
          <div className="text-sm text-[var(--muted-foreground)]">
            {filtered.length} of {items.length}
          </div>
        </Toolbar>

        {loading ? (
          <div className="text-sm text-[var(--muted-foreground)]">Loading…</div>
        ) : filtered.length === 0 ? (
          <EmptyState
            title={items.length === 0 ? "No users yet" : "No matches"}
            description={
              items.length === 0
                ? "Add an admin user and assign campus access."
                : "Try a different search."
            }
          />
        ) : (
          <DataTable>
            <thead className="bg-[var(--muted)] text-xs uppercase tracking-wide text-[var(--muted-foreground)]">
              <tr>
                <th className="px-4 py-3">User</th>
                <th className="px-4 py-3">Role</th>
                <th className="px-4 py-3">Campuses</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((user) => (
                <tr key={user.id} className="border-t border-[var(--border)]">
                  <td className="px-4 py-3">
                    <div className="font-medium">{user.fullName}</div>
                    <div className="text-xs text-[var(--muted-foreground)]">{user.email}</div>
                  </td>
                  <td className="px-4 py-3 text-sm">
                    <div className="flex flex-wrap gap-1">
                      {user.roles.length
                        ? user.roles.map((r) => (
                            <StatusPill key={r.id} tone="info">
                              {r.name}
                            </StatusPill>
                          ))
                        : "—"}
                    </div>
                  </td>
                  <td className="px-4 py-3 text-sm">
                    {user.isHq ? (
                      <StatusPill tone="info" dot>
                        HQ · all campuses
                      </StatusPill>
                    ) : (
                      campusName(user.campusIds)
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <StatusPill tone={user.isActive ? "success" : "neutral"} dot>
                      {user.isActive ? "Active" : "Inactive"}
                    </StatusPill>
                  </td>
                  <td className="px-4 py-3 text-right">
                    {can("users", "Edit") ? (
                      <button
                        type="button"
                        className="mr-3 text-[var(--primary)]"
                        onClick={() => openEdit(user)}
                      >
                        Edit
                      </button>
                    ) : null}
                    {can("users", "Delete") ? (
                      <button type="button" className="text-red-600" onClick={() => remove(user.id)}>
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
        title={editingId ? "Edit user" : "Add admin user"}
        description="Login + role + campus access. Menu visibility follows the role’s permission matrix."
        onClose={() => setOpen(false)}
        footer={
          <>
            <Btn type="button" variant="ghost" onClick={() => setOpen(false)}>
              Cancel
            </Btn>
            <Btn type="submit" form="user-form" disabled={saving}>
              {saving ? "Saving…" : editingId ? "Update" : "Create"}
            </Btn>
          </>
        }
      >
        <form id="user-form" className="space-y-4" onSubmit={onSubmit}>
          <SectionLabel>Profile</SectionLabel>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="First name">
              <TextInput
                value={form.firstName}
                onChange={(e) => setForm({ ...form, firstName: e.target.value })}
                required
              />
            </Field>
            <Field label="Last name">
              <TextInput
                value={form.lastName}
                onChange={(e) => setForm({ ...form, lastName: e.target.value })}
                required
              />
            </Field>
          </div>
          <Field label="Email">
            <TextInput
              type="email"
              value={form.email}
              onChange={(e) => setForm({ ...form, email: e.target.value })}
              required
            />
          </Field>
          <Field label="Phone">
            <TextInput value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
          </Field>
          <Field label={editingId ? "Password (leave blank to keep)" : "Password"}>
            <TextInput
              type="password"
              value={form.password}
              onChange={(e) => setForm({ ...form, password: e.target.value })}
              required={!editingId}
            />
          </Field>

          <SectionLabel>Role</SectionLabel>
          <div className="grid gap-2 sm:grid-cols-2">
            {roles.map((role) => (
              <label
                key={role.id}
                className="flex items-center gap-2 rounded-lg border border-[var(--border)] px-3 py-2 text-sm"
              >
                <input
                  type="checkbox"
                  checked={form.roleIds.includes(role.id)}
                  onChange={() => setForm({ ...form, roleIds: toggleId(form.roleIds, role.id) })}
                />
                <span>
                  <span className="font-medium">{role.name}</span>
                  {role.description ? (
                    <span className="block text-xs text-[var(--muted-foreground)]">{role.description}</span>
                  ) : null}
                </span>
              </label>
            ))}
          </div>

          <SectionLabel>Campus access</SectionLabel>
          <label className="mb-2 flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={form.isHq}
              onChange={(e) => setForm({ ...form, isHq: e.target.checked })}
            />
            HQ access (all campuses)
          </label>
          <div className="grid gap-2 sm:grid-cols-2">
            {campuses.map((campus) => (
              <label
                key={campus.id}
                className="flex items-center gap-2 rounded-lg border border-[var(--border)] px-3 py-2 text-sm"
              >
                <input
                  type="checkbox"
                  checked={form.campusIds.includes(campus.id)}
                  onChange={() => setForm({ ...form, campusIds: toggleId(form.campusIds, campus.id) })}
                  disabled={form.isHq}
                />
                {campus.code} — {campus.name}
              </label>
            ))}
          </div>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={form.isActive}
              onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
            />
            Active
          </label>
        </form>
      </Modal>
    </div>
  );
}
