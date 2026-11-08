import { useEffect, useMemo, useState, type FormEvent, type ReactNode } from "react";
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
  TextArea,
  TextInput,
  Toolbar,
} from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import type { Campus, SaveCampusRequest } from "@/lib/types";

const emptyForm: SaveCampusRequest = {
  code: "",
  name: "",
  address: "",
  city: "",
  state: "",
  country: "",
  postalCode: "",
  phone: "",
  email: "",
  website: "",
  inChargeName: "",
  inChargePhone: "",
  inChargeEmail: "",
  inChargeDesignation: "",
  establishedOn: null,
  studentCapacity: null,
  notes: "",
  isActive: true,
};

export function CampusesPage() {
  const { can } = useAuth();
  const [items, setItems] = useState<Campus[]>([]);
  const [form, setForm] = useState<SaveCampusRequest>(emptyForm);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [open, setOpen] = useState(false);
  const [detail, setDetail] = useState<Campus | null>(null);
  const [query, setQuery] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  async function load() {
    setLoading(true);
    try {
      setItems((await unwrap(api.get("/campuses"))) as Campus[]);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to load campuses");
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
    return items.filter((c) =>
      [c.code, c.name, c.city, c.state, c.country, c.inChargeName]
        .filter(Boolean)
        .some((v) => String(v).toLowerCase().includes(q)),
    );
  }, [items, query]);

  function openCreate() {
    setEditingId(null);
    setForm(emptyForm);
    setOpen(true);
  }

  function openEdit(campus: Campus) {
    setDetail(null);
    setEditingId(campus.id);
    setForm({
      code: campus.code,
      name: campus.name,
      address: campus.address ?? "",
      city: campus.city ?? "",
      state: campus.state ?? "",
      country: campus.country ?? "",
      postalCode: campus.postalCode ?? "",
      phone: campus.phone ?? "",
      email: campus.email ?? "",
      website: campus.website ?? "",
      inChargeName: campus.inChargeName ?? "",
      inChargePhone: campus.inChargePhone ?? "",
      inChargeEmail: campus.inChargeEmail ?? "",
      inChargeDesignation: campus.inChargeDesignation ?? "",
      establishedOn: campus.establishedOn ?? null,
      studentCapacity: campus.studentCapacity ?? null,
      notes: campus.notes ?? "",
      isActive: campus.isActive,
    });
    setOpen(true);
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      const payload = {
        ...form,
        establishedOn: form.establishedOn || null,
        studentCapacity:
          form.studentCapacity === null || Number.isNaN(Number(form.studentCapacity))
            ? null
            : Number(form.studentCapacity),
      };
      if (editingId) {
        await unwrap(api.put(`/campuses/${editingId}`, payload));
        toast.success("Campus updated");
      } else {
        await unwrap(api.post("/campuses", payload));
        toast.success("Campus created");
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
    if (!confirm("Delete this campus?")) return;
    try {
      await unwrap(api.delete(`/campuses/${id}`));
      toast.success("Campus deleted");
      setDetail(null);
      await load();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Delete failed");
    }
  }

  return (
    <div>
      <PageHeader
        title="Campuses"
        description="Branches of this school — where it is, who runs it, and capacity."
        actions={
          can("campuses", "Add") ? (
            <Btn type="button" onClick={openCreate}>
              <Plus className="mr-2 h-4 w-4" />
              Add campus
            </Btn>
          ) : null
        }
      />

      <Panel>
        <Toolbar>
          <SearchField value={query} onChange={setQuery} placeholder="Search code, city, in-charge…" />
          <div className="text-sm text-[var(--muted-foreground)]">
            {filtered.length} of {items.length}
          </div>
        </Toolbar>

        {loading ? (
          <div className="text-sm text-[var(--muted-foreground)]">Loading…</div>
        ) : filtered.length === 0 ? (
          <EmptyState
            title={items.length === 0 ? "No campuses yet" : "No matches"}
            description={
              items.length === 0
                ? "Add the first campus to start multi-branch operations."
                : "Try a different search."
            }
          />
        ) : (
          <DataTable>
            <thead className="bg-[var(--muted)] text-xs uppercase tracking-wide text-[var(--muted-foreground)]">
              <tr>
                <th className="px-4 py-3">Campus</th>
                <th className="px-4 py-3">Location</th>
                <th className="px-4 py-3">In charge</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((campus) => (
                <tr key={campus.id} className="border-t border-[var(--border)]">
                  <td className="px-4 py-3">
                    <button
                      type="button"
                      className="text-left"
                      onClick={() => setDetail(campus)}
                    >
                      <div className="font-medium text-[var(--primary)] hover:underline">
                        {campus.code} — {campus.name}
                      </div>
                      <div className="text-xs text-[var(--muted-foreground)]">
                        {campus.studentCapacity ? `Capacity ${campus.studentCapacity}` : "Capacity —"} ·{" "}
                        {campus.adminUserCount} users
                      </div>
                    </button>
                  </td>
                  <td className="px-4 py-3 text-sm">
                    <div>{[campus.city, campus.state, campus.country].filter(Boolean).join(", ") || "—"}</div>
                    <div className="text-xs text-[var(--muted-foreground)]">{campus.address || "—"}</div>
                  </td>
                  <td className="px-4 py-3 text-sm">
                    <div>{campus.inChargeName || "—"}</div>
                    <div className="text-xs text-[var(--muted-foreground)]">
                      {campus.inChargeDesignation || campus.inChargePhone || "—"}
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <StatusPill tone={campus.isActive ? "success" : "neutral"} dot>
                      {campus.isActive ? "Active" : "Inactive"}
                    </StatusPill>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <button
                      type="button"
                      className="mr-3 text-[var(--primary)]"
                      onClick={() => setDetail(campus)}
                    >
                      View
                    </button>
                    {can("campuses", "Edit") ? (
                      <button
                        type="button"
                        className="mr-3 text-[var(--primary)]"
                        onClick={() => openEdit(campus)}
                      >
                        Edit
                      </button>
                    ) : null}
                    {can("campuses", "Delete") ? (
                      <button type="button" className="text-red-600" onClick={() => remove(campus.id)}>
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
        open={!!detail}
        wide
        title={detail ? `${detail.code} — ${detail.name}` : "Campus"}
        description="Full campus profile"
        onClose={() => setDetail(null)}
        footer={
          <>
            <Btn type="button" variant="ghost" onClick={() => setDetail(null)}>
              Close
            </Btn>
            {detail && can("campuses", "Edit") ? (
              <Btn type="button" onClick={() => openEdit(detail)}>
                Edit campus
              </Btn>
            ) : null}
          </>
        }
      >
        {detail ? (
          <div className="space-y-5 text-sm">
            <div className="flex flex-wrap gap-2">
              <StatusPill tone={detail.isActive ? "success" : "neutral"} dot>
                {detail.isActive ? "Active" : "Inactive"}
              </StatusPill>
              {detail.studentCapacity ? (
                <StatusPill tone="info">Capacity {detail.studentCapacity}</StatusPill>
              ) : null}
              <StatusPill>{detail.adminUserCount} linked users</StatusPill>
            </div>

            <DetailBlock title="Location">
              <Row label="Address" value={detail.address} />
              <Row label="City" value={detail.city} />
              <Row label="State" value={detail.state} />
              <Row label="Country" value={detail.country} />
              <Row label="Postal code" value={detail.postalCode} />
            </DetailBlock>

            <DetailBlock title="Contact">
              <Row label="Phone" value={detail.phone} />
              <Row label="Email" value={detail.email} />
              <Row label="Website" value={detail.website} />
            </DetailBlock>

            <DetailBlock title="In charge">
              <Row label="Name" value={detail.inChargeName} />
              <Row label="Designation" value={detail.inChargeDesignation} />
              <Row label="Phone" value={detail.inChargePhone} />
              <Row label="Email" value={detail.inChargeEmail} />
            </DetailBlock>

            <DetailBlock title="Advanced">
              <Row label="Established" value={detail.establishedOn} />
              <Row label="Notes" value={detail.notes} />
            </DetailBlock>
          </div>
        ) : null}
      </Modal>

      <Modal
        open={open}
        wide
        title={editingId ? "Edit campus" : "Add campus"}
        description="Identity, location, contact, in-charge, and capacity."
        onClose={() => setOpen(false)}
        footer={
          <>
            <Btn type="button" variant="ghost" onClick={() => setOpen(false)}>
              Cancel
            </Btn>
            <Btn type="submit" form="campus-form" disabled={saving}>
              {saving ? "Saving…" : editingId ? "Update" : "Create"}
            </Btn>
          </>
        }
      >
        <form id="campus-form" className="space-y-4" onSubmit={onSubmit}>
          <SectionLabel>Identity</SectionLabel>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Code">
              <TextInput value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} required />
            </Field>
            <Field label="Name">
              <TextInput value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required />
            </Field>
          </div>

          <SectionLabel>Location</SectionLabel>
          <Field label="Address">
            <TextInput value={form.address} onChange={(e) => setForm({ ...form, address: e.target.value })} />
          </Field>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="City">
              <TextInput value={form.city} onChange={(e) => setForm({ ...form, city: e.target.value })} />
            </Field>
            <Field label="State / Province">
              <TextInput value={form.state} onChange={(e) => setForm({ ...form, state: e.target.value })} />
            </Field>
            <Field label="Country">
              <TextInput value={form.country} onChange={(e) => setForm({ ...form, country: e.target.value })} />
            </Field>
            <Field label="Postal code">
              <TextInput value={form.postalCode} onChange={(e) => setForm({ ...form, postalCode: e.target.value })} />
            </Field>
          </div>

          <SectionLabel>Contact</SectionLabel>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Phone">
              <TextInput value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
            </Field>
            <Field label="Email">
              <TextInput type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
            </Field>
          </div>
          <Field label="Website">
            <TextInput value={form.website} onChange={(e) => setForm({ ...form, website: e.target.value })} />
          </Field>

          <SectionLabel>In charge</SectionLabel>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Name">
              <TextInput
                value={form.inChargeName}
                onChange={(e) => setForm({ ...form, inChargeName: e.target.value })}
              />
            </Field>
            <Field label="Designation">
              <TextInput
                value={form.inChargeDesignation}
                onChange={(e) => setForm({ ...form, inChargeDesignation: e.target.value })}
              />
            </Field>
            <Field label="Phone">
              <TextInput
                value={form.inChargePhone}
                onChange={(e) => setForm({ ...form, inChargePhone: e.target.value })}
              />
            </Field>
            <Field label="Email">
              <TextInput
                type="email"
                value={form.inChargeEmail}
                onChange={(e) => setForm({ ...form, inChargeEmail: e.target.value })}
              />
            </Field>
          </div>

          <SectionLabel>Advanced</SectionLabel>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Established on">
              <TextInput
                type="date"
                value={form.establishedOn ?? ""}
                onChange={(e) => setForm({ ...form, establishedOn: e.target.value || null })}
              />
            </Field>
            <Field label="Student capacity">
              <TextInput
                type="number"
                min={0}
                value={form.studentCapacity ?? ""}
                onChange={(e) =>
                  setForm({
                    ...form,
                    studentCapacity: e.target.value === "" ? null : Number(e.target.value),
                  })
                }
              />
            </Field>
          </div>
          <Field label="Notes">
            <TextArea
              rows={3}
              value={form.notes}
              onChange={(e) => setForm({ ...form, notes: e.target.value })}
            />
          </Field>
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

function DetailBlock({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div>
      <SectionLabel>{title}</SectionLabel>
      <div className="mt-2 grid gap-2 sm:grid-cols-2">{children}</div>
    </div>
  );
}

function Row({ label, value }: { label: string; value?: string | number | null }) {
  return (
    <div className="rounded-lg border border-[var(--border)] bg-[var(--muted)]/40 px-3 py-2">
      <div className="text-[11px] uppercase tracking-wide text-[var(--muted-foreground)]">{label}</div>
      <div className="mt-0.5 font-medium break-words">{value || "—"}</div>
    </div>
  );
}
