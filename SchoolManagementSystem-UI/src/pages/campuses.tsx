import { useEffect, useState, type FormEvent } from "react";
import { Plus } from "lucide-react";
import { toast } from "sonner";
import { Modal } from "@/components/modal";
import { Btn, DataTable, EmptyState, Field, PageHeader, Panel, TextInput } from "@/components/sms-ui";
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
  const [items, setItems] = useState<Campus[]>([]);
  const [form, setForm] = useState<SaveCampusRequest>(emptyForm);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [open, setOpen] = useState(false);
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

  function openCreate() {
    setEditingId(null);
    setForm(emptyForm);
    setOpen(true);
  }

  function openEdit(campus: Campus) {
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
        studentCapacity: form.studentCapacity === null || form.studentCapacity === ("" as unknown as number)
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
      await load();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Delete failed");
    }
  }

  return (
    <div>
      <PageHeader
        title="Campuses"
        description="Branches of this school — location, in-charge, and capacity."
        actions={
          <Btn type="button" onClick={openCreate}>
            <Plus className="mr-2 h-4 w-4" />
            Add campus
          </Btn>
        }
      />

      <Panel>
        {loading ? (
          <div className="text-sm text-[var(--muted-foreground)]">Loading…</div>
        ) : items.length === 0 ? (
          <EmptyState title="No campuses yet" description="Add the first campus to start multi-branch operations." />
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
              {items.map((campus) => (
                <tr key={campus.id} className="border-t border-[var(--border)]">
                  <td className="px-4 py-3">
                    <div className="font-medium">
                      {campus.code} — {campus.name}
                    </div>
                    <div className="text-xs text-[var(--muted-foreground)]">
                      {campus.studentCapacity ? `Capacity ${campus.studentCapacity}` : "Capacity —"} ·{" "}
                      {campus.adminUserCount} users
                    </div>
                  </td>
                  <td className="px-4 py-3 text-sm">
                    <div>{[campus.city, campus.state, campus.country].filter(Boolean).join(", ") || "—"}</div>
                    <div className="text-xs text-[var(--muted-foreground)]">{campus.address}</div>
                  </td>
                  <td className="px-4 py-3 text-sm">
                    <div>{campus.inChargeName || "—"}</div>
                    <div className="text-xs text-[var(--muted-foreground)]">
                      {campus.inChargeDesignation || campus.inChargePhone}
                    </div>
                  </td>
                  <td className="px-4 py-3">{campus.isActive ? "Active" : "Inactive"}</td>
                  <td className="px-4 py-3 text-right">
                    <button type="button" className="mr-3 text-[var(--primary)]" onClick={() => openEdit(campus)}>
                      Edit
                    </button>
                    <button type="button" className="text-red-600" onClick={() => remove(campus.id)}>
                      Delete
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
        title={editingId ? "Edit campus" : "Add campus"}
        description="Basic identity, location, and who runs this campus."
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
          <div className="text-xs font-semibold uppercase tracking-wide text-[var(--muted-foreground)]">Identity</div>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Code">
              <TextInput value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} required />
            </Field>
            <Field label="Name">
              <TextInput value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required />
            </Field>
          </div>

          <div className="text-xs font-semibold uppercase tracking-wide text-[var(--muted-foreground)]">Location</div>
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

          <div className="text-xs font-semibold uppercase tracking-wide text-[var(--muted-foreground)]">Contact</div>
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

          <div className="text-xs font-semibold uppercase tracking-wide text-[var(--muted-foreground)]">In charge</div>
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

          <div className="text-xs font-semibold uppercase tracking-wide text-[var(--muted-foreground)]">Advanced</div>
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
            <TextInput value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} />
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
