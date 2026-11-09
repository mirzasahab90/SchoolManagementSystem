import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Info, KeyRound, Lock, Plus, Shield, ShieldCheck, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { ConfirmDialog, Modal } from "@/components/modal";
import {
  Btn,
  CheckMark,
  DataTable,
  EmptyState,
  Field,
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
import { canWriteOwned, OwnerPill } from "@/components/owner-pill";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import type { ModulePermissionCell, RoleDetail, RoleListItem } from "@/lib/types";
import { cn } from "@/lib/utils";

type ActionKey = "view" | "add" | "edit" | "delete";
const ACTIONS: { key: ActionKey; label: string }[] = [
  { key: "view", label: "View" },
  { key: "add", label: "Add" },
  { key: "edit", label: "Edit" },
  { key: "delete", label: "Delete" },
];

export function RolesPage() {
  const { can, user: me } = useAuth();
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
  const [pendingDelete, setPendingDelete] = useState<RoleListItem | null>(null);
  const [deleting, setDeleting] = useState(false);

  // HQ roles are shared by every campus and only HQ changes them; a campus manages its own roles.
  const isHq = !!me?.isHq;
  const holdsRole = !!detail && !!me?.roles.includes(detail.name);
  const isSuperAdminRole = detail?.name === "SuperAdmin";
  const ownsDetail = !!detail && canWriteOwned(me, detail.campusId);
  const canEdit =
    ownsDetail && can("roles", "Edit") && (!holdsRole || !!me?.isSuperAdmin) && (!isSuperAdminRole || !!me?.isSuperAdmin);
  const canAdd = can("roles", "Add") && (isHq || me?.activeCampusId != null);
  const [newOwner, setNewOwner] = useState<string>("hq");
  /** Outside HQ a role can only be given permissions its editor holds (the API enforces this too). */
  const cellAllowed = (moduleKey: string, action: ActionKey) => isHq || can(moduleKey, action);

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
    return items.filter((r) => [r.name, r.description ?? ""].join(" ").toLowerCase().includes(q));
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

  function applyRule(row: ModulePermissionCell, action: ActionKey, value: boolean) {
    const next = { ...row, [action]: value };
    // Add / Edit / Delete need View; removing View removes the rest.
    if (action !== "view" && value) next.view = true;
    if (action === "view" && !value) {
      next.add = false;
      next.edit = false;
      next.delete = false;
    }
    return next;
  }

  function toggle(moduleKey: string, action: ActionKey) {
    setMatrix((prev) => prev.map((row) => (row.moduleKey === moduleKey ? applyRule(row, action, !row[action]) : row)));
  }

  function setColumn(action: ActionKey, value: boolean) {
    setMatrix((prev) => prev.map((row) => (cellAllowed(row.moduleKey, action) ? applyRule(row, action, value) : row)));
  }

  function setRow(moduleKey: string, value: boolean) {
    setMatrix((prev) =>
      prev.map((row) =>
        row.moduleKey === moduleKey
          ? {
              ...row,
              view: cellAllowed(moduleKey, "view") ? value : row.view,
              add: cellAllowed(moduleKey, "add") ? value : row.add,
              edit: cellAllowed(moduleKey, "edit") ? value : row.edit,
              delete: cellAllowed(moduleKey, "delete") ? value : row.delete,
            }
          : row,
      ),
    );
  }

  async function saveRole(e: FormEvent) {
    e.preventDefault();
    if (!detail) return;
    setSaving(true);
    try {
      await unwrap(api.put(`/roles/${detail.id}`, { name, description, isActive, matrix }));
      toast.success("Permissions saved");
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
      const campusId = isHq && me?.activeCampusId == null && newOwner !== "hq" ? Number(newOwner) : undefined;
      const created = (await unwrap(api.post("/roles", { name: newName, description: newDescription, campusId }))) as RoleDetail;
      toast.success("Role created — now choose its permissions");
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

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await unwrap(api.delete(`/roles/${pendingDelete.id}`));
      toast.success("Role deleted");
      setPendingDelete(null);
      await load();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Delete failed");
    } finally {
      setDeleting(false);
    }
  }

  const grantedCount = matrix.reduce((sum, row) => sum + ACTIONS.filter((a) => row[a.key]).length, 0);
  // Ticked permissions the editor does not hold (e.g. added by HQ). The API refuses to save these, so the
  // editor must untick them or ask HQ — say so instead of failing on save.
  const beyondMe = canEdit
    ? matrix.flatMap((row) => ACTIONS.filter((a) => row[a.key] && !cellAllowed(row.moduleKey, a.key)).map((a) => `${row.moduleName} ${a.label}`))
    : [];
  const totalCells = matrix.length * ACTIONS.length;

  return (
    <div>
      <PageHeader
        title="Roles & permissions"
        description="Roles decide what people can view, add, edit or delete. Head-office roles are shared by every campus; a campus can add its own."
        actions={
          canAdd ? (
            <Btn type="button" onClick={() => setCreateOpen(true)}>
              <Plus />
              Add role
            </Btn>
          ) : null
        }
      />

      {!isHq ? (
        <div className="mb-5 flex items-start gap-3 rounded-xl border border-[var(--border)] bg-[var(--info-soft)] px-4 py-3 text-sm text-[var(--info)]">
          <Info className="mt-0.5 size-4 shrink-0" />
          Head-office roles (HQ) are shared by every campus and are read only for you. Add roles for your own campus when you need
          something different.
        </div>
      ) : null}

      <Panel flush>
        <Toolbar>
          <SearchField value={query} onChange={setQuery} placeholder="Search roles" />
          <div className="text-xs text-[var(--muted-foreground)]">
            <span className="tabular font-medium text-[var(--foreground)]">{items.length}</span> roles
          </div>
        </Toolbar>

        {loading ? (
          <TableSkeleton cols={5} />
        ) : filtered.length === 0 ? (
          <EmptyState icon={Shield} title="No roles found" description="Try a different search." />
        ) : (
          <DataTable>
            <THead
              columns={[
                { label: "Role" },
                { label: "Owner" },
                { label: "Users" },
                { label: "Permissions" },
                { label: "Type" },
                { label: "Status" },
                { label: "Actions", align: "right" },
              ]}
            />
            <tbody>
              {filtered.map((role) => (
                <Tr key={role.id} onClick={() => openEdit(role.id)}>
                  <Td>
                    <div className="flex items-center gap-3">
                      <span
                        className={cn(
                          "inline-flex size-9 shrink-0 items-center justify-center rounded-lg",
                          role.name === "SuperAdmin"
                            ? "bg-[var(--primary-soft)] text-[var(--primary-soft-foreground)]"
                            : "bg-[var(--muted)] text-[var(--muted-foreground)]",
                        )}
                      >
                        {role.name === "SuperAdmin" ? <ShieldCheck className="size-4" /> : <KeyRound className="size-4" />}
                      </span>
                      <div className="min-w-0">
                        <div className="flex items-center gap-2 font-medium">
                          {role.name}
                          {me?.roles.includes(role.name) ? <StatusPill tone="primary">Yours</StatusPill> : null}
                        </div>
                        <div className="truncate text-xs text-[var(--muted-foreground)]">{role.description || "No description"}</div>
                      </div>
                    </div>
                  </Td>
                  <Td>
                    <OwnerPill campusCode={role.campusCode} locked={!canWriteOwned(me, role.campusId)} />
                  </Td>
                  <Td className="tabular">{role.userCount}</Td>
                  <Td>
                    {role.name === "SuperAdmin" ? (
                      <StatusPill tone="primary">Full access</StatusPill>
                    ) : (
                      <span className="tabular text-[var(--muted-foreground)]">{role.permissionCount} grants</span>
                    )}
                  </Td>
                  <Td>
                    <StatusPill tone={role.isSystem ? "info" : "neutral"}>{role.isSystem ? "System" : "Custom"}</StatusPill>
                  </Td>
                  <Td>
                    <StatusPill tone={role.isActive ? "success" : "neutral"} dot>
                      {role.isActive ? "Active" : "Inactive"}
                    </StatusPill>
                  </Td>
                  <Td align="right">
                    <div className="flex justify-end gap-1" onClick={(e) => e.stopPropagation()}>
                      <Btn type="button" size="sm" variant="outline" onClick={() => openEdit(role.id)}>
                        {canWriteOwned(me, role.campusId) && can("roles", "Edit") ? "Permissions" : "View"}
                      </Btn>
                      {!role.isSystem && canWriteOwned(me, role.campusId) && can("roles", "Delete") ? (
                        <IconButton label={`Delete ${role.name}`} tone="danger" onClick={() => setPendingDelete(role)}>
                          <Trash2 />
                        </IconButton>
                      ) : null}
                    </div>
                  </Td>
                </Tr>
              ))}
            </tbody>
          </DataTable>
        )}
      </Panel>

      <Modal
        open={open}
        wide
        title={detail ? detail.name : "Permissions"}
        description={
          isSuperAdminRole
            ? "SuperAdmin always has full access to every menu and campus."
            : `${grantedCount} of ${totalCells} permissions granted`
        }
        onClose={() => setOpen(false)}
        footer={
          <>
            <Btn type="button" variant="outline" onClick={() => setOpen(false)}>
              {canEdit ? "Cancel" : "Close"}
            </Btn>
            {canEdit ? (
              <Btn type="submit" form="role-form" disabled={saving || beyondMe.length > 0}>
                {saving ? "Saving…" : "Save permissions"}
              </Btn>
            ) : null}
          </>
        }
      >
        <form id="role-form" className="space-y-5" onSubmit={saveRole}>
          {detail && !ownsDetail ? (
            <div className="flex items-start gap-2.5 rounded-lg bg-[var(--info-soft)] px-3 py-2.5 text-sm text-[var(--info)]">
              <Lock className="mt-0.5 size-4 shrink-0" />
              Head-office role, shared by every campus. Only HQ can change it.
            </div>
          ) : null}
          {ownsDetail && holdsRole && !me?.isSuperAdmin ? (
            <div className="flex items-start gap-2.5 rounded-lg bg-[var(--warning-soft)] px-3 py-2.5 text-sm text-[var(--warning)]">
              <Lock className="mt-0.5 size-4 shrink-0" />
              You hold this role, so you cannot change its permissions. Ask a SuperAdmin.
            </div>
          ) : null}
          {beyondMe.length > 0 ? (
            <div className="flex items-start gap-2.5 rounded-lg bg-[var(--warning-soft)] px-3 py-2.5 text-sm text-[var(--warning)]">
              <Lock className="mt-0.5 size-4 shrink-0" />
              <span>
                This role has permissions you don&apos;t have ({beyondMe.join(", ")}), probably added by head office. Untick them to
                save your changes, or ask HQ to make the change.
              </span>
            </div>
          ) : null}

          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Role name" hint={detail?.isSystem ? "System role names cannot change." : undefined}>
              <TextInput value={name} onChange={(e) => setName(e.target.value)} required disabled={detail?.isSystem || !canEdit} />
            </Field>
            <Field label="Description">
              <TextInput value={description} onChange={(e) => setDescription(e.target.value)} disabled={!canEdit} />
            </Field>
          </div>
          <Switch
            checked={isActive}
            onChange={setIsActive}
            label="Role active"
            description="Users with an inactive role lose its permissions."
            disabled={isSuperAdminRole || !canEdit}
          />

          {!isSuperAdminRole ? (
            <div className="overflow-hidden rounded-xl border border-[var(--border)]">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-[var(--border)] bg-[var(--muted)]/60">
                    <th className="px-4 py-2.5 text-left text-xs font-medium text-[var(--muted-foreground)]">Menu</th>
                    {ACTIONS.map((a) => {
                      const all = matrix.length > 0 && matrix.every((r) => r[a.key]);
                      return (
                        <th key={a.key} className="w-20 px-2 py-2.5 text-center text-xs font-medium text-[var(--muted-foreground)]">
                          <button
                            type="button"
                            disabled={!canEdit}
                            onClick={() => setColumn(a.key, !all)}
                            title={canEdit ? `Toggle ${a.label} for every menu` : undefined}
                            className="inline-flex flex-col items-center gap-1.5 rounded-md px-1.5 py-0.5 enabled:hover:text-[var(--foreground)]"
                          >
                            {a.label}
                            <CheckMark checked={all} className="mt-0" />
                          </button>
                        </th>
                      );
                    })}
                  </tr>
                </thead>
                <tbody>
                  {matrix.map((row) => {
                    const full = ACTIONS.every((a) => row[a.key]);
                    return (
                      <tr key={row.moduleKey} className="border-b border-[var(--border)] last:border-b-0 hover:bg-[var(--card-hover)]">
                        <td className="px-4 py-2.5">
                          <button
                            type="button"
                            disabled={!canEdit}
                            onClick={() => setRow(row.moduleKey, !full)}
                            title={canEdit ? "Toggle every action for this menu" : undefined}
                            className="font-medium enabled:hover:text-[var(--primary)]"
                          >
                            {row.moduleName}
                          </button>
                        </td>
                        {ACTIONS.map((a) => (
                          <td key={a.key} className="px-2 py-2.5 text-center">
                            <button
                              type="button"
                              role="checkbox"
                              aria-checked={row[a.key]}
                              aria-label={`${a.label} ${row.moduleName}`}
                              // A permission the editor lacks can be removed, never added.
                              disabled={!canEdit || (!cellAllowed(row.moduleKey, a.key) && !row[a.key])}
                              title={canEdit && !cellAllowed(row.moduleKey, a.key) ? "You don't have this permission" : undefined}
                              onClick={() => toggle(row.moduleKey, a.key)}
                              className="inline-flex rounded p-1 outline-none focus-visible:ring-4 focus-visible:ring-[var(--ring)] disabled:cursor-not-allowed disabled:opacity-60"
                            >
                              <CheckMark checked={row[a.key]} className="mt-0" />
                            </button>
                          </td>
                        ))}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="flex items-start gap-3 rounded-xl border border-[var(--border)] bg-[var(--primary-soft)]/60 px-4 py-3 text-sm">
              <ShieldCheck className="mt-0.5 size-4 shrink-0 text-[var(--primary)]" />
              SuperAdmin skips the permission matrix — every menu, action and campus is allowed.
            </div>
          )}
        </form>
      </Modal>

      <Modal
        open={createOpen}
        title="Add role"
        description="Create the role first, then choose its permissions."
        onClose={() => setCreateOpen(false)}
        footer={
          <>
            <Btn type="button" variant="outline" onClick={() => setCreateOpen(false)}>
              Cancel
            </Btn>
            <Btn type="submit" form="create-role-form" disabled={saving}>
              {saving ? "Creating…" : "Create role"}
            </Btn>
          </>
        }
      >
        <form id="create-role-form" className="space-y-4" onSubmit={createRole}>
          <Field label="Role name" required>
            <TextInput value={newName} onChange={(e) => setNewName(e.target.value)} placeholder="e.g. Admissions officer" required autoFocus />
          </Field>
          <Field label="Description">
            <TextInput value={newDescription} onChange={(e) => setNewDescription(e.target.value)} placeholder="What this role is for" />
          </Field>
          {isHq && me?.activeCampusId == null ? (
            <Field label="Owner" hint="HQ roles are shared by every campus.">
              <Select value={newOwner} onChange={(e) => setNewOwner(e.target.value)}>
                <option value="hq">Head office (all campuses)</option>
                {me?.campuses.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name} only
                  </option>
                ))}
              </Select>
            </Field>
          ) : (
            <p className="text-xs text-[var(--muted-foreground)]">
              {isHq ? "This role will be shared by every campus." : "This role will belong to your campus only."}
            </p>
          )}
        </form>
      </Modal>

      <ConfirmDialog
        open={!!pendingDelete}
        title="Delete role?"
        description={pendingDelete ? `The “${pendingDelete.name}” role will be removed. Roles still assigned to users cannot be deleted.` : ""}
        busy={deleting}
        onConfirm={confirmDelete}
        onClose={() => setPendingDelete(null)}
      />
    </div>
  );
}
