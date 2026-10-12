// Auth pages (login, register, email links) have no app chrome: no sidebar.
export default function AuthLayout({ children }: LayoutProps<"/">) {
  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/40 px-4 py-12 text-foreground">
      {children}
    </div>
  );
}
