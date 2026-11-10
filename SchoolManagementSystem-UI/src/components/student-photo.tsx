import { useEffect, useState } from "react";
import { Avatar } from "@/components/sms-ui";
import { downloadBlob } from "@/lib/api";
import { cn } from "@/lib/utils";

const sizes = { sm: "size-7", md: "size-9", lg: "size-11", xl: "size-20" } as const;

/**
 * A student's photo loaded through the API (photos are never public URLs); falls back to initials.
 * `version` forces a reload after the photo changes.
 */
export function StudentPhoto({
  studentId,
  name,
  hasPhoto,
  size = "md",
  version = 0,
  path,
}: {
  studentId: number;
  /** API path of the photo; defaults to the student's. Staff pass `/staff/{id}/photo`. */
  path?: string;
  name: string;
  hasPhoto: boolean;
  size?: keyof typeof sizes;
  version?: number;
}) {
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!hasPhoto) {
      setUrl(null);
      return;
    }
    let objectUrl: string | null = null;
    let cancelled = false;
    downloadBlob(path ?? `/students/${studentId}/photo`)
      .then(({ blob }) => {
        if (cancelled) return;
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      })
      .catch(() => setUrl(null));
    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [studentId, hasPhoto, version, path]);

  if (!url) {
    return size === "xl" ? (
      <span className="inline-flex size-20 items-center justify-center rounded-2xl bg-[var(--primary-soft)] text-xl font-semibold text-[var(--primary-soft-foreground)]">
        {name
          .split(/\s+/)
          .filter(Boolean)
          .slice(0, 2)
          .map((p) => p[0]?.toUpperCase())
          .join("")}
      </span>
    ) : (
      <Avatar name={name} size={size === "lg" ? "lg" : size === "sm" ? "sm" : "md"} />
    );
  }

  return (
    <img
      src={url}
      alt=""
      className={cn("shrink-0 object-cover", sizes[size], size === "xl" ? "rounded-2xl" : "rounded-full")}
    />
  );
}
