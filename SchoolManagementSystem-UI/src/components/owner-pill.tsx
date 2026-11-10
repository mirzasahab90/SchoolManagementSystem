import { Lock } from "lucide-react";
import { StatusPill } from "@/components/sms-ui";

/** Shows who owns a row: Main Campus (shared by every campus) or one campus. */
export function OwnerPill({ campusCode, locked }: { campusCode?: string | null; locked?: boolean }) {
  if (!campusCode) {
    return (
      <StatusPill tone="info">
        {locked ? <Lock className="size-3" /> : null}
        Main Campus
      </StatusPill>
    );
  }
  return <StatusPill tone="primary">{campusCode}</StatusPill>;
}

/** True when the current user may change a row with this owner. */
export function canWriteOwned(user: { isHq: boolean } | null | undefined, campusId?: number | null) {
  if (!user) return false;
  return campusId == null ? user.isHq : true;
}
