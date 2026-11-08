import { useState, type FormEvent } from "react";
import { Navigate } from "react-router-dom";
import { toast } from "sonner";
import loginHero from "@/assets/images/loginpageimage.avif";
import { useAuth } from "@/lib/auth";
import { Btn, Field, TextInput } from "@/components/sms-ui";

export function LoginPage() {
  const { user, loading, login } = useAuth();
  const [email, setEmail] = useState("admin@sms.com");
  const [password, setPassword] = useState("@123@123");
  const [submitting, setSubmitting] = useState(false);

  if (!loading && user) {
    return <Navigate to="/admin" replace />;
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    try {
      await login(email, password);
      toast.success("Welcome back");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Login failed");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      <div className="relative hidden overflow-hidden lg:block">
        <img src={loginHero} alt="School Management System" className="absolute inset-0 h-full w-full object-cover" />
        <div className="absolute inset-0 bg-gradient-to-t from-[oklch(0.2_0.05_230/80%)] via-[oklch(0.25_0.05_230/35%)] to-transparent" />
        <div className="absolute inset-x-0 bottom-0 p-10 text-white">
          <div className="font-display text-3xl font-semibold">School Management System</div>
          <p className="mt-3 max-w-md text-white/85">
            One school application with multiple campuses — admissions, fees, attendance, and more.
          </p>
        </div>
      </div>
      <div className="flex items-center justify-center p-6">
        <form onSubmit={onSubmit} className="panel w-full max-w-md space-y-4 p-8">
          <div>
            <h1 className="font-display text-2xl font-semibold">Sign in</h1>
            <p className="mt-1 text-sm text-[var(--muted-foreground)]">Use your school account</p>
          </div>
          <Field label="Email">
            <TextInput type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          </Field>
          <Field label="Password">
            <TextInput
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </Field>
          <Btn type="submit" className="w-full" disabled={submitting}>
            {submitting ? "Signing in…" : "Sign in"}
          </Btn>
        </form>
      </div>
    </div>
  );
}
