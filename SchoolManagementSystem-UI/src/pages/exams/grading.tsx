import { useCallback, useEffect, useState } from "react";
import { Plus, RotateCcw, Save, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { OwnerPill } from "@/components/owner-pill";
import { Btn, Field, IconButton, PageHeader, Panel, Select, Skeleton, StatusPill, Switch, TextInput } from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { cn } from "@/lib/utils";
import type { GradeBand, GradeScale, SchoolClass, Scheme, Schemes } from "@/lib/types";
import { errorText } from "@/pages/hiring/shared";
import { useSessions } from "./shared";

export function GradingPage() {
  const { user, can } = useAuth();
  const canEdit = can("grading", "Edit");
  // Main Campus (all campuses) edits the shared defaults; a campus edits its own copy.
  const mainCampus = !!user?.isHq && user.activeCampusId == null;

  return (
    <div>
      <PageHeader
        title="Grading"
        description={
          mainCampus
            ? "Main Campus defaults used by every campus that has not set its own."
            : "This campus's grade scale and result weightage. Without its own, the campus uses the Main Campus defaults."
        }
      />
      <div className="grid gap-4 xl:grid-cols-2">
        <ScalePanel canEdit={canEdit} mainCampus={mainCampus} />
        <SchemePanel canEdit={canEdit} mainCampus={mainCampus} />
      </div>
    </div>
  );
}

function ScalePanel({ canEdit, mainCampus }: { canEdit: boolean; mainCampus: boolean }) {
  const [scale, setScale] = useState<GradeScale | null>(null);
  const [name, setName] = useState("");
  const [bands, setBands] = useState<GradeBand[]>([]);
  const [busy, setBusy] = useState(false);

  const apply = (s: GradeScale) => {
    setScale(s);
    setName(s.name);
    setBands([...s.bands].sort((a, b) => b.minPercent - a.minPercent));
  };

  const load = useCallback(async () => {
    try {
      apply((await unwrap(api.get("/grading/scale"))) as GradeScale);
    } catch (err) {
      toast.error(errorText(err, "Could not load the grade scale"));
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const own = scale?.campusId != null;
  const editable = canEdit;
  const setBand = (i: number, patch: Partial<GradeBand>) => setBands((list) => list.map((b, j) => (j === i ? { ...b, ...patch } : b)));

  const save = async () => {
    setBusy(true);
    try {
      apply((await unwrap(api.put("/grading/scale", { name: name.trim(), bands, mainCampus }))) as GradeScale);
      toast.success("Grade scale saved");
    } catch (err) {
      toast.error(errorText(err, "Could not save the scale"));
    } finally {
      setBusy(false);
    }
  };

  const reset = async () => {
    setBusy(true);
    try {
      await unwrap(api.delete("/grading/scale"));
      await load();
      toast.success("Using the Main Campus scale again");
    } catch (err) {
      toast.error(errorText(err, "Could not reset"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Panel
      flush
      title="Grade scale"
      description="A percentage gets the band with the highest minimum it reaches. Mark exactly one band as the fail band."
      action={scale ? <OwnerPill campusCode={own ? "This campus" : null} /> : null}
    >
      {!scale ? (
        <Skeleton className="m-5 h-48 rounded-lg" />
      ) : (
        <div className="space-y-3 p-5">
          <Field label="Name">
            <TextInput value={name} maxLength={60} disabled={!editable} onChange={(e) => setName(e.target.value)} />
          </Field>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[360px] text-left text-sm">
              <thead>
                <tr className="text-xs text-[var(--muted-foreground)]">
                  <th className="py-1.5 font-medium">Grade</th>
                  <th className="py-1.5 font-medium">From %</th>
                  <th className="py-1.5 font-medium">Fail</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {bands.map((b, i) => (
                  <tr key={i} className="border-t border-[var(--border)]">
                    <td className="py-1.5 pr-2">
                      <TextInput
                        aria-label={`Grade ${i + 1}`}
                        className="h-8 w-20"
                        maxLength={5}
                        value={b.grade}
                        disabled={!editable}
                        onChange={(e) => setBand(i, { grade: e.target.value })}
                      />
                    </td>
                    <td className="py-1.5 pr-2">
                      <TextInput
                        aria-label={`Minimum percent for ${b.grade || `grade ${i + 1}`}`}
                        type="number"
                        min={0}
                        max={100}
                        className="h-8 w-24"
                        value={b.minPercent}
                        disabled={!editable}
                        onChange={(e) => setBand(i, { minPercent: Number(e.target.value) })}
                      />
                    </td>
                    <td className="py-1.5 pr-2">
                      <input
                        type="radio"
                        name="fail-band"
                        aria-label={`${b.grade || `Grade ${i + 1}`} is the fail band`}
                        className="size-4 accent-[var(--danger)]"
                        checked={b.isFail}
                        disabled={!editable}
                        onChange={() => setBands((list) => list.map((x, j) => ({ ...x, isFail: j === i })))}
                      />
                    </td>
                    <td className="py-1.5 text-right">
                      {editable && bands.length > 2 ? (
                        <IconButton label={`Remove ${b.grade || "band"}`} tone="danger" onClick={() => setBands((list) => list.filter((_, j) => j !== i))}>
                          <Trash2 />
                        </IconButton>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {editable ? (
            <div className="flex flex-wrap justify-between gap-2 pt-1">
              <Btn variant="ghost" size="sm" onClick={() => setBands((list) => [...list, { grade: "", minPercent: 0, isFail: false }])}>
                <Plus /> Add band
              </Btn>
              <div className="flex gap-2">
                {own && !mainCampus ? (
                  <Btn variant="outline" size="sm" disabled={busy} onClick={() => void reset()}>
                    <RotateCcw /> Use Main Campus scale
                  </Btn>
                ) : null}
                <Btn size="sm" disabled={busy || !name.trim()} onClick={() => void save()}>
                  <Save /> Save scale
                </Btn>
              </div>
            </div>
          ) : null}
        </div>
      )}
    </Panel>
  );
}

const WEIGHTS: { key: keyof Pick<Scheme, "monthlyWeight" | "midTermWeight" | "finalWeight" | "otherWeight">; label: string }[] = [
  { key: "monthlyWeight", label: "Monthly tests (average)" },
  { key: "midTermWeight", label: "Mid-term" },
  { key: "finalWeight", label: "Final" },
  { key: "otherWeight", label: "Other" },
];

function SchemePanel({ canEdit, mainCampus }: { canEdit: boolean; mainCampus: boolean }) {
  const { can } = useAuth();
  const sessions = useSessions();
  const [classes, setClasses] = useState<SchoolClass[]>([]);
  const [sessionId, setSessionId] = useState(0);
  const [classId, setClassId] = useState(0);
  const [data, setData] = useState<Schemes | null>(null);
  const [form, setForm] = useState<Scheme | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    unwrap(api.get("/classes"))
      .then((c) => setClasses((c as SchoolClass[]).filter((x) => x.isActive).sort((a, b) => a.displayOrder - b.displayOrder)))
      .catch(() => setClasses([]));
  }, []);

  const load = useCallback(async () => {
    const q = new URLSearchParams();
    if (sessionId) q.set("sessionId", String(sessionId));
    if (classId) q.set("classId", String(classId));
    try {
      const d = (await unwrap(api.get(`/grading/schemes?${q}`))) as Schemes;
      setData(d);
      setForm(d.effective);
    } catch (err) {
      toast.error(errorText(err, "Could not load the weightage"));
    }
  }, [sessionId, classId]);

  useEffect(() => {
    void load();
  }, [load]);

  const sum = form ? WEIGHTS.reduce((s, w) => s + (Number(form[w.key]) || 0), 0) : 0;
  const ownedHere = data?.owned.find((o) => (o.sessionId ?? 0) === sessionId && (o.classId ?? 0) === classId);

  const save = async () => {
    if (!form) return;
    setBusy(true);
    try {
      await unwrap(
        api.put("/grading/schemes", {
          sessionId: sessionId || null,
          classId: classId || null,
          monthlyWeight: Number(form.monthlyWeight),
          midTermWeight: Number(form.midTermWeight),
          finalWeight: Number(form.finalWeight),
          otherWeight: Number(form.otherWeight),
          mustPassFinal: form.mustPassFinal,
          mainCampus,
        }),
      );
      toast.success("Weightage saved");
      await load();
    } catch (err) {
      toast.error(errorText(err, "Could not save the weightage"));
    } finally {
      setBusy(false);
    }
  };

  const remove = async (id: number) => {
    setBusy(true);
    try {
      await unwrap(api.delete(`/grading/schemes/${id}`));
      toast.success("Override removed");
      await load();
    } catch (err) {
      toast.error(errorText(err, "Could not remove"));
    } finally {
      setBusy(false);
    }
  };

  const className = (id?: number | null) => (id ? classes.find((c) => c.id === id)?.name ?? `Class ${id}` : "All classes");
  const sessionName = (id?: number | null) => (id ? sessions?.items.find((s) => s.id === id)?.name ?? `Session ${id}` : "Any session");

  return (
    <Panel title="Annual result weightage" description="How much each exam type counts in the annual result. Types without a published exam drop out and the rest scale up to 100.">
      <div className="mb-4 grid gap-3 sm:grid-cols-2">
        <Field label="Session">
          <Select value={sessionId} onChange={(e) => setSessionId(Number(e.target.value))}>
            <option value={0}>Any session</option>
            {sessions?.items.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Class">
          <Select value={classId} onChange={(e) => setClassId(Number(e.target.value))}>
            <option value={0}>All classes</option>
            {classes.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
        </Field>
      </div>

      {!form ? (
        <Skeleton className="h-40 rounded-lg" />
      ) : (
        <div className="space-y-3">
          <div className="grid gap-3 sm:grid-cols-2">
            {WEIGHTS.map((w) => (
              <Field key={w.key} label={`${w.label} %`}>
                <TextInput
                  type="number"
                  min={0}
                  max={100}
                  value={form[w.key]}
                  disabled={!canEdit}
                  onChange={(e) => setForm({ ...form, [w.key]: Number(e.target.value) })}
                />
              </Field>
            ))}
          </div>
          <div className="flex items-center justify-between text-sm">
            <span className="text-[var(--muted-foreground)]">Total</span>
            <span className={cn("tabular font-semibold", sum === 100 ? "text-[var(--success)]" : "text-[var(--danger)]")}>{sum} / 100</span>
          </div>
          <Switch
            checked={form.mustPassFinal}
            disabled={!canEdit}
            onChange={(v) => setForm({ ...form, mustPassFinal: v })}
            label="Must pass the final"
            description="A student who fails the final exam fails the year, whatever the annual percentage."
          />
          {canEdit ? (
            <div className="flex justify-end">
              <Btn size="sm" disabled={busy || sum !== 100} onClick={() => void save()}>
                <Save /> {ownedHere ? "Update" : "Save"} for {sessionName(sessionId || null)} · {className(classId || null)}
              </Btn>
            </div>
          ) : null}
        </div>
      )}

      {data?.owned.length ? (
        <div className="mt-5 border-t border-[var(--border)] pt-4">
          <div className="mb-2 text-xs font-medium text-[var(--muted-foreground)]">{mainCampus ? "Main Campus settings" : "This campus's settings"}</div>
          <ul className="space-y-1.5">
            {data.owned.map((o) => (
              <li key={o.id} className="flex flex-wrap items-center gap-2 rounded-lg border border-[var(--border)] px-3 py-2 text-sm">
                <span className="font-medium">
                  {sessionName(o.sessionId)} · {className(o.classId)}
                </span>
                <span className="tabular text-xs text-[var(--muted-foreground)]">
                  {o.monthlyWeight}/{o.midTermWeight}/{o.finalWeight}/{o.otherWeight}
                </span>
                {o.mustPassFinal ? <StatusPill tone="warning">Must pass final</StatusPill> : null}
                <span className="flex-1" />
                {can("grading", "Delete") && o.id ? (
                  <IconButton label="Remove this setting" tone="danger" disabled={busy} onClick={() => void remove(o.id!)}>
                    <Trash2 />
                  </IconButton>
                ) : null}
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </Panel>
  );
}
