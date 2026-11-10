import { useState, type FormEvent } from "react";
import { Navigate } from "react-router-dom";
import { toast } from "sonner";
import { ArrowRight, Building2, Eye, EyeOff, GraduationCap, Loader2, ShieldCheck, Sparkles } from "lucide-react";
import loginHero from "@/assets/images/loginpageimage.avif";
import { useAuth } from "@/lib/auth";
import { Btn, Field, TextInput } from "@/components/sms-ui";

const highlights = [
  { icon: Building2, title: "Every campus, one place", text: "Switch between branches or see them all from Main Campus." },
  { icon: ShieldCheck, title: "Access that stays in its lane", text: "Campus teams only see their own campus data." },
  { icon: Sparkles, title: "Roles you control", text: "Decide who can view, add, edit or delete — per menu." },
];

export function LoginPage() {
  const { user, loading, login } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!loading && user) {
    return <Navigate to="/admin" replace />;
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await login(email, password);
      toast.success("Welcome back");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Sign in failed");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="grid min-h-screen bg-[var(--background)] lg:grid-cols-[1.05fr_1fr]">
      {/* Brand side */}
      <div className="relative hidden overflow-hidden lg:block">
        <img src={loginHero} alt="" className="absolute inset-0 h-full w-full object-cover" />
        <div className="absolute inset-0 bg-[linear-gradient(160deg,oklch(0.32_0.16_277/92%),oklch(0.22_0.09_268/94%)_60%,oklch(0.16_0.04_268/96%))]" />
        <div
          className="absolute inset-0 opacity-[0.07]"
          style={{
            backgroundImage:
              "linear-gradient(white 1px, transparent 1px), linear-gradient(90deg, white 1px, transparent 1px)",
            backgroundSize: "44px 44px",
          }}
        />
        <div className="relative flex h-full flex-col justify-between p-12 text-white">
          <div className="flex items-center gap-3">
            <span className="inline-flex size-10 items-center justify-center rounded-xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
              <GraduationCap className="size-5" />
            </span>
            <span className="text-sm font-semibold tracking-wide">School Management System</span>
          </div>

          <div className="max-w-md">
            <h1 className="text-4xl leading-[1.15] font-semibold tracking-tight">
              One school.
              <br />
              <span className="text-white/70">Every campus, in sync.</span>
            </h1>
            <div className="mt-10 space-y-5">
              {highlights.map(({ icon: Icon, title, text }) => (
                <div key={title} className="flex gap-4">
                  <span className="inline-flex size-9 shrink-0 items-center justify-center rounded-lg bg-white/10 ring-1 ring-white/15">
                    <Icon className="size-4" />
                  </span>
                  <div>
                    <div className="text-sm font-semibold">{title}</div>
                    <div className="mt-0.5 text-sm text-white/65">{text}</div>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="text-xs text-white/50">© {new Date().getFullYear()} School Management System</div>
        </div>
      </div>

      {/* Form side */}
      <div className="flex items-center justify-center px-5 py-12 sm:px-10">
        <div className="w-full max-w-sm">
          <div className="mb-8 flex items-center gap-3 lg:hidden">
            <span className="inline-flex size-10 items-center justify-center rounded-xl bg-[var(--primary)] text-[var(--primary-foreground)]">
              <GraduationCap className="size-5" />
            </span>
            <span className="text-sm font-semibold">School Management System</span>
          </div>

          <h2 className="text-2xl font-semibold">Sign in</h2>
          <p className="mt-1.5 text-sm text-[var(--muted-foreground)]">Welcome back. Enter your account details to continue.</p>

          <form onSubmit={onSubmit} className="mt-8 space-y-5">
            <Field label="Email address">
              <TextInput
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@school.edu"
                required
                autoFocus
                autoComplete="username"
                className="h-10"
              />
            </Field>
            <Field label="Password">
              <div className="relative">
                <TextInput
                  type={showPassword ? "text" : "password"}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                  required
                  autoComplete="current-password"
                  className="h-10 pr-10"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((v) => !v)}
                  aria-label={showPassword ? "Hide password" : "Show password"}
                  className="absolute top-1/2 right-1.5 inline-flex size-7 -translate-y-1/2 items-center justify-center rounded-md text-[var(--muted-foreground)] hover:bg-[var(--muted)] hover:text-[var(--foreground)]"
                >
                  {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                </button>
              </div>
            </Field>

            {error ? (
              <div
                role="alert"
                className="animate-fade rounded-lg border border-[var(--danger)]/25 bg-[var(--danger-soft)] px-3 py-2.5 text-sm text-[var(--danger)]"
              >
                {error}
              </div>
            ) : null}

            <Btn type="submit" className="h-10 w-full" disabled={submitting}>
              {submitting ? (
                <>
                  <Loader2 className="animate-spin" /> Signing in…
                </>
              ) : (
                <>
                  Sign in <ArrowRight />
                </>
              )}
            </Btn>
          </form>

          <p className="mt-8 text-center text-xs text-[var(--muted-foreground)]">
            Trouble signing in? Contact your school administrator.
          </p>
        </div>
      </div>
    </div>
  );
}
