import { useEffect, useMemo, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { ArrowRight, Building2, GraduationCap, Lock, Users } from "lucide-react";
import { toast } from "sonner";
import {
  Avatar,
  Btn,
  EmptyState,
  Field,
  FormSection,
  PageHeader,
  Panel,
  Skeleton,
  StatusPill,
  TextArea,
  TextInput,
} from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import type { AcademicSessionList, Campus, SaveCampusRequest, SchoolClass, Section, UserListItem } from "@/lib/types";

type Profile = Omit<SaveCampusRequest, "admin">;

function toProfile(c: Campus): Profile {
  return {
    code: c.code,
    name: c.name,
    address: c.address ?? "",
    city: c.city ?? "",
    state: c.state ?? "",
    country: c.country ?? "",
    postalCode: c.postalCode ?? "",
    phone: c.phone ?? "",
    email: c.email ?? "",
    website: c.website ?? "",
    inChargeName: c.inChargeName ?? "",
    inChargePhone: c.inChargePhone ?? "",
    inChargeEmail: c.inChargeEmail ?? "",
    inChargeDesignation: c.inChargeDesignation ?? "",
    establishedOn: c.establishedOn ?? null,
    studentCapacity: c.studentCapacity ?? null,
    notes: c.notes ?? "",
    isActive: c.isActive,
  };
}

/** The Campus Admin's home for one campus: its profile, its team and its academic setup at a glance. */
export function MyCampusPage() {
  const { user, can } = useAuth();
  const navigate = useNavigate();
  const campusId = user?.activeCampusId ?? null;
  const [campus, setCampus] = useState<Campus | null>(null);
  const [form, setForm] = useState<Profile | null>(null);
  const [users, setUsers] = useState<UserListItem[] | null>(null);
  const [sessions, setSessions] = useState<AcademicSessionList | null>(null);
  const [classes, setClasses] = useState<SchoolClass[] | null>(null);
  const [sections, setSections] = useState<Section[] | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!campusId) return;
    (async () => {
      try {
        const c = (await unwrap(api.get(`/campuses/${campusId}`))) as Campus;
        setCampus(c);
        setForm(toProfile(c));
      } catch (err) {
        toast.error(err instanceof Error ? err.message : "Failed to load campus");
      }
      const load = async <T,>(allowed: boolean, url: string, set: (v: T) => void) => {
        if (!allowed) return;
        try {
          set((await unwrap(api.get(url))) as T);
        } catch {
          /* summary card simply stays empty */
        }
      };
      await Promise.all([
        load<UserListItem[]>(can("users", "View"), "/users", setUsers),
        load<AcademicSessionList>(can("academic-sessions", "View"), "/academic-sessions", setSessions),
        load<SchoolClass[]>(can("classes", "View"), "/classes", setClasses),
        load<Section[]>(can("sections", "View"), "/sections", setSections),
      ]);
    })();
  }, [campusId, can]);

  const roleCounts = useMemo(() => {
    const counts = new Map<string, number>();
    for (const u of users ?? []) for (const r of u.roles) counts.set(r.name, (counts.get(r.name) ?? 0) + 1);
    return [...counts.entries()].sort((a, b) => b[1] - a[1]);
  }, [users]);

  if (!campusId) {
    return (
      <div className="panel">
        <EmptyState icon={Building2} title="Pick a campus" description="Choose a campus in the top bar to see and manage it here." />
      </div>
    );
  }

  const canEdit = can("campuses", "Edit");
  const current = sessions?.items.find((s) => s.id === sessions.effectiveCurrentId);

  async function onSave(e: FormEvent) {
    e.preventDefault();
    if (!form || !campus) return;
    setSaving(true);
    try {
      const saved = (await unwrap(
        api.put(`/campuses/${campus.id}`, {
          ...form,
          establishedOn: form.establishedOn || null,
          studentCapacity: form.studentCapacity == null ? null : Number(form.studentCapacity),
        }),
      )) as Campus;
      setCampus(saved);
      setForm(toProfile(saved));
      toast.success("Campus profile saved");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  const set = (patch: Partial<Profile>) => setForm((f) => (f ? { ...f, ...patch } : f));

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow={
          campus ? (
            <span className="inline-flex items-center gap-2">
              <span className="rounded-md border border-[var(--border)] px-1.5 py-0.5 font-mono text-[11px]">{campus.code}</span>
              {campus.isActive ? "Active campus" : "Inactive campus"}
            </span>
          ) : undefined
        }
        title={campus?.name ?? "My campus"}
        description="Your campus profile, team and academic setup in one place."
      />

      <div className="grid gap-6 lg:grid-cols-3">
        <Panel title="Campus profile" description={canEdit ? "Code and status are managed by head office." : "Read only"} className="lg:col-span-2">
          {!form ? (
            <div className="space-y-3">
              <Skeleton className="h-9 w-full" />
              <Skeleton className="h-9 w-full" />
              <Skeleton className="h-9 w-2/3" />
            </div>
          ) : (
            <form className="space-y-6" onSubmit={onSave}>
              <FormSection title="Identity">
                <div className="grid gap-4 sm:grid-cols-[160px_1fr]">
                  <Field label="Code">
                    <div className="relative">
                      <TextInput value={form.code} disabled className="pr-8 font-mono" />
                      <Lock className="pointer-events-none absolute top-1/2 right-2.5 size-3.5 -translate-y-1/2 text-[var(--muted-foreground)]" />
                    </div>
                  </Field>
                  <Field label="Name" required>
                    <TextInput value={form.name} onChange={(e) => set({ name: e.target.value })} required disabled={!canEdit} />
                  </Field>
                </div>
              </FormSection>
              <FormSection title="Location & contact">
                <Field label="Address">
                  <TextInput value={form.address} onChange={(e) => set({ address: e.target.value })} disabled={!canEdit} />
                </Field>
                <div className="grid gap-4 sm:grid-cols-2">
                  <Field label="City">
                    <TextInput value={form.city} onChange={(e) => set({ city: e.target.value })} disabled={!canEdit} />
                  </Field>
                  <Field label="Country">
                    <TextInput value={form.country} onChange={(e) => set({ country: e.target.value })} disabled={!canEdit} />
                  </Field>
                  <Field label="Phone">
                    <TextInput value={form.phone} onChange={(e) => set({ phone: e.target.value })} disabled={!canEdit} />
                  </Field>
                  <Field label="Email">
                    <TextInput type="email" value={form.email} onChange={(e) => set({ email: e.target.value })} disabled={!canEdit} />
                  </Field>
                </div>
              </FormSection>
              <FormSection title="In charge">
                <div className="grid gap-4 sm:grid-cols-2">
                  <Field label="Name">
                    <TextInput value={form.inChargeName} onChange={(e) => set({ inChargeName: e.target.value })} disabled={!canEdit} />
                  </Field>
                  <Field label="Designation">
                    <TextInput
                      value={form.inChargeDesignation}
                      onChange={(e) => set({ inChargeDesignation: e.target.value })}
                      disabled={!canEdit}
                    />
                  </Field>
                  <Field label="Phone">
                    <TextInput value={form.inChargePhone} onChange={(e) => set({ inChargePhone: e.target.value })} disabled={!canEdit} />
                  </Field>
                  <Field label="Student capacity">
                    <TextInput
                      type="number"
                      min={0}
                      value={form.studentCapacity ?? ""}
                      onChange={(e) => set({ studentCapacity: e.target.value === "" ? null : Number(e.target.value) })}
                      disabled={!canEdit}
                    />
                  </Field>
                </div>
                <Field label="Notes">
                  <TextArea rows={2} value={form.notes} onChange={(e) => set({ notes: e.target.value })} disabled={!canEdit} />
                </Field>
              </FormSection>
              {canEdit ? (
                <div className="flex justify-end">
                  <Btn type="submit" disabled={saving}>
                    {saving ? "Saving…" : "Save profile"}
                  </Btn>
                </div>
              ) : null}
            </form>
          )}
        </Panel>

        <div className="space-y-6">
          <Panel title="Team" description="People who work in this campus">
            {users == null ? (
              <p className="text-sm text-[var(--muted-foreground)]">{can("users", "View") ? "Loading…" : "You cannot view users."}</p>
            ) : (
              <div className="space-y-4">
                <div className="flex items-center gap-3">
                  <span className="inline-flex size-10 items-center justify-center rounded-lg bg-[var(--info-soft)] text-[var(--info)]">
                    <Users className="size-5" />
                  </span>
                  <div>
                    <div className="tabular text-2xl font-semibold">{users.length}</div>
                    <div className="text-xs text-[var(--muted-foreground)]">{users.filter((u) => u.isActive).length} active</div>
                  </div>
                </div>
                <ul className="space-y-1.5 text-sm">
                  {roleCounts.map(([role, n]) => (
                    <li key={role} className="flex items-center justify-between">
                      <span className="flex items-center gap-2">
                        <Avatar name={role} size="sm" />
                        {role}
                      </span>
                      <span className="tabular text-[var(--muted-foreground)]">{n}</span>
                    </li>
                  ))}
                </ul>
                <Btn type="button" variant="outline" className="w-full" onClick={() => navigate("/admin/users")}>
                  Manage users <ArrowRight />
                </Btn>
              </div>
            )}
          </Panel>

          <Panel title="Academics" description="This campus's setup">
            <dl className="space-y-3 text-sm">
              <div className="flex items-center justify-between gap-3">
                <dt className="text-[var(--muted-foreground)]">Current session</dt>
                <dd className="flex items-center gap-2 font-medium">
                  {current ? (
                    <>
                      {current.name}
                      <StatusPill tone={current.campusCode ? "primary" : "info"}>{current.campusCode ?? "HQ"}</StatusPill>
                    </>
                  ) : (
                    "—"
                  )}
                </dd>
              </div>
              <div className="flex items-center justify-between">
                <dt className="text-[var(--muted-foreground)]">Classes</dt>
                <dd className="tabular font-medium">{classes?.filter((c) => c.isActive).length ?? "—"}</dd>
              </div>
              <div className="flex items-center justify-between">
                <dt className="text-[var(--muted-foreground)]">Sections this session</dt>
                <dd className="tabular font-medium">{sections?.length ?? "—"}</dd>
              </div>
            </dl>
            {can("sections", "View") ? (
              <Btn type="button" variant="outline" className="mt-4 w-full" onClick={() => navigate("/admin/academics/sections")}>
                <GraduationCap /> Open sections
              </Btn>
            ) : null}
          </Panel>
        </div>
      </div>
    </div>
  );
}
