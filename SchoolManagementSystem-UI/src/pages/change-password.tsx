import { useState, type FormEvent } from "react";
import { Eye, EyeOff, GraduationCap, KeyRound, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { Btn, Field, TextInput } from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";

/**
 * Change-password form. `forced` renders it full screen before the app (first sign-in);
 * otherwise it is embedded in a dialog from the user menu.
 */
export function ChangePasswordForm({ onDone, forced }: { onDone?: () => void; forced?: boolean }) {
  const { changePassword, logout } = useAuth();
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [show, setShow] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const tooShort = next.length > 0 && next.length < 8;
  const mismatch = confirm.length > 0 && confirm !== next;

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (next.length < 8 || next !== confirm) return;
    setSaving(true);
    setError(null);
    try {
      await changePassword(current, next);
      toast.success("Password updated");
      onDone?.();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not update password");
    } finally {
      setSaving(false);
    }
  }

  const type = show ? "text" : "password";
  return (
    <form onSubmit={onSubmit} className="space-y-4">
      <Field label="Current password" required hint={forced ? "The temporary password you were given." : undefined}>
        <TextInput type={type} value={current} onChange={(e) => setCurrent(e.target.value)} required autoComplete="current-password" autoFocus />
      </Field>
      <Field label="New password" required hint={tooShort ? undefined : "At least 8 characters."}>
        <TextInput type={type} value={next} onChange={(e) => setNext(e.target.value)} required minLength={8} autoComplete="new-password" />
      </Field>
      {tooShort ? <p className="-mt-2 text-xs text-[var(--danger)]">Use at least 8 characters.</p> : null}
      <Field label="Confirm new password" required>
        <TextInput type={type} value={confirm} onChange={(e) => setConfirm(e.target.value)} required autoComplete="new-password" />
      </Field>
      {mismatch ? <p className="-mt-2 text-xs text-[var(--danger)]">Passwords do not match.</p> : null}
      <button
        type="button"
        onClick={() => setShow((v) => !v)}
        className="inline-flex items-center gap-1.5 text-xs font-medium text-[var(--muted-foreground)] hover:text-[var(--foreground)]"
      >
        {show ? <EyeOff className="size-3.5" /> : <Eye className="size-3.5" />}
        {show ? "Hide passwords" : "Show passwords"}
      </button>
      {error ? (
        <div role="alert" className="rounded-lg border border-[var(--danger)]/25 bg-[var(--danger-soft)] px-3 py-2.5 text-sm text-[var(--danger)]">
          {error}
        </div>
      ) : null}
      <div className="flex gap-2 pt-1">
        {forced ? (
          <Btn type="button" variant="outline" onClick={() => void logout()}>
            Sign out
          </Btn>
        ) : null}
        <Btn type="submit" className="flex-1" disabled={saving || tooShort || mismatch || !current || !next || !confirm}>
          {saving ? (
            <>
              <Loader2 className="animate-spin" /> Saving…
            </>
          ) : (
            "Update password"
          )}
        </Btn>
      </div>
    </form>
  );
}

/** Full-screen first-sign-in step shown instead of the app until the password is changed. */
export function ChangePasswordPage() {
  const { user } = useAuth();
  return (
    <div className="grid min-h-screen place-items-center bg-[var(--background)] px-5 py-12">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex items-center gap-3">
          <span className="inline-flex size-10 items-center justify-center rounded-xl bg-[var(--primary)] text-[var(--primary-foreground)]">
            <GraduationCap className="size-5" />
          </span>
          <span className="text-sm font-semibold">{user?.schoolName ?? "School Management System"}</span>
        </div>
        <span className="mb-4 inline-flex size-11 items-center justify-center rounded-xl bg-[var(--primary-soft)] text-[var(--primary-soft-foreground)]">
          <KeyRound className="size-5" />
        </span>
        <h1 className="text-2xl font-semibold">Set a new password</h1>
        <p className="mt-1.5 mb-8 text-sm text-[var(--muted-foreground)]">
          Welcome{user ? `, ${user.firstName}` : ""}. For your security, choose your own password before you continue.
        </p>
        <ChangePasswordForm forced />
      </div>
    </div>
  );
}
