import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { ThemedToaster } from "./components/themed-toaster";
import App from "./App";
import { AuthProvider } from "./lib/auth";
import { initTheme } from "./lib/theme";
import "./styles.css";

initTheme();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <App />
        <ThemedToaster />
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>,
);
