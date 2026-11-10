import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Printer, Trophy } from "lucide-react";
import { EmptyState, Panel, Skeleton } from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import { examTypeLabel, type StudentResults } from "@/lib/types";
import { PassPill, pct2 } from "@/pages/exams/shared";

/** Current-session published exam results of one student, with the annual summary. */
export function StudentResultsTab({ studentId }: { studentId: number }) {
  const [data, setData] = useState<StudentResults | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    unwrap(api.get(`/students/${studentId}/results`))
      .then((d) => setData(d as StudentResults))
      .catch((err) => setError(err instanceof Error ? err.message : "Could not load results"));
  }, [studentId]);

  if (error) return <p className="text-sm text-[var(--muted-foreground)]">{error}</p>;
  if (!data) return <Skeleton className="h-48 rounded-xl" />;

  return (
    <div className="grid gap-4 lg:grid-cols-3">
      <Panel title="Annual (so far)">
        <div className="text-4xl font-semibold tabular-nums">{pct2(data.annual?.percent)}</div>
        <div className="mt-2 flex items-center gap-2 text-sm">
          <span className="font-semibold">{data.annual?.grade ?? "—"}</span>
          <PassPill passed={data.annual?.passed} />
        </div>
        <p className="mt-3 text-xs text-[var(--muted-foreground)]">From the published exams of the current session.</p>
      </Panel>
      <Panel flush title="Published exams" className="lg:col-span-2">
        {data.exams.length === 0 ? (
          <EmptyState icon={Trophy} title="No published results" description="Results show here once a section's result is published." />
        ) : (
          <ul className="divide-y divide-[var(--border)]">
            {data.exams.map((e) => (
              <li key={e.examId} className="flex flex-wrap items-center gap-3 px-5 py-3 text-sm">
                <div className="min-w-0 flex-1">
                  <div className="font-medium">{e.name}</div>
                  <div className="text-xs text-[var(--muted-foreground)]">{examTypeLabel[e.type]}</div>
                </div>
                <span className="tabular w-20 text-right">{pct2(e.percent)}</span>
                <span className="w-8 text-center font-semibold">{e.grade ?? "—"}</span>
                <span className="tabular w-16 text-xs text-[var(--muted-foreground)]">{e.position ? `Pos. ${e.position}` : ""}</span>
                <PassPill passed={e.passed} />
                <Link
                  to={`/admin/exams/cards?examId=${e.examId}&studentId=${studentId}`}
                  className="inline-flex items-center gap-1 text-xs text-[var(--primary)] hover:underline"
                >
                  <Printer className="size-3.5" /> Card
                </Link>
              </li>
            ))}
          </ul>
        )}
      </Panel>
    </div>
  );
}
