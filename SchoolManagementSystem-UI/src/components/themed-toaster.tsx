import { Toaster } from "sonner";
import { useTheme } from "@/lib/theme";

export function ThemedToaster() {
  const { theme } = useTheme();
  return <Toaster theme={theme} richColors closeButton position="top-right" />;
}
