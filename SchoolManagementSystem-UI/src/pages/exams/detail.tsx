import { useCallback, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, CheckCircle2, Lock, PlayCircle, Save, Trash2, Trophy, Upload, Undo2 } from "lucide-react";
import { toast } from "sonner";
import { ConfirmDialog } from "@/components/modal";
import { OwnerPill } from "@/components/owner-pill";
import { Btn, IconButton, PageHeader, Panel, Skeleton, StatusPill, TextInput } from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { ExamClass, ExamDetail, ExamStatus, Paper, SectionProgress } from "@/lib/types";
import { errorText, formatDate } from "@/pages/hiring/shared";
import { ExamTypePill, examStatusTone } from "./shared";

export function ExamDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { can } = useAuth();
  const [detail, setDetail] = useState<ExamDetail | null>(null);
  const [busy, setBusy] = useState(false);
  const [confirm, setConfirm] = useState<null | "delete" | ExamStatus>(null);

  const load = useCallback(async () => {
    try {
      setDetail((await unwrap(api.get(`/exams/${id}`))) as ExamDetail);
    } catch (err) {
      toast.error(errorText(err, "Could not load the exam"));
      navigate("/admin/exams", { replace: true });
    }
  }, [id, navigate]);

  useEffect(() => {
    void load();
  }, [load]);

  if (!detail) return <Skeleton className="h-96 rounded-xl" />;
  const exam = detail.exam;
  const canEdit = can("exams", "Edit");
  const papersEditable = canEdit && exam.status !== "Closed";
  const canPublish = can("results", "Edit");

  const setStatus = async (status: ExamStatus) => {
    setBusy(true);
    try {
      await unwrap(api.post(`/exams/${exam.id}/status`, { status }));
      await load();
      toast.success(status === "Open" ? "Exam opened for marks entry" : status === "Closed" ? "Exam closed" : "Exam moved back to draft");
    } catch (err) {
      toast.error(errorText(err, "Could not change the status"));
    } finally {
      setBusy(false);
      setConfirm(null);
    }
  };

  const remove = async () => {
    setBusy(true);
    try {
      await unwrap(api.delete(`/exams/${exam.id}`));
      toast.success("Exam deleted");
      navigate("/admin/exams");
    } catch (err) {
      toast.error(errorText(err, "Could not delete"));
      setBusy(false);
      setConfirm(null);
    }
  };

  return (
    <div>
      <PageHeader
        eyebrow={
          <Link to="/admin/exams" className="inline-flex items-center gap-1 hover:text-[var(--foreground)]">
            <ArrowLeft className="size-3.5" /> Exams
          </Link>
        }
        title={exam.name}
        description={`${exam.sessionName} · ${exam.startDate ? `${formatDate(exam.startDate)} – ${formatDate(exam.endDate)}` : "Dates not set"}`}
        actions={
          <>
            <StatusPill tone={examStatusTone[exam.status]} dot>
              {exam.status}
            </StatusPill>
            <ExamTypePill type={exam.type} />
            <OwnerPill campusCode={exam.campusCode} />
            {canEdit && exam.status === "Draft" ? (
              <Btn onClick={() => void setStatus("Open")} disabled={busy}>
                <PlayCircle /> Open for marks
              </Btn>
            ) : null}
            {canEdit && exam.status === "Open" ? (
              <Btn variant="outline" onClick={() => setConfirm("Closed")} disabled={busy}>
                <Lock /> Close exam
              </Btn>
            ) : null}
            {canEdit && exam.status === "Closed" ? (
              <Btn variant="outline" onClick={() => void setStatus("Open")} disabled={busy}>
                <PlayCircle /> Reopen
              </Btn>
            ) : null}
            {can("exams", "Delete") ? (
              <IconButton label="Delete exam" tone="danger" onClick={() => setConfirm("delete")}>
                <Trash2 />
              </IconButton>
            ) : null}
          </>
        }
      />

      <div className="space-y-4">
        {detail.classes.map((c) => (
          <ClassPanel
            key={c.classId}
            examId={exam.id}
            cls={c}
            editable={papersEditable}
            canPublish={canPublish && exam.status !== "Draft"}
            reload={load}
          />
        ))}
      </div>

      <ConfirmDialog
        open={confirm === "delete"}
        title="Delete this exam?"
        description="Only an exam without marks can be deleted."
        busy={busy}
        onConfirm={() => void remove()}
        onClose={() => setConfirm(null)}
      />
      <ConfirmDialog
        open={confirm === "Closed"}
        title="Close this exam?"
        description="Teachers can no longer enter marks. Published results stay as they are."
        confirmLabel="Close exam"
        busy={busy}
        onConfirm={() => void setStatus("Closed")}
        onClose={() => setConfirm(null)}
      />
    </div>
  );
}

function ClassPanel({
  examId,
  cls,
  editable,
  canPublish,
  reload,
}: {
  examId: number;
  cls: ExamClass;
  editable: boolean;
  canPublish: boolean;
  reload: () => Promise<void>;
}) {
  return (
    <Panel flush title={cls.className} description={`${cls.papers.filter((p) => p.isActive).length} papers · ${cls.sections.length} sections`}>
      <div className="flex flex-wrap gap-2 border-b border-[var(--border)] px-5 py-3">
        {cls.sections.length === 0 ? (
          <span className="text-sm text-[var(--muted-foreground)]">No sections in this class.</span>
        ) : (
          cls.sections.map((s) => <SectionChip key={s.sectionId} examId={examId} s={s} canPublish={canPublish} reload={reload} />)
        )}
      </div>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[720px] text-left text-sm">
          <thead>
            <tr className="border-b border-[var(--border)] bg-[var(--muted)]/60 text-xs text-[var(--muted-foreground)]">
              <th className="px-5 py-2.5 font-medium">Subject</th>
              <th className="px-3 py-2.5 font-medium">Total</th>
              <th className="px-3 py-2.5 font-medium">Pass</th>
              <th className="px-3 py-2.5 font-medium">Practical</th>
              <th className="px-3 py-2.5 font-medium">Date</th>
              <th className="px-3 py-2.5 font-medium">Active</th>
              <th className="px-5 py-2.5" />
            </tr>
          </thead>
          <tbody>
            {cls.papers.map((p) => (
              <PaperRow key={p.id} examId={examId} paper={p} editable={editable} reload={reload} />
            ))}
          </tbody>
        </table>
      </div>
    </Panel>
  );
}

function SectionChip({ examId, s, canPublish, reload }: { examId: number; s: SectionProgress; canPublish: boolean; reload: () => Promise<void> }) {
  const [busy, setBusy] = useState(false);
  const done = s.papers > 0 && s.submitted === s.papers;
  const act = async (what: "publish" | "unpublish") => {
    setBusy(true);
    try {
      await unwrap(api.post(`/results/${examId}/sections/${s.sectionId}/${what}`));
      toast.success(what === "publish" ? `Section ${s.sectionName} published` : `Section ${s.sectionName} unpublished`);
      await reload();
    } catch (err) {
      toast.error(errorText(err, "Could not change the result"));
    } finally {
      setBusy(false);
    }
  };
  return (
    <div className="flex items-center gap-2 rounded-lg border border-[var(--border)] px-2.5 py-1.5 text-sm">
      <Link to={`/admin/exams/results?examId=${examId}&sectionId=${s.sectionId}`} className="font-medium hover:underline">
        {s.sectionName}
      </Link>
      {s.published ? (
        <StatusPill tone="success">
          <Trophy className="size-3" /> Published
        </StatusPill>
      ) : (
        <StatusPill tone={done ? "success" : s.submitted ? "warning" : "neutral"}>
          {done ? <CheckCircle2 className="size-3" /> : null}
          {s.submitted}/{s.papers}
        </StatusPill>
      )}
      {canPublish ? (
        s.published ? (
          <Btn size="sm" variant="ghost" disabled={busy} onClick={() => void act("unpublish")}>
            <Undo2 /> Unpublish
          </Btn>
        ) : done ? (
          <Btn size="sm" variant="outline" disabled={busy} onClick={() => void act("publish")}>
            <Upload /> Publish
          </Btn>
        ) : null
      ) : null}
    </div>
  );
}

function PaperRow({ examId, paper, editable, reload }: { examId: number; paper: Paper; editable: boolean; reload: () => Promise<void> }) {
  const [p, setP] = useState(paper);
  const [busy, setBusy] = useState(false);
  useEffect(() => setP(paper), [paper]);
  const dirty = JSON.stringify(p) !== JSON.stringify(paper);

  const save = async () => {
    setBusy(true);
    try {
      await unwrap(
        api.put(`/exams/${examId}/papers/${p.id}`, {
          totalMarks: Number(p.totalMarks),
          passMarks: Number(p.passMarks),
          hasPractical: p.hasPractical,
          practicalMarks: p.hasPractical ? Number(p.practicalMarks ?? 0) : null,
          paperDate: p.paperDate || null,
          isActive: p.isActive,
        }),
      );
      await reload();
      toast.success(`${p.subjectName} saved`);
    } catch (err) {
      toast.error(errorText(err, "Could not save the paper"));
    } finally {
      setBusy(false);
    }
  };

  const numInput = (value: number | null | undefined, set: (v: number) => void, label: string) => (
    <TextInput
      type="number"
      min={0}
      step="0.5"
      aria-label={`${label} for ${p.subjectName}`}
      className="h-8 w-20 text-sm"
      value={value ?? ""}
      disabled={!editable}
      onChange={(e) => set(Number(e.target.value))}
    />
  );

  return (
    <tr className={`border-b border-[var(--border)] last:border-b-0 ${p.isActive ? "" : "opacity-60"}`}>
      <td className="px-5 py-2 font-medium">{p.subjectName}</td>
      <td className="px-3 py-2">{numInput(p.totalMarks, (v) => setP({ ...p, totalMarks: v }), "Total marks")}</td>
      <td className="px-3 py-2">{numInput(p.passMarks, (v) => setP({ ...p, passMarks: v }), "Pass marks")}</td>
      <td className="px-3 py-2">
        <div className="flex items-center gap-2">
          <input
            type="checkbox"
            aria-label={`Practical for ${p.subjectName}`}
            className="size-4 accent-[var(--primary)]"
            checked={p.hasPractical}
            disabled={!editable}
            onChange={(e) => setP({ ...p, hasPractical: e.target.checked, practicalMarks: e.target.checked ? p.practicalMarks ?? 25 : null })}
          />
          {p.hasPractical ? numInput(p.practicalMarks, (v) => setP({ ...p, practicalMarks: v }), "Practical marks") : null}
        </div>
      </td>
      <td className="px-3 py-2">
        <TextInput
          type="date"
          aria-label={`Date for ${p.subjectName}`}
          className="h-8 w-36 text-sm"
          value={p.paperDate ?? ""}
          disabled={!editable}
          onChange={(e) => setP({ ...p, paperDate: e.target.value || null })}
        />
      </td>
      <td className="px-3 py-2">
        <input
          type="checkbox"
          aria-label={`${p.subjectName} active`}
          className="size-4 accent-[var(--primary)]"
          checked={p.isActive}
          disabled={!editable}
          onChange={(e) => setP({ ...p, isActive: e.target.checked })}
        />
      </td>
      <td className="px-5 py-2 text-right">
        {editable && dirty ? (
          <Btn size="sm" disabled={busy} onClick={() => void save()}>
            <Save /> Save
          </Btn>
        ) : null}
      </td>
    </tr>
  );
}
