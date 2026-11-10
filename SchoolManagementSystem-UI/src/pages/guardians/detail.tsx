import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, HeartHandshake } from "lucide-react";
import { toast } from "sonner";
import { Avatar, Btn, EmptyState, Field, PageHeader, Panel, Skeleton, TextInput } from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import type { GuardianDetail, SaveGuardianRequest } from "@/lib/types";

export function GuardianDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { can } = useAuth();
  const canEdit = can("guardians", "Edit");
  const [guardian, setGuardian] = useState<GuardianDetail | null>(null);
  const [form, setForm] = useState<SaveGuardianRequest | null>(null);
  const [missing, setMissing] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const g = (await unwrap(api.get(`/guardians/${id}`))) as GuardianDetail;
        setGuardian(g);
        setForm({
          fullName: g.fullName,
          cnic: g.cnic ?? "",
          phone: g.phone,
          altPhone: g.altPhone ?? "",
          email: g.email ?? "",
          occupation: g.occupation ?? "",
          address: g.address ?? "",
        });
      } catch {
        setMissing(true);
      }
    })();
  }, [id]);

  if (missing) {
    return (
      <div className="panel">
        <EmptyState icon={HeartHandshake} title="Guardian not found" description="They may only have children on another campus." />
      </div>
    );
  }

  if (!guardian || !form) {
    return <Skeleton className="h-64 w-full rounded-xl" />;
  }

  const set = (p: Partial<SaveGuardianRequest>) => setForm((f) => (f ? { ...f, ...p } : f));

  async function save() {
    if (!form) return;
    setSaving(true);
    try {
      const clean = (v?: string | null) => (v && v.trim() ? v.trim() : null);
      const g = (await unwrap(
        api.put(`/guardians/${id}`, {
          ...form,
          cnic: clean(form.cnic),
          altPhone: clean(form.altPhone),
          email: clean(form.email),
          occupation: clean(form.occupation),
          address: clean(form.address),
        }),
      )) as GuardianDetail;
      setGuardian(g);
      toast.success("Guardian updated");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="space-y-5">
      <PageHeader
        eyebrow={
          <button type="button" onClick={() => navigate("/admin/guardians")} className="inline-flex items-center gap-1 hover:text-[var(--foreground)]">
            <ArrowLeft className="size-3.5" /> Guardians
          </button>
        }
        title={guardian.fullName}
        description={`${guardian.children.length} ${guardian.children.length === 1 ? "child" : "children"} in the school`}
      />
      <div className="grid gap-5 lg:grid-cols-3">
        <Panel title="Details" className="lg:col-span-2">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Full name" required>
              <TextInput value={form.fullName} onChange={(e) => set({ fullName: e.target.value })} disabled={!canEdit} />
            </Field>
            <Field label="CNIC">
              <TextInput value={form.cnic ?? ""} onChange={(e) => set({ cnic: e.target.value })} disabled={!canEdit} />
            </Field>
            <Field label="Phone" required>
              <TextInput value={form.phone} onChange={(e) => set({ phone: e.target.value })} disabled={!canEdit} />
            </Field>
            <Field label="Alternate phone">
              <TextInput value={form.altPhone ?? ""} onChange={(e) => set({ altPhone: e.target.value })} disabled={!canEdit} />
            </Field>
            <Field label="Email">
              <TextInput type="email" value={form.email ?? ""} onChange={(e) => set({ email: e.target.value })} disabled={!canEdit} />
            </Field>
            <Field label="Occupation">
              <TextInput value={form.occupation ?? ""} onChange={(e) => set({ occupation: e.target.value })} disabled={!canEdit} />
            </Field>
            <Field label="Address" className="sm:col-span-2">
              <TextInput value={form.address ?? ""} onChange={(e) => set({ address: e.target.value })} disabled={!canEdit} />
            </Field>
          </div>
          {canEdit ? (
            <div className="mt-5 flex justify-end">
              <Btn type="button" onClick={save} disabled={saving}>
                {saving ? "Saving…" : "Save changes"}
              </Btn>
            </div>
          ) : null}
        </Panel>
        <Panel title="Children" flush>
          <ul className="divide-y divide-[var(--border)] text-sm">
            {guardian.children.map((c, i) => (
              <li key={`${c.studentId ?? "x"}-${i}`} className="flex items-center gap-3 px-5 py-3">
                <Avatar name={c.fullName} size="sm" />
                <div className="min-w-0 flex-1 leading-tight">
                  {c.studentId ? (
                    <Link to={`/admin/students/${c.studentId}`} className="font-medium hover:text-[var(--primary)]">
                      {c.fullName}
                    </Link>
                  ) : (
                    <span className="font-medium">{c.fullName}</span>
                  )}
                  <div className="text-xs text-[var(--muted-foreground)]">
                    {c.className ? `${c.className} – ${c.sectionName}` : "No current class"}
                  </div>
                </div>
                <span className="rounded border border-[var(--border)] px-1 font-mono text-[11px] text-[var(--muted-foreground)]">{c.campusCode}</span>
              </li>
            ))}
          </ul>
        </Panel>
      </div>
    </div>
  );
}
