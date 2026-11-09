import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Pencil, Plus, Trash2, Users as UsersIcon } from "lucide-react";
import { toast } from "sonner";
import { ConfirmDialog, Modal } from "@/components/modal";
import {
  Avatar,
  Btn,
  ChoiceCard,
  DataTable,
  EmptyState,
  Field,
  FormSection,
  IconButton,
  PageHeader,
  Panel,
  SearchField,
  Select,
  StatusPill,
  Switch,
  TableSkeleton,
  Td,
  TextInput,
  THead,
  Toolbar,
  Tr,
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

type StatusFilter = "all" | "active" | "inactive";

function formatLastLogin(value?: string | null) {
  if (!value) return "Never";
  const date = new Date(value.endsWith("Z") ? value : `${value}Z`);
  const diff = Date.now() - date.getTime();
  const minutes = Math.round(diff / 60000);
  if (minutes < 1) return "Just now";
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  const days = Math.round(hours / 24);
  if (days < 30) return `${days} d ago`;
  return date.toLocaleDateString();
}

export function UsersPage() {
  const { can, user: me } = useAuth();
  const [items, setItems] = useState<UserListItem[]>([]);
  const [roles, setRoles] = useState<RoleListItem[]>([]);
  const [campuses, setCampuses] = useState<Campus[]>([]);
  const [form, setForm] = useState<SaveUserRequest>(emptyForm);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState<StatusFilter>("all");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<UserListItem | null>(null);
  const [deleting, setDeleting] = useState(false);

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
    return items.filter((u) => {
      if (status === "active" && !u.isActive) return false;
      if (status === "inactive" && u.isActive) return false;
      if (!q) return true;
      return [u.fullName, u.email, u.roles.map((r) => r.name).join(" ")].join(" ").toLowerCase().includes(q);
    });
  }, [items, query, status]);

  // Only a SuperAdmin may hand out the SuperAdmin role.
  const assignableRoles = roles.filter((r) => r.name !== "SuperAdmin" || me?.isSuperAdmin);
  const editingSelf = editingId != null && editingId === me?.id;
  const lockAccess = editingSelf && !me?.isSuperAdmin;

  function openCreate() {
    setEditingId(null);
    setForm({ ...emptyForm, campusIds: me?.activeCampusId ? [me.activeCampusId] : [] });
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

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await unwrap(api.delete(`/users/${pendingDelete.id}`));
      toast.success("User deleted");
      setPendingDelete(null);
      await load();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Delete failed");
    } finally {
      setDeleting(false);
    }
  }

  const campusCodes = (ids: number[]) => ids.map((id) => campuses.find((c) => c.id === id)?.code).filter(Boolean) as string[];
  const activeCount = items.filter((u) => u.isActive).length;

  return (
    <div>
      <PageHeader
        title="Users"
        description="Logins for head office and campus staff. Roles decide what they can do; campuses decide where."
        actions={
          can("users", "Add") ? (
            <Btn type="button" onClick={openCreate}>
              <Plus />
              Add user
            </Btn>
          ) : null
        }
      />

      <Panel flush>
        <Toolbar>
          <div className="flex w-full flex-wrap items-center gap-2 sm:w-auto">
            <SearchField value={query} onChange={setQuery} placeholder="Search name, email or role" />
            <Select
              value={status}
              onChange={(e) => setStatus(e.target.value as StatusFilter)}
              aria-label="Filter by status"
              className="w-auto"
            >
              <option value="all">All statuses</option>
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </Select>
          </div>
          <div className="text-xs text-[var(--muted-foreground)]">
            <span className="tabular font-medium text-[var(--foreground)]">{items.length}</span> users ·{" "}
            <span className="tabular">{activeCount}</span> active
          </div>
        </Toolbar>

        {loading ? (
          <TableSkeleton cols={5} />
        ) : filtered.length === 0 ? (
          <EmptyState
            icon={UsersIcon}
            title={items.length === 0 ? "No users yet" : "No matching users"}
            description={
              items.length === 0 ? "Add a login and choose the campuses it can work in." : "Try a different search or filter."
            }
            action={
              items.length === 0 && can("users", "Add") ? (
                <Btn type="button" onClick={openCreate}>
                  <Plus />
                  Add user
                </Btn>
              ) : null
            }
          />
        ) : (
          <DataTable>
            <THead
              columns={[
                { label: "User" },
                { label: "Roles" },
                { label: "Campus access" },
                { label: "Last sign-in" },
                { label: "Status" },
                { label: "Actions", align: "right" },
              ]}
            />
            <tbody>
              {filtered.map((user) => {
                const codes = campusCodes(user.campusIds);
                return (
                  <Tr key={user.id}>
                    <Td>
                      <div className="flex items-center gap-3">
                        <Avatar name={user.fullName} />
                        <div className="min-w-0">
                          <div className="flex items-center gap-2 font-medium">
                            <span className="truncate">{user.fullName}</span>
                            {user.id === me?.id ? <StatusPill tone="primary">You</StatusPill> : null}
                          </div>
                          <div className="truncate text-xs text-[var(--muted-foreground)]">{user.email}</div>
                        </div>
                      </div>
                    </Td>
                    <Td>
                      <div className="flex flex-wrap gap-1">
                        {user.roles.length ? user.roles.map((r) => <StatusPill key={r.id}>{r.name}</StatusPill>) : "—"}
                      </div>
                    </Td>
                    <Td>
                      {user.isHq ? (
                        <StatusPill tone="info" dot>
                          All campuses (HQ)
                        </StatusPill>
                      ) : codes.length ? (
                        <div className="flex flex-wrap gap-1">
                          {codes.slice(0, 3).map((code) => (
                            <span
                              key={code}
                              className="rounded-md border border-[var(--border)] px-1.5 py-0.5 font-mono text-[11px] text-[var(--muted-foreground)]"
                            >
                              {code}
                            </span>
                          ))}
                          {codes.length > 3 ? (
                            <span className="text-xs text-[var(--muted-foreground)]">+{codes.length - 3}</span>
                          ) : null}
                        </div>
                      ) : (
                        <span className="text-[var(--muted-foreground)]">—</span>
                      )}
                    </Td>
                    <Td className="text-[var(--muted-foreground)]">{formatLastLogin(user.lastLoginOn)}</Td>
                    <Td>
                      <StatusPill tone={user.isActive ? "success" : "neutral"} dot>
                        {user.isActive ? "Active" : "Inactive"}
                      </StatusPill>
                    </Td>
                    <Td align="right">
                      <div className="flex justify-end gap-0.5">
                        {can("users", "Edit") ? (
                          <IconButton label={`Edit ${user.fullName}`} onClick={() => openEdit(user)}>
                            <Pencil />
                          </IconButton>
                        ) : null}
                        {can("users", "Delete") && user.id !== me?.id ? (
                          <IconButton label={`Delete ${user.fullName}`} tone="danger" onClick={() => setPendingDelete(user)}>
                            <Trash2 />
                          </IconButton>
                        ) : null}
                      </div>
                    </Td>
                  </Tr>
                );
              })}
            </tbody>
          </DataTable>
        )}
      </Panel>

      <Modal
        open={open}
        wide
        title={editingId ? "Edit user" : "Add user"}
        description="Profile, sign-in, roles and campus access."
        onClose={() => setOpen(false)}
        footer={
          <>
            <Btn type="button" variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Btn>
            <Btn type="submit" form="user-form" disabled={saving}>
              {saving ? "Saving…" : editingId ? "Save changes" : "Create user"}
            </Btn>
          </>
        }
      >
        <form id="user-form" className="space-y-6" onSubmit={onSubmit}>
          <FormSection title="Profile">
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="First name" required>
                <TextInput value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} required />
              </Field>
              <Field label="Last name" required>
                <TextInput value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} required />
              </Field>
              <Field label="Email" required>
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
              <Field
                label="Password"
                required={!editingId}
                hint={editingId ? "Leave blank to keep the current password." : "At least 8 characters."}
                className="sm:col-span-2"
              >
                <TextInput
                  type="password"
                  autoComplete="new-password"
                  minLength={8}
                  value={form.password}
                  onChange={(e) => setForm({ ...form, password: e.target.value })}
                  required={!editingId}
                />
              </Field>
            </div>
            <Switch
              checked={form.isActive}
              onChange={(v) => setForm({ ...form, isActive: v })}
              label="Account active"
              description="Inactive users cannot sign in."
              disabled={editingSelf}
            />
          </FormSection>

          <FormSection
            title="Roles"
            description={lockAccess ? "You cannot change your own roles." : "What this user is allowed to do."}
          >
            <div className="grid gap-2 sm:grid-cols-2">
              {assignableRoles.map((role) => (
                <ChoiceCard
                  key={role.id}
                  checked={form.roleIds.includes(role.id)}
                  onToggle={() => setForm({ ...form, roleIds: toggleId(form.roleIds, role.id) })}
                  title={role.name}
                  description={role.description}
                  disabled={lockAccess}
                />
              ))}
            </div>
          </FormSection>

          <FormSection
            title="Campus access"
            description={lockAccess ? "You cannot change your own campus access." : "Where this user can work."}
          >
            {me?.isHq ? (
              <Switch
                checked={form.isHq}
                onChange={(v) => setForm({ ...form, isHq: v })}
                label="Head office (HQ) access"
                description="Can work across every campus, including new ones."
                disabled={lockAccess}
              />
            ) : null}
            <div className="grid gap-2 sm:grid-cols-2">
              {campuses.map((campus) => (
                <ChoiceCard
                  key={campus.id}
                  checked={form.isHq || form.campusIds.includes(campus.id)}
                  onToggle={() => setForm({ ...form, campusIds: toggleId(form.campusIds, campus.id) })}
                  title={campus.name}
                  description={campus.code}
                  disabled={form.isHq || lockAccess}
                  badge={
                    !form.isHq && form.campusIds[0] === campus.id ? <StatusPill tone="primary">Primary</StatusPill> : null
                  }
                />
              ))}
            </div>
            {!form.isHq && form.campusIds.length > 1 ? (
              <p className="text-xs text-[var(--muted-foreground)]">The first campus you pick is the user’s primary campus.</p>
            ) : null}
          </FormSection>
        </form>
      </Modal>

      <ConfirmDialog
        open={!!pendingDelete}
        title="Delete user?"
        description={
          pendingDelete ? `${pendingDelete.fullName} will no longer be able to sign in. This cannot be undone from the app.` : ""
        }
        busy={deleting}
        onConfirm={confirmDelete}
        onClose={() => setPendingDelete(null)}
      />
    </div>
  );
}
