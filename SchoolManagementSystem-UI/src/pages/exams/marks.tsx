import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Lock, PencilLine, Save, Send, Undo2 } from "lucide-react";
import { toast } from "sonner";
import { ConfirmDialog } from "@/components/modal";
import { Btn, EmptyState, Field, PageHeader, Panel, Select, Skeleton, StatusPill, TextInput } from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { cn } from "@/lib/utils";
import type { MarkRow, MarkSheet, MarkStatus, MyPaper } from "@/lib/types";
import { errorText } from "@/pages/hiring/shared";
import { gradeFor, num, paperStatusLabel, paperStatusTone, round2 } from "./shared";

const key = (p: { paperId: number; sectionId: number }) => `${p.paperId}:${p.sectionId}`;

const statusStyle: Record<MarkStatus, string> = {
  Present: "bg-[var(--success)] text-white border-[var(--success)]",
  Absent: "bg-[var(--danger)] text-white border-[var(--danger)]",
  Exempted: "bg-[var(--info)] text-white border-[var(--info)]",
};
const statusCode: Record<MarkStatus, string> = { Present: "P", Absent: "A", Exempted: "Ex" };

export function MarksEntryPage() {
  const { user: me } = useAuth();
  const [params, setParams] = useSearchParams();
  const [papers, setPapers] = useState<MyPaper[] | null>(null);
  const [current, setCurrent] = useState(params.get("paperId") ? `${params.get("paperId")}:${params.get("sectionId")}` : "");
  const [sheet, setSheet] = useState<MarkSheet | null>(null);
  const [rows, setRows] = useState<MarkRow[]>([]);
  const [busy, setBusy] = useState(false);
  const [confirmSubmit, setConfirmSubmit] = useState(false);
  const onAllCampuses = me?.activeCampusId == null;

  useEffect(() => {
    if (onAllCampuses) return;
    unwrap(api.get("/marks/my-papers"))
      .then((p) => {
        const list = p as MyPaper[];
        setPapers(list);
        setCurrent((cur) => (cur && list.some((x) => key(x) === cur) ? cur : list[0] ? key(list[0]) : ""));
      })
      .catch((err) => {
        toast.error(errorText(err, "Could not load your papers"));
        setPapers([]);
      });
  }, [onAllCampuses]);

  const load = useCallback(async () => {
    if (!current) return;
    const [paperId, sectionId] = current.split(":");
    setSheet(null);
    try {
      const s = (await unwrap(api.get(`/marks/${paperId}/sections/${sectionId}`))) as MarkSheet;
      setSheet(s);
      setRows(s.rows);
    } catch (err) {
      toast.error(errorText(err, "Could not load the marks"));
    }
  }, [current]);

  useEffect(() => {
    void load();
    if (current) {
      const [paperId, sectionId] = current.split(":");
      setParams({ paperId, sectionId }, { replace: true });
    }
  }, [load, current, setParams]);

  const groups = useMemo(() => {
    const m = new Map<string, MyPaper[]>();
    papers?.forEach((p) => m.set(p.examName, [...(m.get(p.examName) ?? []), p]));
    return [...m.entries()];
  }, [papers]);

  const editable = !!sheet?.canEdit;
  const paper = sheet?.paper;
  const theoryMax = paper ? (paper.hasPractical ? paper.totalMarks - (paper.practicalMarks ?? 0) : paper.totalMarks) : 0;
  const setRow = (id: number, patch: Partial<MarkRow>) => setRows((list) => list.map((r) => (r.studentId === id ? { ...r, ...patch } : r)));
  const entered = rows.filter((r) => r.status != null || r.theory != null || r.practical != null);

  const apply = (s: MarkSheet, message: string) => {
    setSheet(s);
    setRows(s.rows);
    setPapers((list) => list?.map((p) => (key(p) === key({ paperId: s.paper.id, sectionId: s.sectionId }) ? { ...p, status: s.status } : p)) ?? list);
    toast.success(message);
  };

  const save = async () => {
    if (!sheet) return;
    setBusy(true);
    try {
      const body = {
        rows: entered.map((r) => {
          const status = r.status ?? "Present";
          return {
            studentId: r.studentId,
            status,
            theory: status === "Present" ? r.theory ?? null : null,
            practical: status === "Present" && sheet.paper.hasPractical ? r.practical ?? null : null,
          };
        }),
      };
      apply((await unwrap(api.put(`/marks/${sheet.paper.id}/sections/${sheet.sectionId}`, body))) as MarkSheet, "Marks saved");
    } catch (err) {
      toast.error(errorText(err, "Could not save"));
    } finally {
      setBusy(false);
    }
  };

  const post = async (what: "submit" | "return") => {
    if (!sheet) return;
    setBusy(true);
    try {
      apply(
        (await unwrap(api.post(`/marks/${sheet.paper.id}/sections/${sheet.sectionId}/${what}`))) as MarkSheet,
        what === "submit" ? "Marks submitted" : "Returned to the teacher",
      );
    } catch (err) {
      toast.error(errorText(err, what === "submit" ? "Could not submit" : "Could not return"));
    } finally {
      setBusy(false);
      setConfirmSubmit(false);
    }
  };

  if (onAllCampuses) {
    return (
      <div>
        <PageHeader title="Marks entry" />
        <div className="panel">
          <EmptyState icon={PencilLine} title="Pick a campus first" description="Marks are entered for one campus at a time. Choose a campus from the switcher at the top." />
        </div>
      </div>
    );
  }

  const live = (r: MarkRow) => {
    if (!paper) return { obtained: null as number | null, grade: null as string | null, fail: false };
    if (r.status === "Exempted") return { obtained: null, grade: null, fail: false };
    if (r.status === "Absent") return { obtained: 0, grade: gradeFor(0, sheet!.bands).grade, fail: true };
    if (r.theory == null && r.practical == null) return { obtained: null, grade: null, fail: false };
    const got = (r.theory ?? 0) + (r.practical ?? 0);
    const g = gradeFor(round2((got * 100) / paper.totalMarks), sheet!.bands);
    return { obtained: got, grade: g.grade, fail: got < paper.passMarks };
  };

  return (
    <div>
      <PageHeader title="Marks entry" description="Enter marks paper by paper. Save as often as you like; submit when the whole section is done." />

      <Panel className="mb-4">
        <Field label="Paper" className="max-w-xl">
          <Select value={current} onChange={(e) => setCurrent(e.target.value)} disabled={!papers?.length}>
            {papers?.length === 0 ? <option value="">No papers for you</option> : null}
            {groups.map(([exam, list]) => (
              <optgroup key={exam} label={exam}>
                {list.map((p) => (
                  <option key={key(p)} value={key(p)}>
                    {p.published ? "🏆 " : p.status === "Submitted" ? "✓ " : "• "}
                    {p.className} – {p.sectionName} · {p.subjectName}
                  </option>
                ))}
              </optgroup>
            ))}
          </Select>
        </Field>
      </Panel>

      {papers?.length === 0 ? (
        <div className="panel">
          <EmptyState
            icon={PencilLine}
            title="No papers to enter"
            description="Papers appear here once an exam is open and you teach that subject in a section."
          />
        </div>
      ) : !sheet || !paper ? (
        <Skeleton className="h-64 rounded-xl" />
      ) : (
        <Panel
          flush
          title={`${sheet.className} – ${sheet.sectionName} · ${paper.subjectName}`}
          description={`${sheet.examName} · out of ${num(paper.totalMarks)}${paper.hasPractical ? ` (theory ${num(theoryMax)} + practical ${num(paper.practicalMarks)})` : ""} · pass ${num(paper.passMarks)}`}
          action={
            <div className="flex flex-wrap items-center gap-2">
              <StatusPill tone={sheet.published ? "success" : paperStatusTone[sheet.status]}>
                {sheet.published ? "Published" : paperStatusLabel[sheet.status]}
              </StatusPill>
              {sheet.canReturn ? (
                <Btn size="sm" variant="outline" disabled={busy} onClick={() => void post("return")}>
                  <Undo2 /> Return to teacher
                </Btn>
              ) : null}
              {editable ? (
                <>
                  <Btn size="sm" variant="outline" disabled={busy || entered.length === 0} onClick={() => void save()}>
                    <Save /> {busy ? "Saving…" : "Save"}
                  </Btn>
                  {sheet.status !== "Submitted" ? (
                    <Btn size="sm" disabled={busy} onClick={() => setConfirmSubmit(true)}>
                      <Send /> Submit
                    </Btn>
                  ) : null}
                </>
              ) : null}
            </div>
          }
        >
          {!editable ? (
            <div className="flex items-center gap-2 border-b border-[var(--border)] bg-[var(--muted)]/60 px-5 py-2.5 text-sm text-[var(--muted-foreground)]">
              <Lock className="size-4 shrink-0" /> {sheet.lockedReason ?? "You cannot change these marks."}
            </div>
          ) : null}
          {rows.length === 0 ? (
            <p className="px-5 py-6 text-sm text-[var(--muted-foreground)]">No students in this section for this exam.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[600px] text-left text-sm">
                <thead>
                  <tr className="border-b border-[var(--border)] bg-[var(--muted)]/60 text-xs text-[var(--muted-foreground)]">
                    <th className="w-12 px-5 py-2.5 font-medium">Roll</th>
                    <th className="px-3 py-2.5 font-medium">Name</th>
                    <th className="px-3 py-2.5 font-medium">Status</th>
                    <th className="px-3 py-2.5 font-medium">{paper.hasPractical ? `Theory /${num(theoryMax)}` : `Marks /${num(theoryMax)}`}</th>
                    {paper.hasPractical ? <th className="px-3 py-2.5 font-medium">Practical /{num(paper.practicalMarks)}</th> : null}
                    <th className="px-3 py-2.5 text-right font-medium">Total</th>
                    <th className="px-5 py-2.5 text-right font-medium">Grade</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => {
                    const l = live(r);
                    const present = (r.status ?? "Present") === "Present";
                    return (
                      <tr key={r.studentId} className="border-b border-[var(--border)] last:border-b-0">
                        <td className="tabular px-5 py-2 text-xs text-[var(--muted-foreground)]">{r.rollNo}</td>
                        <td className="px-3 py-2 font-medium">{r.name}</td>
                        <td className="px-3 py-2">
                          <div role="radiogroup" aria-label={`Status for ${r.name}`} className="inline-flex gap-1">
                            {(["Present", "Absent", "Exempted"] as MarkStatus[]).map((o) => (
                              <button
                                key={o}
                                type="button"
                                role="radio"
                                aria-checked={r.status === o}
                                title={o}
                                disabled={!editable}
                                onClick={() => setRow(r.studentId, { status: o })}
                                className={cn(
                                  "h-8 min-w-9 rounded-md border px-2 text-xs font-semibold transition outline-none focus-visible:ring-4 focus-visible:ring-[var(--ring)] disabled:cursor-not-allowed disabled:opacity-50",
                                  r.status === o
                                    ? statusStyle[o]
                                    : "border-[var(--border)] bg-[var(--card)] text-[var(--muted-foreground)] hover:border-[var(--border-strong)]",
                                )}
                              >
                                {statusCode[o]}
                              </button>
                            ))}
                          </div>
                        </td>
                        <td className="px-3 py-2">
                          <MarkInput
                            label={`Theory marks for ${r.name}`}
                            value={r.theory}
                            max={theoryMax}
                            disabled={!editable || !present}
                            onChange={(v) => setRow(r.studentId, { theory: v, status: r.status ?? "Present" })}
                          />
                        </td>
                        {paper.hasPractical ? (
                          <td className="px-3 py-2">
                            <MarkInput
                              label={`Practical marks for ${r.name}`}
                              value={r.practical}
                              max={paper.practicalMarks ?? 0}
                              disabled={!editable || !present}
                              onChange={(v) => setRow(r.studentId, { practical: v, status: r.status ?? "Present" })}
                            />
                          </td>
                        ) : null}
                        <td className={cn("tabular px-3 py-2 text-right", l.fail && "font-semibold text-[var(--danger)]")}>{num(l.obtained)}</td>
                        <td className="px-5 py-2 text-right">{l.grade ? <StatusPill tone={l.fail ? "danger" : "neutral"}>{l.grade}</StatusPill> : "—"}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </Panel>
      )}

      <ConfirmDialog
        open={confirmSubmit}
        title="Submit these marks?"
        description="Unsaved changes are not submitted — save first. After submitting, only an admin can return the marks for changes."
        confirmLabel="Submit"
        busy={busy}
        onConfirm={() => void post("submit")}
        onClose={() => setConfirmSubmit(false)}
      />
    </div>
  );
}

function MarkInput({
  label,
  value,
  max,
  disabled,
  onChange,
}: {
  label: string;
  value?: number | null;
  max: number;
  disabled?: boolean;
  onChange: (v: number | null) => void;
}) {
  const over = value != null && (value > max || value < 0);
  return (
    <TextInput
      type="number"
      inputMode="decimal"
      min={0}
      max={max}
      step="0.5"
      aria-label={label}
      aria-invalid={over}
      className={cn("h-8 w-20 text-sm", over && "border-[var(--danger)]")}
      value={value ?? ""}
      disabled={disabled}
      onChange={(e) => onChange(e.target.value === "" ? null : Number(e.target.value))}
    />
  );
}
