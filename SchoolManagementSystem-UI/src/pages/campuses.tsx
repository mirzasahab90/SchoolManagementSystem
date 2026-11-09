import { useEffect, useMemo, useState, type FormEvent, type ReactNode } from "react";
import { Building2, Mail, MapPin, Pencil, Phone, Plus, Trash2, UserRound, Users } from "lucide-react";
import { toast } from "sonner";
import { ConfirmDialog, Modal } from "@/components/modal";
import {
  Btn,
  EmptyState,
  Field,
  FormSection,
  IconButton,
  PageHeader,
  SearchField,
  SectionLabel,
  Skeleton,
  StatusPill,
  Switch,
  TextArea,
  TextInput,
} from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import type { Campus, SaveCampusRequest } from "@/lib/types";
import { cn } from "@/lib/utils";

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
  const { can, user: me } = useAuth();
  const isHq = !!me?.isHq;
  const [items, setItems] = useState<Campus[]>([]);
  const [form, setForm] = useState<SaveCampusRequest>(emptyForm);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [open, setOpen] = useState(false);
  const [detail, setDetail] = useState<Campus | null>(null);
  const [query, setQuery] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<Campus | null>(null);
  const [deleting, setDeleting] = useState(false);

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
      [c.code, c.name, c.city, c.state, c.country, c.inChargeName].filter(Boolean).join(" ").toLowerCase().includes(q),
    );
  }, [items, query]);

  const canAdd = isHq && can("campuses", "Add");
  const canDelete = isHq && can("campuses", "Delete");

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
          form.studentCapacity === null || Number.isNaN(Number(form.studentCapacity)) ? null : Number(form.studentCapacity),
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

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await unwrap(api.delete(`/campuses/${pendingDelete.id}`));
      toast.success("Campus deleted");
      setPendingDelete(null);
      setDetail(null);
      await load();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Delete failed");
    } finally {
      setDeleting(false);
    }
  }

  const activeCount = items.filter((c) => c.isActive).length;
  const totalCapacity = items.reduce((sum, c) => sum + (c.studentCapacity ?? 0), 0);

  return (
    <div>
      <PageHeader
        title="Campuses"
        description={
          isHq
            ? "Every branch of your school — where it is, who runs it and how many students it can take."
            : "The campuses you work in."
        }
        actions={
          canAdd ? (
            <Btn type="button" onClick={openCreate}>
              <Plus />
              Add campus
            </Btn>
          ) : null
        }
      />

      <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
        <SearchField value={query} onChange={setQuery} placeholder="Search name, code, city or in-charge" />
        <div className="flex items-center gap-4 text-xs text-[var(--muted-foreground)]">
          <span>
            <span className="tabular font-medium text-[var(--foreground)]">{items.length}</span> campuses
          </span>
          <span>
            <span className="tabular font-medium text-[var(--foreground)]">{activeCount}</span> active
          </span>
          {totalCapacity ? (
            <span>
              <span className="tabular font-medium text-[var(--foreground)]">{totalCapacity.toLocaleString()}</span> seats
            </span>
          ) : null}
        </div>
      </div>

      {loading ? (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {[1, 2, 3].map((i) => (
            <div key={i} className="panel space-y-4 p-5">
              <div className="flex gap-3">
                <Skeleton className="size-10 rounded-lg" />
                <div className="flex-1 space-y-2">
                  <Skeleton className="h-3.5 w-2/3" />
                  <Skeleton className="h-3 w-1/3" />
                </div>
              </div>
              <Skeleton className="h-3 w-full" />
              <Skeleton className="h-3 w-4/5" />
            </div>
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <div className="panel">
          <EmptyState
            icon={Building2}
            title={items.length === 0 ? "No campuses yet" : "No matching campuses"}
            description={items.length === 0 ? "Add your first campus to start running multiple branches." : "Try a different search."}
            action={
              items.length === 0 && canAdd ? (
                <Btn type="button" onClick={openCreate}>
                  <Plus />
                  Add campus
                </Btn>
              ) : null
            }
          />
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {filtered.map((campus) => (
            <CampusCard
              key={campus.id}
              campus={campus}
              isWorking={campus.id === me?.activeCampusId}
              onOpen={() => setDetail(campus)}
              onEdit={can("campuses", "Edit") ? () => openEdit(campus) : undefined}
              onDelete={canDelete ? () => setPendingDelete(campus) : undefined}
            />
          ))}
        </div>
      )}

      <Modal
        open={!!detail}
        wide
        title={detail?.name ?? "Campus"}
        description={detail ? `Campus code ${detail.code}` : undefined}
        onClose={() => setDetail(null)}
        footer={
          <>
            <Btn type="button" variant="outline" onClick={() => setDetail(null)}>
              Close
            </Btn>
            {detail && can("campuses", "Edit") ? (
              <Btn type="button" onClick={() => openEdit(detail)}>
                <Pencil />
                Edit campus
              </Btn>
            ) : null}
          </>
        }
      >
        {detail ? (
          <div className="space-y-6 text-sm">
            <div className="flex flex-wrap gap-2">
              <StatusPill tone={detail.isActive ? "success" : "neutral"} dot>
                {detail.isActive ? "Active" : "Inactive"}
              </StatusPill>
              {detail.studentCapacity ? <StatusPill tone="info">{detail.studentCapacity} seats</StatusPill> : null}
              <StatusPill>{detail.adminUserCount} linked users</StatusPill>
            </div>
            <DetailBlock title="Location">
              <Row label="Address" value={detail.address} wide />
              <Row label="City" value={detail.city} />
              <Row label="State / Province" value={detail.state} />
              <Row label="Country" value={detail.country} />
              <Row label="Postal code" value={detail.postalCode} />
            </DetailBlock>
            <DetailBlock title="Contact">
              <Row label="Phone" value={detail.phone} />
              <Row label="Email" value={detail.email} />
              <Row label="Website" value={detail.website} wide />
            </DetailBlock>
            <DetailBlock title="In charge">
              <Row label="Name" value={detail.inChargeName} />
              <Row label="Designation" value={detail.inChargeDesignation} />
              <Row label="Phone" value={detail.inChargePhone} />
              <Row label="Email" value={detail.inChargeEmail} />
            </DetailBlock>
            <DetailBlock title="Other">
              <Row label="Established" value={detail.establishedOn} />
              <Row label="Notes" value={detail.notes} wide />
            </DetailBlock>
          </div>
        ) : null}
      </Modal>

      <Modal
        open={open}
        wide
        title={editingId ? "Edit campus" : "Add campus"}
        description="Identity, location, contact, in-charge and capacity."
        onClose={() => setOpen(false)}
        footer={
          <>
            <Btn type="button" variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Btn>
            <Btn type="submit" form="campus-form" disabled={saving}>
              {saving ? "Saving…" : editingId ? "Save changes" : "Create campus"}
            </Btn>
          </>
        }
      >
        <form id="campus-form" className="space-y-6" onSubmit={onSubmit}>
          <FormSection title="Identity">
            <div className="grid gap-4 sm:grid-cols-[160px_1fr]">
              <Field label="Code" required hint={!isHq ? "Only HQ can change the code." : "Short, unique, e.g. MAIN"}>
                <TextInput
                  value={form.code}
                  onChange={(e) => setForm({ ...form, code: e.target.value.toUpperCase() })}
                  required
                  disabled={!isHq}
                  className="font-mono uppercase"
                />
              </Field>
              <Field label="Name" required>
                <TextInput value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required />
              </Field>
            </div>
            <Switch
              checked={form.isActive}
              onChange={(v) => setForm({ ...form, isActive: v })}
              label="Campus active"
              description={isHq ? "Inactive campuses are hidden from campus pickers." : "Only HQ can change this."}
              disabled={!isHq}
            />
          </FormSection>

          <FormSection title="Location">
            <Field label="Address">
              <TextInput value={form.address} onChange={(e) => setForm({ ...form, address: e.target.value })} />
            </Field>
            <div className="grid gap-4 sm:grid-cols-2">
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
          </FormSection>

          <FormSection title="Contact">
            <div className="grid gap-4 sm:grid-cols-2">
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
          </FormSection>

          <FormSection title="In charge" description="Principal or campus head.">
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Name">
                <TextInput value={form.inChargeName} onChange={(e) => setForm({ ...form, inChargeName: e.target.value })} />
              </Field>
              <Field label="Designation">
                <TextInput
                  value={form.inChargeDesignation}
                  onChange={(e) => setForm({ ...form, inChargeDesignation: e.target.value })}
                />
              </Field>
              <Field label="Phone">
                <TextInput value={form.inChargePhone} onChange={(e) => setForm({ ...form, inChargePhone: e.target.value })} />
              </Field>
              <Field label="Email">
                <TextInput
                  type="email"
                  value={form.inChargeEmail}
                  onChange={(e) => setForm({ ...form, inChargeEmail: e.target.value })}
                />
              </Field>
            </div>
          </FormSection>

          <FormSection title="Other">
            <div className="grid gap-4 sm:grid-cols-2">
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
                    setForm({ ...form, studentCapacity: e.target.value === "" ? null : Number(e.target.value) })
                  }
                />
              </Field>
            </div>
            <Field label="Notes">
              <TextArea rows={3} value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} />
            </Field>
          </FormSection>
        </form>
      </Modal>

      <ConfirmDialog
        open={!!pendingDelete}
        title="Delete campus?"
        description={
          pendingDelete
            ? `${pendingDelete.name} (${pendingDelete.code}) will be removed. Users who only belong to this campus must be moved first.`
            : ""
        }
        busy={deleting}
        onConfirm={confirmDelete}
        onClose={() => setPendingDelete(null)}
      />
    </div>
  );
}

function CampusCard({
  campus,
  isWorking,
  onOpen,
  onEdit,
  onDelete,
}: {
  campus: Campus;
  isWorking: boolean;
  onOpen: () => void;
  onEdit?: () => void;
  onDelete?: () => void;
}) {
  const location = [campus.city, campus.country].filter(Boolean).join(", ");
  return (
    <article
      className={cn(
        "panel group flex flex-col transition hover:border-[var(--border-strong)] hover:shadow-[var(--shadow-sm)]",
        !campus.isActive && "opacity-75",
      )}
    >
      <button type="button" onClick={onOpen} className="flex-1 p-5 text-left outline-none focus-visible:ring-4 focus-visible:ring-[var(--ring)]">
        <div className="flex items-start gap-3">
          <span className="inline-flex size-10 shrink-0 items-center justify-center rounded-lg bg-[var(--primary-soft)] text-[var(--primary-soft-foreground)]">
            <Building2 className="size-5" />
          </span>
          <div className="min-w-0 flex-1">
            <div className="truncate font-semibold">{campus.name}</div>
            <div className="mt-0.5 flex items-center gap-2">
              <span className="font-mono text-xs text-[var(--muted-foreground)]">{campus.code}</span>
              {isWorking ? <StatusPill tone="primary">Working here</StatusPill> : null}
            </div>
          </div>
          <StatusPill tone={campus.isActive ? "success" : "neutral"} dot>
            {campus.isActive ? "Active" : "Inactive"}
          </StatusPill>
        </div>

        <dl className="mt-5 space-y-2 text-sm">
          <InfoLine icon={<MapPin />} value={location || campus.address} />
          <InfoLine
            icon={<UserRound />}
            value={campus.inChargeName ? `${campus.inChargeName}${campus.inChargeDesignation ? ` · ${campus.inChargeDesignation}` : ""}` : null}
            empty="No in-charge set"
          />
          <InfoLine icon={campus.phone ? <Phone /> : <Mail />} value={campus.phone || campus.email} empty="No contact details" />
        </dl>
      </button>

      <div className="flex items-center justify-between border-t border-[var(--border)] px-5 py-2.5">
        <div className="flex items-center gap-4 text-xs text-[var(--muted-foreground)]">
          <span className="flex items-center gap-1.5">
            <Users className="size-3.5" />
            <span className="tabular">{campus.adminUserCount}</span> users
          </span>
          {campus.studentCapacity ? (
            <span>
              <span className="tabular">{campus.studentCapacity.toLocaleString()}</span> seats
            </span>
          ) : null}
        </div>
        <div className="flex gap-0.5">
          {onEdit ? (
            <IconButton label={`Edit ${campus.name}`} onClick={onEdit}>
              <Pencil />
            </IconButton>
          ) : null}
          {onDelete ? (
            <IconButton label={`Delete ${campus.name}`} tone="danger" onClick={onDelete}>
              <Trash2 />
            </IconButton>
          ) : null}
        </div>
      </div>
    </article>
  );
}

function InfoLine({ icon, value, empty = "—" }: { icon: ReactNode; value?: string | null; empty?: string }) {
  return (
    <div className="flex items-center gap-2.5 text-[var(--muted-foreground)] [&_svg]:size-4 [&_svg]:shrink-0">
      {icon}
      <span className={cn("truncate", value ? "text-[var(--foreground)]" : "italic")}>{value || empty}</span>
    </div>
  );
}

function DetailBlock({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div>
      <SectionLabel>{title}</SectionLabel>
      <dl className="mt-2.5 grid gap-x-6 gap-y-3 sm:grid-cols-2">{children}</dl>
    </div>
  );
}

function Row({ label, value, wide }: { label: string; value?: string | number | null; wide?: boolean }) {
  return (
    <div className={cn(wide && "sm:col-span-2")}>
      <dt className="text-xs text-[var(--muted-foreground)]">{label}</dt>
      <dd className={cn("mt-0.5 break-words", value ? "font-medium" : "text-[var(--muted-foreground)]")}>{value || "—"}</dd>
    </div>
  );
}
