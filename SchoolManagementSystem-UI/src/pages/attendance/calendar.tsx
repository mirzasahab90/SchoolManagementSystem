import { useCallback, useEffect, useState } from "react";
import { CalendarDays, Pencil, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { ConfirmDialog, Modal } from "@/components/modal";
import { Btn, ChoiceCard, EmptyState, Field, IconButton, PageHeader, Panel, Skeleton, StatusPill, Switch, TextInput } from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import { weekDays, type CalendarInfo, type Holiday, type WeekDay } from "@/lib/types";
import { errorText, formatDate } from "@/pages/hiring/shared";
import { isoToday } from "./shared";

export function CalendarPage() {
  const { can, user: me } = useAuth();
  const [info, setInfo] = useState<CalendarInfo | null>(null);
  const [off, setOff] = useState<WeekDay[]>([]);
  const [editing, setEditing] = useState<Holiday | "new" | null>(null);
  const [deleting, setDeleting] = useState<Holiday | null>(null);
  const onAllCampuses = me?.activeCampusId == null;
  const canEdit = can("calendar", "Edit");

  const load = useCallback(async () => {
    try {
      const c = (await unwrap(api.get("/calendar"))) as CalendarInfo;
      setInfo(c);
      setOff(c.weeklyOff);
    } catch (err) {
      toast.error(errorText(err, "Could not load the calendar"));
    }
  }, []);

  useEffect(() => {
    if (!onAllCampuses) void load();
  }, [load, onAllCampuses]);

  if (onAllCampuses) {
    return (
      <div>
        <PageHeader title="Calendar" />
        <div className="panel">
          <EmptyState icon={CalendarDays} title="Pick a campus first" description="Each campus has its own weekly off and holidays." />
        </div>
      </div>
    );
  }

  const saveOff = async () => {
    try {
      const c = (await unwrap(api.put("/calendar/weekly-off", { days: off }))) as CalendarInfo;
      setInfo(c);
      toast.success("Weekly off saved");
    } catch (err) {
      toast.error(errorText(err, "Could not save"));
    }
  };

  const changed = info != null && [...off].sort().join() !== [...info.weeklyOff].sort().join();

  return (
    <div>
      <PageHeader title="Calendar" description="Attendance cannot be marked on weekly offs and holidays, and reports leave them out." />
      {!info ? (
        <Skeleton className="h-64 rounded-xl" />
      ) : (
        <div className="grid gap-4 lg:grid-cols-3">
          <Panel
            title="Weekly off"
            action={
              canEdit ? (
                <Btn type="button" size="sm" disabled={!changed} onClick={() => void saveOff()}>
                  Save
                </Btn>
              ) : null
            }
          >
            <div className="grid grid-cols-2 gap-2">
              {weekDays.map((d) => (
                <ChoiceCard
                  key={d}
                  title={d}
                  checked={off.includes(d)}
                  disabled={!canEdit}
                  onToggle={() => setOff((list) => (list.includes(d) ? list.filter((x) => x !== d) : [...list, d]))}
                />
              ))}
            </div>
          </Panel>
          <Panel
            className="lg:col-span-2"
            title="Holidays"
            action={
              can("calendar", "Add") ? (
                <Btn type="button" size="sm" onClick={() => setEditing("new")}>
                  <Plus /> Add holiday
                </Btn>
              ) : null
            }
          >
            {info.holidays.length === 0 ? (
              <p className="text-sm text-[var(--muted-foreground)]">No holidays yet.</p>
            ) : (
              <ul className="divide-y divide-[var(--border)]">
                {info.holidays.map((h) => {
                  const mainCampus = h.campusId == null;
                  const canTouch = !mainCampus || me?.isHq;
                  return (
                    <li key={h.id} className="flex flex-wrap items-center gap-3 py-2.5">
                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-2 text-sm font-medium">
                          {h.name}
                          {mainCampus ? <StatusPill tone="primary">All campuses</StatusPill> : null}
                          {h.endDate < isoToday() ? <StatusPill>Past</StatusPill> : null}
                        </div>
                        <div className="text-xs text-[var(--muted-foreground)]">
                          {h.startDate === h.endDate ? formatDate(h.startDate) : `${formatDate(h.startDate)} – ${formatDate(h.endDate)}`}
                        </div>
                      </div>
                      {canTouch && canEdit ? (
                        <IconButton label="Edit" onClick={() => setEditing(h)}>
                          <Pencil />
                        </IconButton>
                      ) : null}
                      {canTouch && can("calendar", "Delete") ? (
                        <IconButton label="Delete" tone="danger" onClick={() => setDeleting(h)}>
                          <Trash2 />
                        </IconButton>
                      ) : null}
                    </li>
                  );
                })}
              </ul>
            )}
          </Panel>
        </div>
      )}

      {editing ? (
        <HolidayDialog
          holiday={editing === "new" ? null : editing}
          allowAllCampuses={!!me?.isHq}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            void load();
          }}
        />
      ) : null}
      <ConfirmDialog
        open={!!deleting}
        title={`Delete ${deleting?.name}?`}
        description="Days in this holiday become working days again in attendance and reports."
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          try {
            await unwrap(api.delete(`/calendar/holidays/${deleting!.id}`));
            setDeleting(null);
            toast.success("Holiday deleted");
            void load();
          } catch (err) {
            toast.error(errorText(err, "Could not delete"));
          }
        }}
      />
    </div>
  );
}

function HolidayDialog({
  holiday,
  allowAllCampuses,
  onClose,
  onSaved,
}: {
  holiday: Holiday | null;
  allowAllCampuses: boolean;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [name, setName] = useState(holiday?.name ?? "");
  const [start, setStart] = useState(holiday?.startDate ?? isoToday());
  const [end, setEnd] = useState(holiday?.endDate ?? isoToday());
  const [all, setAll] = useState(holiday ? holiday.campusId == null : false);
  const [busy, setBusy] = useState(false);

  const save = async () => {
    setBusy(true);
    try {
      const body = { name: name.trim(), startDate: start, endDate: end, allCampuses: all };
      if (holiday) await unwrap(api.put(`/calendar/holidays/${holiday.id}`, body));
      else await unwrap(api.post("/calendar/holidays", body));
      toast.success(holiday ? "Holiday updated" : "Holiday added");
      onSaved();
    } catch (err) {
      toast.error(errorText(err, "Could not save"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      open
      title={holiday ? "Edit holiday" : "Add holiday"}
      onClose={onClose}
      footer={
        <>
          <Btn type="button" variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn type="button" disabled={busy || !name.trim() || !start || !end || end < start} onClick={() => void save()}>
            {busy ? "Saving…" : "Save"}
          </Btn>
        </>
      }
    >
      <div className="space-y-4">
        <Field label="Name" required>
          <TextInput value={name} maxLength={100} placeholder="e.g. Eid ul Fitr" onChange={(e) => setName(e.target.value)} autoFocus />
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="From" required>
            <TextInput type="date" value={start} onChange={(e) => setStart(e.target.value)} />
          </Field>
          <Field label="To" required hint="Up to 60 days">
            <TextInput type="date" value={end} min={start} onChange={(e) => setEnd(e.target.value)} />
          </Field>
        </div>
        {allowAllCampuses && !holiday ? (
          <Switch checked={all} onChange={setAll} label="All campuses" description="A Main Campus holiday every campus follows." />
        ) : null}
      </div>
    </Modal>
  );
}
