import { useCallback, useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { ArrowLeft, Printer, Save } from "lucide-react";
import { toast } from "sonner";
import { StudentPhoto } from "@/components/student-photo";
import { Btn, EmptyState, Skeleton, TextArea } from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { ExamDetail, ExamType, ReportCard } from "@/lib/types";
import { examTypeLabel } from "@/lib/types";
import { errorText } from "@/pages/hiring/shared";
import { num, pct2 } from "./shared";

const TYPES: ExamType[] = ["Monthly", "MidTerm", "Final", "Other"];

/** Print page: one A4 report card per student (exam or annual). */
export function ReportCardsPage() {
  const { can } = useAuth();
  const [params] = useSearchParams();
  const annual = params.get("annual") === "1";
  const examId = Number(params.get("examId")) || 0;
  const sectionId = params.get("sectionId");
  const studentId = params.get("studentId");
  const [sessionId, setSessionId] = useState(Number(params.get("sessionId")) || 0);
  const [cards, setCards] = useState<ReportCard[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    const q = new URLSearchParams();
    if (sectionId) q.set("sectionId", sectionId);
    if (studentId) q.set("studentId", studentId);
    try {
      if (annual) {
        q.set("sessionId", String(sessionId));
        setCards((await unwrap(api.get(`/report-cards/annual?${q}`))) as ReportCard[]);
      } else {
        const [list, detail] = await Promise.all([
          unwrap(api.get(`/report-cards/exam/${examId}?${q}`)) as Promise<ReportCard[]>,
          unwrap(api.get(`/exams/${examId}`)) as Promise<ExamDetail>,
        ]);
        setSessionId(detail.exam.sessionId);
        setCards(list);
      }
    } catch (err) {
      setError(errorText(err, "Could not load the report cards"));
      setCards([]);
    }
  }, [annual, examId, sectionId, studentId, sessionId]);

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [annual, examId, sectionId, studentId]);

  const back = annual
    ? `/admin/exams/results?annual=1&sectionId=${sectionId ?? ""}`
    : `/admin/exams/results?examId=${examId}&sectionId=${sectionId ?? ""}`;

  return (
    <div>
      <div className="no-print mb-4 flex flex-wrap items-center justify-between gap-3">
        <Link to={back} className="inline-flex items-center gap-1 text-sm text-[var(--muted-foreground)] hover:text-[var(--foreground)]">
          <ArrowLeft className="size-4" /> Results
        </Link>
        <div className="flex items-center gap-3">
          {cards?.length ? <span className="text-sm text-[var(--muted-foreground)]">{cards.length} cards · A4, one per page</span> : null}
          <Btn onClick={() => window.print()} disabled={!cards?.length}>
            <Printer /> Print
          </Btn>
        </div>
      </div>

      {cards == null ? (
        <Skeleton className="mx-auto h-[600px] max-w-[210mm] rounded-xl" />
      ) : cards.length === 0 ? (
        <div className="panel">
          <EmptyState icon={Printer} title="No cards" description={error ?? "There is nothing to print for this selection."} />
        </div>
      ) : (
        <div className="print-area space-y-6">
          {cards.map((c) => (
            <Card
              key={c.student.studentId}
              card={c}
              canRemark={can("results", "View")}
              remarkTarget={{ examId: annual ? null : examId, sessionId }}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function Card({
  card,
  canRemark,
  remarkTarget,
}: {
  card: ReportCard;
  canRemark: boolean;
  remarkTarget: { examId: number | null; sessionId: number };
}) {
  const [remark, setRemark] = useState(card.remark ?? "");
  const [saved, setSaved] = useState(card.remark ?? "");
  const [busy, setBusy] = useState(false);
  const s = card.student;

  const saveRemark = async () => {
    setBusy(true);
    try {
      await unwrap(api.put("/results/remarks", { ...remarkTarget, studentId: s.studentId, remark: remark.trim() }));
      setSaved(remark.trim());
      toast.success(`Remark saved for ${s.name}`);
    } catch (err) {
      toast.error(errorText(err, "Could not save the remark"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <article className="report-card mx-auto max-w-[210mm] rounded-xl border border-[var(--border)] bg-white p-8 text-[13px] text-neutral-900 shadow-[var(--shadow-xs)]">
      <header className="mb-5 border-b-2 border-neutral-800 pb-3 text-center">
        <div className="text-xl font-bold tracking-wide uppercase">{card.schoolName}</div>
        <div className="text-sm text-neutral-600">{card.campusName}</div>
        <div className="mt-2 text-base font-semibold">
          {card.title} · {card.sessionName}
        </div>
      </header>

      <section className="mb-5 flex items-center gap-4">
        <StudentPhoto studentId={s.studentId} name={s.name} hasPhoto={s.hasPhoto} size="xl" />
        <dl className="grid flex-1 grid-cols-2 gap-x-6 gap-y-1">
          <Info label="Name" value={s.name} />
          <Info label="Admission no." value={s.admissionNo} />
          <Info label="Class" value={`${s.className} – ${s.sectionName}`} />
          <Info label="Roll no." value={String(s.rollNo || "—")} />
        </dl>
      </section>

      {card.annual ? (
        <table className="mb-4 w-full border-collapse">
          <thead>
            <tr className="bg-neutral-100">
              <Th>Exam</Th>
              <Th>Type</Th>
              <Th right>%</Th>
            </tr>
          </thead>
          <tbody>
            {(card.exams ?? []).map((e) => (
              <tr key={e.examId}>
                <Tc>{e.name}</Tc>
                <Tc>{examTypeLabel[e.type]}</Tc>
                <Tc right>{pct2(e.percent)}</Tc>
              </tr>
            ))}
            {TYPES.filter((t) => card.annual!.effectiveWeights[t] != null).map((t) => (
              <tr key={t} className="bg-neutral-50">
                <Tc>
                  {examTypeLabel[t]} average · weight {card.annual!.effectiveWeights[t]}%
                </Tc>
                <Tc />
                <Tc right>{pct2(card.annual!.typePercents[t])}</Tc>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        <table className="mb-4 w-full border-collapse">
          <thead>
            <tr className="bg-neutral-100">
              <Th>Subject</Th>
              <Th right>Total</Th>
              <Th right>Theory</Th>
              <Th right>Practical</Th>
              <Th right>Obtained</Th>
              <Th right>Grade</Th>
            </tr>
          </thead>
          <tbody>
            {card.subjects.map((x) => (
              <tr key={x.subjectId}>
                <Tc>{x.name}</Tc>
                <Tc right>{num(x.total)}</Tc>
                <Tc right>{x.status === "Present" ? num(x.theory) : "—"}</Tc>
                <Tc right>{x.status === "Present" && x.practical != null ? num(x.practical) : "—"}</Tc>
                <Tc right>
                  <span className={x.passed === false ? "font-bold" : undefined}>
                    {x.status === "Absent" ? "Absent" : x.status === "Exempted" ? "Exempt" : num(x.obtained)}
                  </span>
                </Tc>
                <Tc right>{x.grade ?? "—"}</Tc>
              </tr>
            ))}
            <tr className="bg-neutral-100 font-semibold">
              <Tc>Total</Tc>
              <Tc right>{num(card.total)}</Tc>
              <Tc />
              <Tc />
              <Tc right>{num(card.obtained)}</Tc>
              <Tc right>{card.grade ?? "—"}</Tc>
            </tr>
          </tbody>
        </table>
      )}

      <section className="mb-4 grid grid-cols-2 gap-2 sm:grid-cols-4">
        <Stat label="Percentage" value={pct2(card.percent)} />
        <Stat label="Grade" value={card.grade ?? "—"} />
        <Stat label="Result" value={card.passed == null ? "—" : card.passed ? "Pass" : "Fail"} />
        {card.annual ? (
          <Stat label="Attendance" value={card.attendancePct == null ? "—" : `${card.attendancePct.toFixed(1)}%`} />
        ) : (
          <Stat label="Position" value={card.position ? String(card.position) : "—"} />
        )}
      </section>
      {!card.annual ? (
        <div className="mb-4 text-sm">
          Attendance: <strong>{card.attendancePct == null ? "—" : `${card.attendancePct.toFixed(1)}%`}</strong>
        </div>
      ) : null}

      <section className="mb-10">
        <div className="mb-1 text-xs font-semibold tracking-wide text-neutral-600 uppercase">Class teacher's remark</div>
        <p className="print-only min-h-10 border-b border-neutral-300 pb-1">{saved || " "}</p>
        {canRemark ? (
          <div className="no-print flex items-start gap-2">
            <TextArea
              rows={2}
              maxLength={300}
              aria-label={`Remark for ${s.name}`}
              className="flex-1 bg-white text-neutral-900"
              value={remark}
              placeholder="Write a remark (class teacher or admin)"
              onChange={(e) => setRemark(e.target.value)}
            />
            <Btn size="sm" variant="outline" disabled={busy || remark.trim() === saved} onClick={() => void saveRemark()}>
              <Save /> Save
            </Btn>
          </div>
        ) : (
          <p className="no-print min-h-10 border-b border-neutral-300 pb-1">{saved || " "}</p>
        )}
      </section>

      <footer className="grid grid-cols-3 gap-6 pt-6 text-center text-xs text-neutral-600">
        {["Class teacher", "Principal", "Parent / guardian"].map((x) => (
          <div key={x} className="border-t border-neutral-500 pt-1">
            {x}
          </div>
        ))}
      </footer>
    </article>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex gap-2">
      <dt className="text-neutral-500">{label}:</dt>
      <dd className="font-medium">{value}</dd>
    </div>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-neutral-300 px-3 py-2 text-center">
      <div className="text-[11px] text-neutral-500 uppercase">{label}</div>
      <div className="text-base font-bold">{value}</div>
    </div>
  );
}

function Th({ children, right }: { children?: React.ReactNode; right?: boolean }) {
  return <th className={`border border-neutral-300 px-2 py-1.5 text-xs font-semibold ${right ? "text-right" : "text-left"}`}>{children}</th>;
}

function Tc({ children, right }: { children?: React.ReactNode; right?: boolean }) {
  return <td className={`border border-neutral-300 px-2 py-1.5 ${right ? "text-right tabular" : ""}`}>{children}</td>;
}
