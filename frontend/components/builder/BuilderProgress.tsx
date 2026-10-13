type BuilderProgressProps = {
  /** 1-based position of the open step. */
  position: number;
  totalSteps: number;
  stepTitle: string;
};

export function BuilderProgress({ position, totalSteps, stepTitle }: BuilderProgressProps) {
  const progress = (position / totalSteps) * 100;

  return (
    <div className="space-y-3">
      <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-sm font-medium text-muted-foreground">
            Step {position} of {totalSteps}
          </p>
          <h2 className="font-heading text-xl font-semibold">{stepTitle}</h2>
        </div>
        <span className="text-sm text-muted-foreground">{Math.round(progress)}%</span>
      </div>

      <div
        className="h-2 overflow-hidden rounded-full bg-muted"
        role="progressbar"
        aria-valuemin={0}
        aria-valuemax={totalSteps}
        aria-valuenow={position}
      >
        <div
          className="h-full rounded-full bg-primary transition-all"
          style={{ width: `${progress}%` }}
        />
      </div>
    </div>
  );
}
