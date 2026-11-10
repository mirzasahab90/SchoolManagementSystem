import { useEffect, useState } from "react";
import { toast } from "sonner";
import { StatusPill, type StatusTone } from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import type { AcademicSessionList, ExamStatus, ExamType, GradeBand, PaperSectionStatus } from "@/lib/types";
import { examTypeLabel } from "@/lib/types";
import { errorText } from "@/pages/hiring/shared";

export const examStatusTone: Record<ExamStatus, StatusTone> = { Draft: "neutral", Open: "success", Closed: "info" };
export const examTypeTone: Record<ExamType, StatusTone> = { Monthly: "info", MidTerm: "primary", Final: "warning", Other: "neutral" };
export const paperStatusTone: Record<PaperSectionStatus, StatusTone> = { NotStarted: "neutral", InProgress: "warning", Submitted: "success" };
export const paperStatusLabel: Record<PaperSectionStatus, string> = { NotStarted: "Not started", InProgress: "In progress", Submitted: "Submitted" };

export function ExamTypePill({ type }: { type: ExamType }) {
  return <StatusPill tone={examTypeTone[type]}>{examTypeLabel[type]}</StatusPill>;
}

export function PassPill({ passed }: { passed?: boolean | null }) {
  if (passed == null) return <StatusPill>Not entered</StatusPill>;
  return <StatusPill tone={passed ? "success" : "danger"}>{passed ? "Pass" : "Fail"}</StatusPill>;
}

/** Same rule as the server: the band with the highest minimum not above the percentage. */
export function gradeFor(percent: number, bands: GradeBand[]): { grade: string; isFail: boolean } {
  const sorted = [...bands].sort((a, b) => b.minPercent - a.minPercent);
  const band = sorted.find((b) => b.minPercent <= percent) ?? sorted[sorted.length - 1];
  return band ? { grade: band.grade, isFail: band.isFail } : { grade: "-", isFail: false };
}

export const round2 = (v: number) => Math.round((v + Number.EPSILON) * 100) / 100;

export const num = (v?: number | null) => (v == null ? "—" : Number.isInteger(v) ? String(v) : v.toFixed(2).replace(/0$/, ""));

export const pct2 = (v?: number | null) => (v == null ? "—" : `${v.toFixed(2)}%`);

/** Sessions visible here plus the effective current one. */
export function useSessions() {
  const [sessions, setSessions] = useState<AcademicSessionList | null>(null);
  useEffect(() => {
    unwrap(api.get("/academic-sessions"))
      .then((s) => setSessions(s as AcademicSessionList))
      .catch((err) => {
        toast.error(errorText(err, "Could not load sessions"));
        setSessions({ items: [] });
      });
  }, []);
  return sessions;
}
