"use client";

import { type FormEvent, useState } from "react";
import { Check, Copy } from "lucide-react";

import { ConfirmDialog } from "@/components/forms/ConfirmDialog";
import { FormAlert } from "@/components/forms/FormAlert";
import { FormField } from "@/components/forms/FormField";
import {
  EmptyState,
  ErrorState,
  errorMessage,
  LoadingCards,
  LoadMore,
} from "@/components/layout/PageStates";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api";
import { formatRelativeTime } from "@/lib/format";
import {
  type FieldErrors,
  hasErrors,
  readValidationErrors,
} from "@/lib/form-errors";
import type { CreateInviteRequest, InviteDto, InviteStatus } from "@/lib/types";
import { usePagedList } from "@/lib/use-api-data";
import { useApiFetch } from "@/lib/use-api-fetch";
import { cn } from "@/lib/utils";

// CreateInviteRequest ranges in the API.
const EXPIRY_MAX_DAYS = 90;
const MAX_USES_LIMIT = 100;

/** Creating, sharing and revoking Player invites (GameMasters only). */
export function SettingInvites({ settingId }: { settingId: string }) {
  const fetchApi = useApiFetch();
  const invites = usePagedList<InviteDto>(`/api/settings/${settingId}/invites`, 20);

  const [revoking, setRevoking] = useState<InviteDto>();
  const [revokeOpen, setRevokeOpen] = useState(false);

  return (
    <section className="space-y-4">
      <div>
        <h2 className="font-heading text-xl font-semibold">Invites</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Anyone with an active link can join as a Player. Only Game Masters see
          these links.
        </p>
      </div>

      <CreateInviteForm settingId={settingId} onCreated={invites.reload} />

      {invites.error ? (
        <ErrorState error={invites.error} onRetry={invites.reload} />
      ) : !invites.hasLoaded ? (
        <LoadingCards count={2} />
      ) : invites.items.length === 0 ? (
        <EmptyState
          title="No invites yet"
          description="Create an invite link above and share it with your players."
        />
      ) : (
        <>
          <ul className="divide-y divide-border border border-border">
            {invites.items.map((invite) => (
              <InviteRow
                key={invite.id}
                invite={invite}
                onRevoke={() => {
                  setRevoking(invite);
                  setRevokeOpen(true);
                }}
              />
            ))}
          </ul>

          {invites.hasMore && (
            <LoadMore
              shown={invites.items.length}
              total={invites.totalCount}
              loading={invites.loadingMore}
              onLoadMore={invites.loadMore}
            />
          )}
        </>
      )}

      <ConfirmDialog
        open={revokeOpen}
        onOpenChange={setRevokeOpen}
        title="Revoke this invite?"
        description={`Nobody can join with ${revoking?.code ?? "this code"} any more. People who already joined stay.`}
        confirmLabel="Revoke"
        onConfirm={async () => {
          if (!revoking) return;
          await fetchApi<void>(`/api/settings/${settingId}/invites/${revoking.id}`, {
            method: "DELETE",
          });
          invites.reload();
        }}
      />
    </section>
  );
}

type CreateInviteFormProps = {
  settingId: string;
  onCreated: () => void;
};

function CreateInviteForm({ settingId, onCreated }: CreateInviteFormProps) {
  const fetchApi = useApiFetch();

  const [expiresInDays, setExpiresInDays] = useState("7");
  const [maxUses, setMaxUses] = useState("");
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [created, setCreated] = useState<InviteDto>();

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(undefined);

    const days = Number(expiresInDays);
    const uses = maxUses.trim() ? Number(maxUses) : null;

    const errors: FieldErrors = {
      expiresInDays:
        Number.isInteger(days) && days >= 1 && days <= EXPIRY_MAX_DAYS
          ? undefined
          : `Enter 1 to ${EXPIRY_MAX_DAYS} days.`,
      maxUses:
        uses === null || (Number.isInteger(uses) && uses >= 1 && uses <= MAX_USES_LIMIT)
          ? undefined
          : `Enter 1 to ${MAX_USES_LIMIT}, or leave empty for unlimited.`,
    };

    setFieldErrors(errors);
    if (hasErrors(errors)) return;

    setIsSubmitting(true);

    try {
      const invite = await fetchApi<InviteDto>(`/api/settings/${settingId}/invites`, {
        method: "POST",
        json: { expiresInDays: days, maxUses: uses } satisfies CreateInviteRequest,
      });
      setCreated(invite);
      onCreated();
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        const result = readValidationErrors(error, ["expiresInDays", "maxUses"]);
        setFieldErrors(result.fieldErrors);
        setFormError(result.general);
      } else {
        // 409: the setting already has the maximum of 20 active invites.
        setFormError(errorMessage(error));
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <form
      onSubmit={handleSubmit}
      noValidate
      className="space-y-4 border border-border bg-card p-4"
    >
      {formError && <FormAlert>{formError}</FormAlert>}

      <div className="grid gap-4 sm:grid-cols-2">
        <FormField
          id="invite-expiry"
          label="Expires after (days)"
          type="number"
          inputMode="numeric"
          min={1}
          max={EXPIRY_MAX_DAYS}
          value={expiresInDays}
          onChange={(event) => setExpiresInDays(event.target.value)}
          error={fieldErrors.expiresInDays}
        />
        <FormField
          id="invite-max-uses"
          label="Maximum uses"
          type="number"
          inputMode="numeric"
          min={1}
          max={MAX_USES_LIMIT}
          placeholder="Unlimited"
          value={maxUses}
          onChange={(event) => setMaxUses(event.target.value)}
          error={fieldErrors.maxUses}
        />
      </div>

      <Button type="submit" disabled={isSubmitting}>
        {isSubmitting ? "Creating..." : "Create Invite Link"}
      </Button>

      {created && (
        <FormAlert variant="success">
          <div className="flex flex-wrap items-center gap-2">
            <span>New link:</span>
            <code className="break-all">{created.joinUrl}</code>
            <CopyButton text={created.joinUrl} />
          </div>
        </FormAlert>
      )}
    </form>
  );
}

const statusStyles: Record<InviteStatus, string> = {
  Active: "bg-primary/10 text-primary",
  Expired: "bg-muted text-muted-foreground",
  Revoked: "bg-destructive/10 text-destructive",
  Exhausted: "bg-muted text-muted-foreground",
};

function InviteRow({ invite, onRevoke }: { invite: InviteDto; onRevoke: () => void }) {
  const isActive = invite.status === "Active";

  return (
    <li className="flex flex-col gap-3 p-3 sm:flex-row sm:items-center sm:justify-between">
      <div className="min-w-0 space-y-1">
        <div className="flex flex-wrap items-center gap-2">
          <code className="font-medium">{invite.code}</code>
          <span
            className={cn(
              "rounded-full px-2 py-0.5 text-xs font-medium",
              statusStyles[invite.status]
            )}
          >
            {invite.status === "Exhausted" ? "Used up" : invite.status}
          </span>
        </div>
        <p className="text-xs text-muted-foreground">
          {invite.useCount} of {invite.maxUses ?? "unlimited"} uses
          {invite.expiresAt &&
            ` · ${new Date(invite.expiresAt) > new Date() ? "expires" : "expired"} ${formatRelativeTime(invite.expiresAt)}`}
          {` · created ${formatRelativeTime(invite.createdAt)}`}
        </p>
      </div>

      {isActive && (
        <div className="flex flex-wrap gap-2">
          <CopyButton text={invite.joinUrl} label="Copy Link" />
          <Button size="sm" variant="outline" onClick={onRevoke}>
            Revoke
          </Button>
        </div>
      )}
    </li>
  );
}

function CopyButton({ text, label = "Copy" }: { text: string; label?: string }) {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard blocked (e.g. not HTTPS): the link is shown to copy by hand.
      window.prompt("Copy this link:", text);
    }
  }

  return (
    <Button type="button" size="sm" variant="outline" onClick={handleCopy}>
      {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
      {copied ? "Copied" : label}
    </Button>
  );
}
