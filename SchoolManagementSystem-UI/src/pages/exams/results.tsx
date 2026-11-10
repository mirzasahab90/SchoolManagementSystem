import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { AlertTriangle, Download, Printer, Trophy, Undo2, Upload } from "lucide-react";
import { toast } from "sonner";
import { Btn, EmptyState, Field, PageHeader, Panel, Select, Skeleton, StatusPill } from "@/components/sms-ui";
import { api, downloadBlob, saveBlob, unwrap } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { cn } from "@/lib/utils";
import type { AnnualRow, Exam, ExamDetail, ExamType, Tabulation } from "@/lib/types";
import { examTypeLabel } from "@/lib/types";
import { errorText, formatDateTime } from "@/pages/hiring/shared";
import { PassPill, num, pct2, useSessions } from "./shared";

const TYPES: ExamType[] = ["Monthly", "MidTerm", "Final", "Other"];

export function ResultsPage() {
  const { user: me, can } = useAuth();
  const [params, setParams] = useSearchParams();
  const sessions = useSessions();
  const [sessionId, setSessionId] = useState(Number(params.get("sessionId")) || 0);
  const [exams, setExams] = useState<Exam[] | null>(null);
  const [examId, setExamId] = useState(Number(params.get("examId")) || 0);
  const [detail, setDetail] = useState<ExamDetail | null>(null);
  const [sectionId, setSectionId] = useState(Number(params.get("sectionId")) || 0);
  const [annual, setAnnual] = useState(params.get("annual") === "1");
  const [tab, setTab] = useState<Tabulation | null>(null);
  const [annualRows, setAnnualRows] = useState<AnnualRow[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const onAllCampuses = me?.activeCampusId == null;

  useEffect(() => {
    // No session yet, or the one from the URL is not listed: fall back to the current session.
    if (sessions && !sessions.items.some((s) => s.id === sessionId))
      setSessionId(sessions.effectiveCurrentId ?? sessions.items[0]?.id ?? 0);
  }, [sessions, sessionId]);

  useEffect(() => {
    if (!sessionId || onAllCampuses) return;
    unwrap(api.get(`/exams?sessionId=${sessionId}`))
      .then((e) => {
        const list = (e as Exam[]).filter((x) => x.status !== "Draft");
        setExams(list);
        setExamId((cur) => (cur && list.some((x) => x.id === cur) ? cur : list[0]?.id ?? 0));
      })
      .catch((err) => {
        toast.error(errorText(err, "Could not load exams"));
        setExams([]);
      });
  }, [sessionId, onAllCampuses]);

  useEffect(() => {
    if (!examId) return setDetail(null);
    unwrap(api.get(`/exams/${examId}`))
      .then((d) => {
        const det = d as ExamDetail;
        setDetail(det);
        const ids = det.classes.flatMap((c) => c.sections.map((s) => s.sectionId));
        setSectionId((cur) => (cur && ids.includes(cur) ? cur : ids[0] ?? 0));
      })
      .catch((err) => toast.error(errorText(err, "Could not load the exam")));
  }, [examId]);

  const load = useCallback(async () => {
    if (!examId || !sectionId) return;
    setTab(null);
    setAnnualRows(null);
    setLoadError(null);
    try {
      if (annual) setAnnualRows((await unwrap(api.get(`/results/annual?sessionId=${sessionId}&sectionId=${sectionId}`))) as AnnualRow[]);
      else setTab((await unwrap(api.get(`/results/${examId}/sections/${sectionId}`))) as Tabulation);
    } catch (err) {
      setLoadError(errorText(err, "Could not load the result"));
    }
  }, [examId, sectionId, annual, sessionId]);

  useEffect(() => {
    void load();
  }, [load]);

  // Keep the picked session, exam, section and view in the URL so refresh, back and shared links keep them.
  useEffect(() => {
    if (!sessionId) return;
    const next = new URLSearchParams(params);
    const put = (key: string, value: string | null) => (value ? next.set(key, value) : next.delete(key));
    put("sessionId", String(sessionId));
    put("examId", examId ? String(examId) : null);
    put("sectionId", sectionId ? String(sectionId) : null);
    put("annual", annual ? "1" : null);
    if (next.toString() !== params.toString()) setParams(next, { replace: true });
  }, [params, setParams, sessionId, examId, sectionId, annual]);

  const sections = useMemo(
    () => detail?.classes.flatMap((c) => c.sections.map((s) => ({ ...s, className: c.className }))) ?? [],
    [detail],
  );

  const act = async (what: "publish" | "unpublish") => {
    setBusy(true);
    try {
      setTab((await unwrap(api.post(`/results/${examId}/sections/${sectionId}/${what}`))) as Tabulation);
      toast.success(what === "publish" ? "Result published" : "Result unpublished — marks can be changed again");
    } catch (err) {
      toast.error(errorText(err, "Could not change the result"));
    } finally {
      setBusy(false);
    }
  };

  const csv = async () => {
    try {
      const { blob, fileName } = await downloadBlob(`/results/${examId}/sections/${sectionId}/csv`);
      saveBlob(blob, fileName ?? "result.csv");
    } catch (err) {
      toast.error(errorText(err, "Download failed"));
    }
  };

  if (onAllCampuses) {
    return (
      <div>
        <PageHeader title="Results" />
        <div className="panel">
          <EmptyState icon={Trophy} title="Pick a campus first" description="Results are shown for one campus at a time. Choose a campus from the switcher at the top." />
        </div>
      </div>
    );
  }

  const cardsHref = annual
    ? `/admin/exams/cards?annual=1&sessionId=${sessionId}&sectionId=${sectionId}`
    : `/admin/exams/cards?examId=${examId}&sectionId=${sectionId}`;

  return (
    <div>
      <PageHeader
        title="Results"
        description="Live until a section is published; publishing freezes the result for report cards."
        actions={
          examId && sectionId ? (
            <>
              {!annual && tab ? (
                <Btn variant="outline" onClick={() => void csv()}>
                  <Download /> CSV
                </Btn>
              ) : null}
              {annual || tab?.published ? (
                <Link to={cardsHref}>
                  <Btn variant="outline" tabIndex={-1}>
                    <Printer /> Print cards
                  </Btn>
                </Link>
              ) : null}
            </>
          ) : null
        }
      />

      <Panel className="mb-4">
        <div className="flex flex-wrap items-end gap-3">
          <Field label="Session" className="w-44">
            <Select value={sessionId} onChange={(e) => setSessionId(Number(e.target.value))}>
              {sessions?.items.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </Select>
          </Field>
          {!annual ? (
            <Field label="Exam" className="min-w-52 flex-1 sm:flex-none">
              <Select value={examId} onChange={(e) => setExamId(Number(e.target.value))} disabled={!exams?.length}>
                {exams?.length === 0 ? <option value={0}>No open or closed exams</option> : null}
                {exams?.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name}
                  </option>
                ))}
              </Select>
            </Field>
          ) : null}
          <Field label="Section" className="min-w-44 flex-1 sm:flex-none">
            <Select value={sectionId} onChange={(e) => setSectionId(Number(e.target.value))} disabled={!sections.length}>
              {sections.map((s) => (
                <option key={s.sectionId} value={s.sectionId}>
                  {s.published ? "🏆 " : ""}
                  {s.className} – {s.sectionName}
                </option>
              ))}
            </Select>
          </Field>
          <div role="tablist" aria-label="Result view" className="inline-flex rounded-lg border border-[var(--border)] p-0.5">
            {[
              { v: false, label: "Exam" },
              { v: true, label: "Annual" },
            ].map((o) => (
              <button
                key={o.label}
                type="button"
                role="tab"
                aria-selected={annual === o.v}
                onClick={() => setAnnual(o.v)}
                className={cn(
                  "h-8 rounded-md px-3 text-sm font-medium transition outline-none focus-visible:ring-4 focus-visible:ring-[var(--ring)]",
                  annual === o.v ? "bg-[var(--primary)] text-[var(--primary-foreground)]" : "text-[var(--muted-foreground)] hover:text-[var(--foreground)]",
                )}
              >
                {o.label}
              </button>
            ))}
          </div>
        </div>
      </Panel>

      {exams?.length === 0 ? (
        <div className="panel">
          <EmptyState icon={Trophy} title="No results yet" description="Results appear once an exam is opened and marks are entered." />
        </div>
      ) : loadError ? (
        <div className="panel">
          <EmptyState icon={Trophy} title="Not available" description={loadError} />
        </div>
      ) : annual ? (
        annualRows == null ? (
          <Skeleton className="h-64 rounded-xl" />
        ) : (
          <AnnualTable rows={annualRows} />
        )
      ) : !tab ? (
        <Skeleton className="h-64 rounded-xl" />
      ) : (
        <Panel
          flush
          title={`${tab.className} – ${tab.sectionName} · ${tab.examName}`}
          description={tab.published ? `Published ${formatDateTime(tab.publishedOn)}` : "Live — changes as marks are entered"}
          action={
            can("results", "Edit") ? (
              tab.published ? (
                <Btn size="sm" variant="outline" disabled={busy} onClick={() => void act("unpublish")}>
                  <Undo2 /> Unpublish
                </Btn>
              ) : (
                <Btn size="sm" disabled={busy || tab.pendingSubjects.length > 0 || tab.rows.length === 0} onClick={() => void act("publish")}>
                  <Upload /> Publish
                </Btn>
              )
            ) : tab.published ? (
              <StatusPill tone="success">Published</StatusPill>
            ) : null
          }
        >
          {!tab.published && tab.pendingSubjects.length ? (
            <div className="flex items-start gap-2 border-b border-[var(--border)] bg-[var(--warning-soft)] px-5 py-2.5 text-sm text-[var(--warning)]">
              <AlertTriangle className="mt-0.5 size-4 shrink-0" />
              <span>Not submitted yet: {tab.pendingSubjects.join(", ")}.</span>
            </div>
          ) : null}
          <TabTable tab={tab} />
        </Panel>
      )}
    </div>
  );
}

function TabTable({ tab }: { tab: Tabulation }) {
  if (tab.rows.length === 0) return <p className="px-5 py-6 text-sm text-[var(--muted-foreground)]">No students in this section for this exam.</p>;
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-sm">
        <thead>
          <tr className="border-b border-[var(--border)] bg-[var(--muted)] text-xs text-[var(--muted-foreground)]">
            <th className="sticky left-0 z-10 min-w-44 bg-[var(--muted)] px-4 py-2.5 font-medium">Student</th>
            {tab.subjects.map((s) => (
              <th key={s} className="px-3 py-2.5 text-center font-medium whitespace-nowrap">
                {s}
              </th>
            ))}
            <th className="px-3 py-2.5 text-right font-medium">Total</th>
            <th className="px-3 py-2.5 text-right font-medium">%</th>
            <th className="px-3 py-2.5 text-center font-medium">Grade</th>
            <th className="px-3 py-2.5 text-center font-medium">Pos.</th>
            <th className="px-4 py-2.5 font-medium">Result</th>
          </tr>
        </thead>
        <tbody>
          {tab.rows.map((r) => (
            <tr key={r.studentId} className="border-b border-[var(--border)] last:border-b-0 hover:bg-[var(--card-hover)]">
              <td className="sticky left-0 z-10 bg-[var(--card)] px-4 py-2">
                <span className="tabular mr-2 text-xs text-[var(--muted-foreground)]">{r.rollNo}</span>
                <span className="font-medium">{r.name}</span>
              </td>
              {tab.subjects.map((name) => {
                const s = r.subjects.find((x) => x.name === name);
                return (
                  <td key={name} className="px-3 py-2 text-center whitespace-nowrap">
                    {!s || s.status == null ? (
                      <span className="text-[var(--muted-foreground)]">—</span>
                    ) : s.status === "Exempted" ? (
                      <span className="text-xs text-[var(--info)]">Exempt</span>
                    ) : (
                      <span className={cn("tabular", s.passed === false && "font-semibold text-[var(--danger)]")}>
                        {s.status === "Absent" ? "Abs" : num(s.obtained)}
                        <span className="ml-1 text-xs text-[var(--muted-foreground)]">{s.grade}</span>
                      </span>
                    )}
                  </td>
                );
              })}
              <td className="tabular px-3 py-2 text-right">{r.total != null ? `${num(r.obtained)}/${num(r.total)}` : "—"}</td>
              <td className="tabular px-3 py-2 text-right">{pct2(r.percent)}</td>
              <td className="px-3 py-2 text-center font-semibold">{r.grade ?? "—"}</td>
              <td className="tabular px-3 py-2 text-center">{r.position ?? "—"}</td>
              <td className="px-4 py-2">
                <PassPill passed={r.passed} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function AnnualTable({ rows }: { rows: AnnualRow[] }) {
  if (rows.length === 0) {
    return (
      <div className="panel">
        <EmptyState icon={Trophy} title="No students" description="No students in this section this session." />
      </div>
    );
  }
  const weights = rows[0].result.effectiveWeights;
  return (
    <Panel flush title="Annual result" description="Computed from the published exams of this session.">
      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-[var(--border)] bg-[var(--muted)] text-xs text-[var(--muted-foreground)]">
              <th className="sticky left-0 z-10 min-w-44 bg-[var(--muted)] px-4 py-2.5 font-medium">Student</th>
              {TYPES.map((t) => (
                <th key={t} className="px-3 py-2.5 text-right font-medium whitespace-nowrap">
                  {examTypeLabel[t]}
                  {weights[t] != null ? <span className="ml-1 opacity-70">({weights[t]}%)</span> : null}
                </th>
              ))}
              <th className="px-3 py-2.5 text-right font-medium">Annual %</th>
              <th className="px-3 py-2.5 text-center font-medium">Grade</th>
              <th className="px-4 py-2.5 font-medium">Result</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.studentId} className="border-b border-[var(--border)] last:border-b-0">
                <td className="sticky left-0 z-10 bg-[var(--card)] px-4 py-2">
                  <span className="tabular mr-2 text-xs text-[var(--muted-foreground)]">{r.rollNo}</span>
                  <span className="font-medium">{r.name}</span>
                </td>
                {TYPES.map((t) => (
                  <td key={t} className="tabular px-3 py-2 text-right">
                    {pct2(r.result.typePercents[t])}
                  </td>
                ))}
                <td className="tabular px-3 py-2 text-right font-semibold">{pct2(r.result.percent)}</td>
                <td className="px-3 py-2 text-center font-semibold">{r.result.grade ?? "—"}</td>
                <td className="px-4 py-2">
                  <PassPill passed={r.result.passed} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Panel>
  );
}
